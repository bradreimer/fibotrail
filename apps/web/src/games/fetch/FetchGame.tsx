import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';

import { apiSend } from '../../api/client';
import type { components } from '../../api/schema';
import { useSession } from '../../api/session';
import { useApi } from '../../api/useApi';
import { ErrorState } from '../../states/ErrorState';
import { countOf, formatDateRange } from '../../trips/dates';
import { GameEnd } from '../GameEnd';
import { localDate } from '../today';
import { drawFrame, type Scene } from './draw';
import { Flight, type Course, type RunGrade } from './flight';
import spriteUrl from './schnauzer.png';
import './fetch.css';

type Puzzle = components['schemas']['FetchPuzzleView'];
type Result = components['schemas']['FetchResultView'];

/** One tick of the physics, in milliseconds. */
const TICK_MS = 1000 / 60;
/** The longest gap between frames that's simulated, so a hidden tab doesn't jump ahead. */
const MAX_FRAME_MS = 100;
/** How long a caught photo shows in the corner. */
const POP_MS = 1200;
/** Every course has 8 treats, one after each gate. */
const TREATS = 8;

/**
 * The Fetch game: a schnauzer flies one of your trips. Tap to flap through 8 gates that follow the
 * trip north and south, and catch its photos on the way. The browser flies each run to draw it,
 * and the server flies it again from the taps to grade it. See docs/specs/fetch-game.md.
 */
export default function FetchGame() {
  const date = localDate();
  const puzzle = useApi('/api/games/fetch/puzzle', `/api/games/fetch/puzzle?date=${date}`);
  const [finished, setFinished] = useState<Result | null>(null);

  if (puzzle.loading) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading today's course</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (puzzle.error) return <ErrorState message={puzzle.error} onRetry={puzzle.reload} />;
  if (!puzzle.data) return null;

  const data = puzzle.data;
  const heading = (
    <h2 className="timeline__title">
      Fetch <span className="timeline__number">#{data.number}</span>
    </h2>
  );

  if (data.tripsNeeded > 0 || !data.course) {
    return (
      <section className="fetch">
        {heading}
        <p className="games-page__lead">
          Fetch needs a trip with photos that have a location. Add one, and today's course is ready.{' '}
          <Link to="/inbox">Open the inbox</Link>
        </p>
      </section>
    );
  }

  const result = finished ?? data.result;
  if (result) return <FetchResult heading={heading} result={result} />;
  return <FetchPlay heading={heading} puzzle={data} course={data.course} onFinish={setFinished} />;
}

type Phase = 'ready' | 'flying' | 'checking' | 'between';

/** Says how a run ended, in words. */
function describeRun(run: RunGrade, treats: number): string {
  const caught = `${run.caught.length} of ${treats} treats`;
  if (run.end === 'finish') return `Finished, with ${caught}.`;
  if (run.end === 'gate') return `Crashed at gate ${run.gatesPassed + 1}, with ${caught}.`;
  if (run.end === 'ceiling') return `Flew too high, with ${caught}.`;
  return `Hit the ground, with ${caught}.`;
}

function useImage(url: string): HTMLImageElement | null {
  const [image, setImage] = useState<HTMLImageElement | null>(null);
  useEffect(() => {
    const img = new Image();
    img.onload = () => setImage(img);
    img.src = url;
    return () => {
      img.onload = null;
    };
  }, [url]);
  return image;
}

/** Loads the treat photos, so a treat shows its photo once it arrives. */
function usePhotos(ids: readonly string[]): ReadonlyMap<string, HTMLImageElement> {
  const [photos, setPhotos] = useState<ReadonlyMap<string, HTMLImageElement>>(new Map());
  const key = [...new Set(ids)].join(',');
  useEffect(() => {
    const images = key.split(',').map((id) => {
      const img = new Image();
      img.onload = () => setPhotos((loaded) => new Map(loaded).set(id, img));
      img.src = `/api/photos/${id}/thumbnail`;
      return img;
    });
    return () => images.forEach((img) => (img.onload = null));
  }, [key]);
  return photos;
}

