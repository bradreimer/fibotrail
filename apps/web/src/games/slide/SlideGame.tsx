import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';

import { apiSend } from '../../api/client';
import type { components } from '../../api/schema';
import { useSession } from '../../api/session';
import { useApi } from '../../api/useApi';
import { ErrorState } from '../../states/ErrorState';
import { countOf, formatDateRange } from '../../trips/dates';
import { GameStatsPanel } from '../GameStatsPanel';
import { shareResult, type ShareOutcome } from '../share';
import { localDate } from '../today';
import { isSolved, moveAll } from './board';
import { SlideBoard } from './SlideBoard';

type Puzzle = components['schemas']['SlidePuzzleView'];
type Result = components['schemas']['SlideResultView'];

/** How long the whole photo shows on the board before the caption opens. */
export const REVEAL_MS = 900;

/**
 * The Slide game: one of your trip photos, cut into 9 tiles with the bottom-right one removed and
 * the rest shuffled. Slide them back. When the photo is whole, it shows with its trip's name,
 * dates, and place. The server replays the moves and stores the day's result. See
 * docs/specs/slide-game.md.
 */
export default function SlideGame() {
  const date = localDate();
  const puzzle = useApi('/api/games/slide/puzzle', `/api/games/slide/puzzle?date=${date}`);
  const [finished, setFinished] = useState<Result | null>(null);

  if (puzzle.loading) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading today's puzzle</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (puzzle.error) return <ErrorState message={puzzle.error} onRetry={puzzle.reload} />;
  if (!puzzle.data) return null;

  const data = puzzle.data;
  const heading = (
    <h2 className="timeline__title">
      Slide <span className="timeline__number">#{data.number}</span>
    </h2>
  );

  if (data.tripsNeeded > 0 || !data.photoId) {
    return (
      <section className="slide">
        {heading}
        <p className="games-page__lead">
          Slide needs a trip with photos. Add one, and today's puzzle is ready.{' '}
          <Link to="/inbox">Open the inbox</Link>
        </p>
      </section>
    );
  }

  const result = finished ?? data.result;
  if (result) return <SlideResult heading={heading} result={result} />;
  return (
    <SlidePlay heading={heading} puzzle={data} photoId={data.photoId} onFinish={setFinished} />
  );
}

/** Today's puzzle while you play it. The board and the moves live here until it's solved. */
function SlidePlay({
  heading,
  puzzle,
  photoId,
  onFinish,
}: {
  heading: ReactNode;
  puzzle: Puzzle;
  photoId: string;
  onFinish: (result: Result) => void;
}) {
  const session = useSession();
  const [board, setBoard] = useState<number[]>(() => [...puzzle.tiles]);
  const [moves, setMoves] = useState<number[]>([]);
  const [problem, setProblem] = useState<string | null>(null);
  const [announcement, setAnnouncement] = useState('');
  const solved = isSolved(board);
  const reveal = useRef<number | undefined>(undefined);

  useEffect(() => () => window.clearTimeout(reveal.current), []);

  async function send(all: number[]) {
    if (!session) return;
    setProblem(null);
    try {
      onFinish(
        await apiSend<Result>('POST', '/api/games/slide/results', session.csrfToken, {
          date: puzzle.date,
          moves: all,
        }),
      );
    } catch (error) {
      setProblem(error instanceof Error ? error.message : 'Something went wrong. Try again.');
    }
  }

  function slide(tiles: number[]) {
    const next = moveAll(board, tiles);
    const all = [...moves, ...tiles];
    setBoard(next);
    setMoves(all);
    if (isSolved(next)) {
      setAnnouncement('Solved. The photo is whole.');
      // The whole photo shows on the board for a moment, then the result opens.
      reveal.current = window.setTimeout(() => void send(all), REVEAL_MS);
    } else {
      setAnnouncement(`Moved ${tiles.join(', ')}.`);
    }
  }

  return (
    <section className="slide">
      {heading}
      <p className="games-page__lead">
        Slide the tiles into the empty space until the photo is whole.
      </p>
      <SlideBoard board={board} photoId={photoId} solved={solved} onSlide={slide} />
      <p className="slide__moves" aria-hidden="true">
        {countOf(moves.length, 'move')}
      </p>
      <p className="visually-hidden" role="status">
        {announcement}
      </p>
      {problem && (
        <p className="timeline__problem" role="alert">
          {problem}{' '}
          <button type="button" className="button" onClick={() => void send(moves)}>
            Try again
          </button>
        </p>
      )}
    </section>
  );
}

/** The end of the game: the whole photo, with its trip's name, dates, and place. */
function SlideResult({ heading, result }: { heading: ReactNode; result: Result }) {
  const [shared, setShared] = useState<ShareOutcome | null>(null);
  const label =
    shared === 'copied' ? 'Copied' : shared === 'failed' ? "Couldn't share. Try again." : 'Share';
  const { answer } = result;
  return (
    <section className="slide">
      {heading}
      <p className="timeline__verdict">Solved in {countOf(result.moves, 'move')}.</p>
      <figure className="slide-photo">
        <img
          className="slide-photo__image"
          src={`/api/photos/${answer.photoId}/preview`}
          alt={answer.tripName}
        />
        <figcaption className="slide-photo__caption">
          <Link className="slide-photo__name" to={`/trips/${answer.tripId}`}>
            {answer.tripName}
          </Link>
          <span className="slide-photo__detail">
            {formatDateRange(answer.startDate, answer.endDate)}
            {answer.place && ` · ${answer.place}`}
          </span>
        </figcaption>
      </figure>
      <button
        type="button"
        className="button timeline__check"
        onClick={() => void shareResult(result.share).then(setShared)}
      >
        {label}
      </button>
      <GameStatsPanel stats={result.stats} />
      <p className="games-page__lead">A new puzzle comes tomorrow.</p>
    </section>
  );
}
