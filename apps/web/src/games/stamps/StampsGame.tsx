import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';

import { apiSend } from '../../api/client';
import type { components } from '../../api/schema';
import { useSession } from '../../api/session';
import { useApi } from '../../api/useApi';
import { ErrorState } from '../../states/ErrorState';
import { countOf } from '../../trips/dates';
import { GameEnd } from '../GameEnd';
import { formatTime } from '../time';
import { localDate } from '../today';
import {
  autoCrossed,
  clearSavedGame,
  conflicts,
  crossesOf,
  isSolved,
  loadAutoCross,
  loadGame,
  nextCell,
  parseRegions,
  saveAutoCross,
  saveGame,
  stampsOf,
  type Cell,
} from './board';
import { StampsBoard } from './StampsBoard';
import { useStopwatch } from './stopwatch';
import './stamps.css';

type Puzzle = components['schemas']['StampsPuzzleView'];
type Result = components['schemas']['StampsResultView'];
type Hint = components['schemas']['StampsHintView'];

/** How long the solved board shows before the passport page opens. */
export const REVEAL_MS = 600;

/**
 * The Stamps game: an N by N board where each colored region is one of your trips. Place one stamp
 * in each row, column, and trip, with no two touching. When it's solved, the stamped photos become
 * a passport page. The server grades the stamps and stores the day's result. See
 * docs/specs/stamps-game.md.
 */
export default function StampsGame() {
  const date = localDate();
  const puzzle = useApi('/api/games/stamps/puzzle', `/api/games/stamps/puzzle?date=${date}`);
  const [finished, setFinished] = useState<Result | null>(null);

  if (puzzle.loading) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading today's board</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (puzzle.error) return <ErrorState message={puzzle.error} onRetry={puzzle.reload} />;
  if (!puzzle.data) return null;

  const data = puzzle.data;
  const heading = (
    <h2 className="timeline__title">
      Stamps <span className="timeline__number">#{data.number}</span>
    </h2>
  );

  if (data.tripsNeeded > 0 || data.size === 0) {
    return (
      <section className="stamps">
        {heading}
        <p className="games-page__lead">
          Stamps needs 5 trips with photos. Add {countOf(data.tripsNeeded, 'more trip')}, and
          today's board is ready. <Link to="/inbox">Open the inbox</Link>
        </p>
      </section>
    );
  }

  const result = finished ?? data.result;
  if (result) return <StampsResult heading={heading} result={result} />;
  return <StampsPlay heading={heading} puzzle={data} onFinish={setFinished} />;
}