/** Today's course while you fly it. The runs and their taps live here until the game ends. */
function FetchPlay({
  heading,
  puzzle,
  course,
  onFinish,
}: {
  heading: ReactNode;
  puzzle: Puzzle;
  course: Course;
  onFinish: (result: Result) => void;
}) {
  const session = useSession();
  const canvas = useRef<HTMLCanvasElement>(null);
  const flight = useRef(new Flight(course));
  const taps = useRef<number[]>([]);
  const pending = useRef(false);
  const allTaps = useRef<number[][]>([]);
  const [phase, setPhase] = useState<Phase>('ready');
  const [runs, setRuns] = useState<RunGrade[]>([]);
  const [pop, setPop] = useState<string | null>(null);
  const [announcement, setAnnouncement] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const sprite = useImage(spriteUrl);
  const photos = usePhotos(course.treats.map((treat) => treat.photoId));
  const scene = useRef<Scene>({ course, sprite, photos });
  useEffect(() => {
    scene.current = { course, sprite, photos };
  }, [course, sprite, photos]);
  const runsLeft = puzzle.maxTries - runs.length;
  const treatCount = course.treats.length;

  const draw = useCallback((seconds: number) => {
    const ctx = canvas.current?.getContext('2d');
    if (!ctx) return;
    const { physics } = scene.current.course;
    const ratio = window.devicePixelRatio || 1;
    const width = physics.worldWidth / 100;
    const height = physics.worldHeight / 100;
    if (ctx.canvas.width !== width * ratio) {
      ctx.canvas.width = width * ratio;
      ctx.canvas.height = height * ratio;
    }
    ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    drawFrame(ctx, scene.current, flight.current, seconds);
  }, []);

  // Draw the still course before a run and between runs, and again when an image arrives.
  useEffect(() => {
    if (phase !== 'flying') draw(0);
  }, [phase, draw, sprite, photos]);

  const endRun = useCallback(async () => {
    setPhase('checking');
    setProblem(null);
    const runTaps = taps.current;
    try {
      if (!session) throw new Error('Sign in again to save this run.');
      const grade = await apiSend<RunGrade>('POST', '/api/games/fetch/check', session.csrfToken, {
        date: puzzle.date,
        taps: runTaps,
      });
      const all = [...allTaps.current, runTaps];
      allTaps.current = all;
      setRuns((done) => [...done, grade]);
      setAnnouncement(describeRun(grade, treatCount));
      if (grade.end === 'finish' || all.length >= puzzle.maxTries) {
        onFinish(
          await apiSend<Result>('POST', '/api/games/fetch/results', session.csrfToken, {
            date: puzzle.date,
            runs: all,
          }),
        );
        return;
      }
      setPhase('between');
    } catch (error) {
      setProblem(error instanceof Error ? error.message : 'Something went wrong. Try again.');
      setPhase('between');
    }
  }, [session, puzzle.date, puzzle.maxTries, treatCount, onFinish]);

  // The game loop: fixed ticks of 1/60 s, drawn on each animation frame.
  useEffect(() => {
    if (phase !== 'flying') return;
    let frame = 0;
    let last = performance.now();
    let spare = 0;
    let popTimer: ReturnType<typeof setTimeout> | undefined;
    const start = last;
    const loop = (now: number) => {
      spare += Math.min(now - last, MAX_FRAME_MS);
      last = now;
      const run = flight.current;
      while (spare >= TICK_MS && !run.end) {
        spare -= TICK_MS;
        const flap = pending.current;
        pending.current = false;
        if (flap && run.tick >= 1) taps.current.push(run.tick + 1);
        for (const index of run.step(flap)) {
          const treat = course.treats[index];
          if (!treat) continue;
          setPop(treat.photoId);
          setAnnouncement(`Treat ${index + 1} of ${treatCount} caught.`);
          clearTimeout(popTimer);
          popTimer = setTimeout(() => setPop(null), POP_MS);
        }
      }
      draw((now - start) / 1000);
      if (run.end) {
        void endRun();
        return;
      }
      frame = requestAnimationFrame(loop);
    };
    frame = requestAnimationFrame(loop);
    return () => {
      cancelAnimationFrame(frame);
      clearTimeout(popTimer);
    };
  }, [phase, course, treatCount, draw, endRun]);

  function press() {
    if (phase === 'flying') {
      pending.current = true;
      return;
    }
    if (phase === 'checking' || runsLeft <= 0) return;
    // The first tap starts a run and is its first flap, on tick 1.
    flight.current = new Flight(course);
    taps.current = [];
    pending.current = true;
    setPop(null);
    setAnnouncement('Flying.');
    setPhase('flying');
  }

  const last = runs[runs.length - 1];
  const hint =
    phase === 'flying'
      ? null
      : phase === 'checking'
        ? 'Checking…'
        : phase === 'between' && last
          ? `${describeRun(last, treatCount)} Tap to fly again.`
          : 'Tap to start';

  return (
    <section className="fetch">
      {heading}
      <p className="games-page__lead">
        Tap to flap through the gates and catch the photos. North is up.{' '}
        {countOf(runsLeft, 'run', 'runs')} left.
      </p>
      <button
        type="button"
        className="fetch__stage"
        aria-label="Flap"
        aria-describedby="fetch-help"
        onPointerDown={(event) => {
          event.preventDefault();
          press();
        }}
        onKeyDown={(event) => {
          if (event.key !== ' ' && event.key !== 'Enter') return;
          event.preventDefault();
          if (!event.repeat) press();
        }}
      >
        <canvas ref={canvas} className="fetch__canvas" aria-hidden="true" />
        {hint && (
          <span className="fetch__hint" aria-hidden="true">
            {hint}
          </span>
        )}
        {pop && (
          <img
            className="fetch__pop"
            src={`/api/photos/${pop}/thumbnail`}
            alt=""
            aria-hidden="true"
          />
        )}
      </button>
      <p className="visually-hidden" id="fetch-help">
        A real-time game: each press flaps. The trip shows at the end.
      </p>
      <p className="visually-hidden" role="status">
        {announcement}
      </p>
      {runs.length > 0 && <RunList runs={runs} treats={treatCount} />}
      {problem && (
        <p className="timeline__problem" role="alert">
          {problem}
        </p>
      )}
    </section>
  );
}