/** Today's board while you play it. The marks, the clock, and the hints live here. */
function StampsPlay({
  heading,
  puzzle,
  onFinish,
}: {
  heading: ReactNode;
  puzzle: Puzzle;
  onFinish: (result: Result) => void;
}) {
  const session = useSession();
  const size = puzzle.size;
  const regions = useMemo(() => parseRegions(puzzle.regions), [puzzle.regions]);
  const saved = useMemo(() => loadGame(puzzle.date, size), [puzzle.date, size]);
  const [cells, setCells] = useState<Cell[]>(
    () => saved?.cells ?? Array.from({ length: size * size }, () => 'empty' as const),
  );
  const [history, setHistory] = useState<Cell[][]>([]);
  const [hints, setHints] = useState(saved?.hints ?? 0);
  const [hint, setHint] = useState<Hint | null>(null);
  const [autoCross, setAutoCross] = useState(loadAutoCross);
  const [problem, setProblem] = useState<string | null>(null);
  const [announcement, setAnnouncement] = useState('');
  const clock = useStopwatch(saved?.seconds ?? 0);
  const reveal = useRef<number | undefined>(undefined);
  const solved = isSolved(size, regions, cells);
  const broken = conflicts(size, regions, cells);
  const ruledOut = autoCross ? autoCrossed(size, regions, cells) : new Set<number>();

  useEffect(() => () => window.clearTimeout(reveal.current), []);

  // A board picked up later the same day keeps counting from where it stopped.
  useEffect(() => {
    if (saved && saved.seconds > 0 && !solved) clock.start();
    // Only on the first render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!solved) saveGame({ date: puzzle.date, cells, seconds: clock.seconds, hints });
  }, [puzzle.date, cells, clock.seconds, hints, solved]);

  async function send(stamps: number[], seconds: number) {
    if (!session) return;
    setProblem(null);
    try {
      const result = await apiSend<Result>('POST', '/api/games/stamps/results', session.csrfToken, {
        date: puzzle.date,
        stamps,
        seconds: Math.max(1, seconds),
        hints,
      });
      clearSavedGame();
      onFinish(result);
    } catch (error) {
      setProblem(error instanceof Error ? error.message : 'Something went wrong. Try again.');
    }
  }

  function change(next: Cell[], saveUndo = true) {
    if (solved) return;
    if (!clock.running) clock.start();
    if (saveUndo) setHistory((past) => [...past, cells]);
    setHint(null);
    setCells(next);
    if (isSolved(size, regions, next)) {
      const seconds = clock.stop();
      setAnnouncement(`Solved in ${formatTime(seconds)}.`);
      const stamps = stampsOf(next);
      reveal.current = window.setTimeout(() => void send(stamps, seconds), REVEAL_MS);
    }
  }

  function tap(cell: number) {
    const next = [...cells];
    next[cell] = nextCell(cells[cell] ?? 'empty');
    if (next[cell] === 'stamp') navigator.vibrate?.(10);
    change(next);
  }

  function drag(cell: number, mode: 'mark' | 'clear') {
    setCells((now) => {
      const from = mode === 'mark' ? 'empty' : 'cross';
      if (now[cell] !== from || solved) return now;
      const next = [...now];
      next[cell] = mode === 'mark' ? 'cross' : 'empty';
      return next;
    });
    if (!clock.running) clock.start();
    setHint(null);
  }

  function undo() {
    const last = history.at(-1);
    if (!last) return;
    setHistory((past) => past.slice(0, -1));
    setHint(null);
    setCells(last);
  }

  function clearBoard() {
    if (!window.confirm('Clear the board? The clock keeps its time.')) return;
    change(Array.from({ length: size * size }, () => 'empty' as const));
  }

  async function askHint() {
    if (!session) return;
    setProblem(null);
    try {
      const next = await apiSend<Hint>('POST', '/api/games/stamps/hint', session.csrfToken, {
        date: puzzle.date,
        stamps: stampsOf(cells),
        crosses: crossesOf(cells),
      });
      if (!clock.running) clock.start();
      setHints((count) => count + 1);
      setHint(next);
      setAnnouncement(next.reason);
    } catch (error) {
      setProblem(error instanceof Error ? error.message : 'Something went wrong. Try again.');
    }
  }

  function switchAutoCross(on: boolean) {
    setAutoCross(on);
    saveAutoCross(on);
  }

  const hintLine =
    hint && hint.cell !== null
      ? `${hint.wrong ? 'Look again: ' : hint.stamp ? 'Stamp here: ' : '✕ here: '}${hint.reason}`
      : hint?.reason;

  return (
    <section className="stamps">
      {heading}
      <p className="stamps__day">
        {puzzle.weekday} · {puzzle.size} × {puzzle.size} · {puzzle.label}
      </p>
      <p className="games-page__lead">{puzzle.rule}</p>
      {puzzle.sizeNote && <p className="games-page__lead">{puzzle.sizeNote}</p>}
      <div className="stamps__bar">
        <span className="stamps__clock" aria-label={`Time ${formatTime(clock.seconds)}`}>
          {formatTime(clock.seconds)}
        </span>
        <span className="stamps__hints">{countOf(hints, 'hint')}</span>
      </div>
      <StampsBoard
        size={size}
        regions={regions}
        photos={puzzle.photos}
        cells={cells}
        autoCrossed={ruledOut}
        conflicts={broken}
        hint={hint?.cell ?? null}
        onTap={tap}
        onDrag={drag}
        onDragStart={() => setHistory((past) => [...past, cells])}
      />
      {hintLine && <p className="stamps__hint-line">{hintLine}</p>}
      <div className="stamps__tools">
        <button
          type="button"
          className="button"
          onClick={undo}
          disabled={history.length === 0 || solved}
        >
          Undo
        </button>
        <button type="button" className="button" onClick={() => void askHint()} disabled={solved}>
          Hint
        </button>
        <button type="button" className="button" onClick={clearBoard} disabled={solved}>
          Clear board
        </button>
      </div>
      <label className="setting-row">
        <span>Mark ruled-out cells</span>
        <input
          type="checkbox"
          role="switch"
          className="switch"
          checked={autoCross}
          onChange={(event) => switchAutoCross(event.target.checked)}
        />
      </label>
      <p className="visually-hidden" role="status">
        {announcement}
      </p>
      {problem && (
        <p className="timeline__problem" role="alert">
          {problem}{' '}
          {solved && (
            <button
              type="button"
              className="button"
              onClick={() => void send(stampsOf(cells), clock.seconds)}
            >
              Try again
            </button>
          )}
        </p>
      )}
    </section>
  );
}

/** The month and year of a trip's start, such as "Apr 2025". */
function monthOf(date: string): string {
  const [year, month] = date.split('-').map(Number);
  return new Date(year ?? 2000, (month ?? 1) - 1, 1).toLocaleDateString('en-US', {
    month: 'short',
    year: 'numeric',
  });
}

/** The end of the game: the passport page, one stamped photo per trip. */
function StampsResult({ heading, result }: { heading: ReactNode; result: Result }) {
  return (
    <section className="stamps">
      {heading}
      <p className="timeline__verdict">
        Solved in {formatTime(result.seconds)} ·{' '}
        {result.hints === 0 ? 'no hints' : countOf(result.hints, 'hint')}
      </p>
      <section className="passport" aria-labelledby="passport-title">
        <h3 className="passport__title" id="passport-title">
          Passport
        </h3>
        <ol className="passport__stamps">
          {result.passport.map((stamp) => {
            const image = (
              <img
                className="passport__photo"
                src={`/api/photos/${stamp.photoId}/thumbnail`}
                alt={stamp.tripName}
              />
            );
            return (
              <li key={stamp.cell} className="passport__stamp">
                {stamp.immichUrl ? (
                  <a
                    href={stamp.immichUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    aria-label={`Open the photo from ${stamp.tripName} in Immich`}
                  >
                    {image}
                  </a>
                ) : (
                  image
                )}
                <Link className="passport__name" to={`/trips/${stamp.tripId}`}>
                  {stamp.tripName}
                </Link>
                <span className="passport__date">{monthOf(stamp.startDate)}</span>
              </li>
            );
          })}
        </ol>
      </section>
      {result.passport.some((stamp) => stamp.immichUrl) && (
        <p className="games-page__lead">Tap a photo to open it in Immich.</p>
      )}
      <GameEnd game="stamps" share={result.share} stats={result.stats} />
    </section>
  );
}