/** One line per run: a bone for each caught treat, then how it ended. */
function RunList({ runs, treats }: { runs: readonly RunGrade[]; treats: number }) {
  return (
    <ol className="fetch__runs" aria-label="Your runs">
      {runs.map((run, index) => {
        const reached = run.end === 'finish' ? treats : run.gatesPassed;
        const marks = Array.from({ length: reached }, (_, i) =>
          run.caught.includes(i) ? '🦴' : '⬛',
        );
        return (
          <li key={index} aria-label={`Run ${index + 1}: ${describeRun(run, treats)}`}>
            <span aria-hidden="true">
              {marks.join('')}
              {run.end === 'finish' ? '🏁' : '💥'}
            </span>
          </li>
        );
      })}
    </ol>
  );
}

/** The end of the day: the caught photos and the trip they come from. */
function FetchResult({ heading, result }: { heading: ReactNode; result: Result }) {
  const { trip } = result;
  const treats = TREATS;

  return (
    <section className="fetch">
      {heading}
      <p className="timeline__verdict">
        {result.solved
          ? `Finished on run ${result.tries}, with ${result.treats} of ${treats} treats.`
          : `No finish today. Your best run caught ${countOf(result.treats, 'treat', 'treats')}.`}
      </p>
      <RunList runs={result.runs} treats={treats} />
      {result.caughtPhotoIds.length > 0 && (
        <ul className="fetch__caught" aria-label="The photos you caught">
          {result.caughtPhotoIds.map((id, index) => (
            <li key={`${id}-${index}`}>
              <img
                src={`/api/photos/${id}/thumbnail`}
                alt={`Treat ${index + 1} of ${trip.tripName}`}
              />
            </li>
          ))}
        </ul>
      )}
      <p className="fetch__trip">
        You flew{' '}
        <Link className="slide-photo__name" to={`/trips/${trip.tripId}`}>
          {trip.tripName}
        </Link>
        <span className="slide-photo__detail">
          {formatDateRange(trip.startDate, trip.endDate)}
          {trip.place && ` · ${trip.place}`}
        </span>
      </p>
      <GameEnd
        game="fetch"
        share={result.share}
        stats={result.stats}
        today={result.solved ? result.tries : undefined}
      />
    </section>
  );
}
