import { useState, type ReactNode } from 'react';
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
import { inPlace, summary } from './order';
import { TimelineBoard } from './TimelineBoard';

type Mark = components['schemas']['Mark'];
type Result = components['schemas']['TimelineResultView'];
type Puzzle = components['schemas']['TimelinePuzzleView'];
type Twist = components['schemas']['TimelineTwistView'];

const SQUARE: Record<Mark, string> = {
  right: '🟩',
  close: '🟨',
  wrong: '⬜',
  earlier: '⬆️',
  later: '⬇️',
};

/**
 * The Timeline game: put your photos in the order you took them. Each weekday has a twist, which
 * the server picks and names. The server grades every try and stores the day's result. See
 * docs/specs/timeline-game.md.
 */
export default function TimelineGame() {
  const date = localDate();
  const puzzle = useApi('/api/games/timeline/puzzle', `/api/games/timeline/puzzle?date=${date}`);
  const [finished, setFinished] = useState<Result | null>(null);

  if (puzzle.loading) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading today's puzzle</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (puzzle.error) return <ErrorState message={puzzle.error} onRetry={puzzle.reload} />;
  if (!puzzle.data) return null;

  const data = puzzle.data;
  const title = (
    <h2 className="timeline__title">
      Timeline <span className="timeline__number">#{data.number}</span>
    </h2>
  );
  const heading = (
    <>
      {title}
      <TwistLine twist={data.twist} />
    </>
  );

  if (data.tripsNeeded > 0) {
    return (
      <section className="timeline">
        {title}
        <p className="games-page__lead">
          Timeline needs 5 trips with photos. Add {countOf(data.tripsNeeded, 'more trip')}, and
          today's puzzle is ready. <Link to="/inbox">Open the inbox</Link>
        </p>
      </section>
    );
  }

  const result = finished ?? data.result;
  if (result) return <TimelineResult heading={heading} result={result} twist={data.twist} />;
  return <TimelinePlay heading={heading} puzzle={data} onFinish={setFinished} />;
}

/** The day's twist and its one-line rule, such as "Arrows: A photo out of place shows…". */
function TwistLine({ twist }: { twist: Twist }) {
  return (
    <p className="timeline__twist">
      <span className="timeline__twist-name">{twist.name}:</span> {twist.rule}
    </p>
  );
}

/** Today's puzzle while you play it. The order and the tries live here until the game ends. */
function TimelinePlay({
  heading,
  puzzle: data,
  onFinish,
}: {
  heading: ReactNode;
  puzzle: Puzzle;
  onFinish: (result: Result) => void;
}) {
  const session = useSession();
  const [order, setOrder] = useState<string[]>(() => data.cards.map((card) => card.photoId));
  const [guesses, setGuesses] = useState<string[][]>([]);
  const [rows, setRows] = useState<Mark[][]>([]);
  const [marks, setMarks] = useState<ReadonlyMap<string, Mark>>(new Map());
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [announcement, setAnnouncement] = useState('');
  const triesLeft = data.maxTries - rows.length;
  // Count only says how many cards are in place, not which, so no card gets a mark or locks.
  const countOnly = data.twist.id === 'countOnly';

  function reorder(next: string[]) {
    setOrder(next);
    // A mark belongs to a place, so moved cards lose theirs. Right cards never move.
    setMarks((before) => new Map([...before].filter(([, mark]) => mark === 'right')));
  }

  async function check() {
    if (!session) return;
    setBusy(true);
    setProblem(null);
    try {
      const answer = await apiSend<{ marks: Mark[] }>(
        'POST',
        '/api/games/timeline/check',
        session.csrfToken,
        { date: data.date, order },
      );
      const nextGuesses = [...guesses, order];
      const nextRows = [...rows, answer.marks];
      setGuesses(nextGuesses);
      setRows(nextRows);
      if (!countOnly)
        setMarks(
          new Map(order.map((id, index): [string, Mark] => [id, answer.marks[index] ?? 'wrong'])),
        );
      setAnnouncement(summary(answer.marks, countOnly));
      const solved = answer.marks.every((mark) => mark === 'right');
      if (solved || nextRows.length >= data.maxTries) {
        onFinish(
          await apiSend<Result>('POST', '/api/games/timeline/results', session.csrfToken, {
            date: data.date,
            guesses: nextGuesses,
          }),
        );
      }
    } catch (error) {
      setProblem(error instanceof Error ? error.message : 'Something went wrong. Try again.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="timeline">
      {heading}
      <p className="games-page__lead">
        Drag the photos into the order you took them, oldest first.{' '}
        {countOf(triesLeft, 'try', 'tries')} left.
      </p>
      <TimelineBoard order={order} marks={marks} disabled={busy} onChange={reorder} />
      {countOnly && rows.length > 0 && (
        <p className="timeline__count">Last check: {inPlace(rows[rows.length - 1]!)}.</p>
      )}
      {rows.length > 0 && <MarkGrid rows={rows} countOnly={countOnly} />}
      <p className="visually-hidden" role="status">
        {announcement}
      </p>
      {problem && (
        <p className="timeline__problem" role="alert">
          {problem}
        </p>
      )}
      <button
        type="button"
        className="button timeline__check"
        disabled={busy || !session}
        onClick={() => void check()}
      >
        {busy ? 'Checking…' : 'Check'}
      </button>
    </section>
  );
}

function MarkGrid({
  rows,
  countOnly = false,
}: {
  rows: readonly (readonly Mark[])[];
  countOnly?: boolean;
}) {
  return (
    <ol className="timeline__grid" aria-label="Your tries">
      {rows.map((row, index) => (
        <li key={index} aria-label={`Try ${index + 1}: ${summary(row, countOnly)}`}>
          <span aria-hidden="true">{row.map((mark) => SQUARE[mark]).join('')}</span>
        </li>
      ))}
    </ol>
  );
}

/** The end of the game: the right order, each card with its trip and a link to it. */
function TimelineResult({
  heading,
  result,
  twist,
}: {
  heading: ReactNode;
  result: Result;
  twist: Twist;
}) {
  const [shared, setShared] = useState<ShareOutcome | null>(null);
  const label =
    shared === 'copied' ? 'Copied' : shared === 'failed' ? "Couldn't share. Try again." : 'Share';
  return (
    <section className="timeline">
      {heading}
      <p className="timeline__verdict">
        {result.solved
          ? `Solved in ${countOf(result.tries, 'try', 'tries')}.`
          : 'Not this time. Here is the order you took them.'}
      </p>
      <MarkGrid rows={result.rows} countOnly={twist.id === 'countOnly'} />
      <ol className="timeline__answer">
        {result.answer.map((card) => (
          <li key={card.photoId}>
            <Link className="timeline-answer" to={`/trips/${card.tripId}`}>
              <img
                className="timeline-answer__photo"
                src={`/api/photos/${card.photoId}/thumbnail`}
                alt=""
                loading="lazy"
              />
              <span className="timeline-answer__name">{card.tripName}</span>
              <span className="timeline-answer__date">{formatDateRange(card.date, card.date)}</span>
            </Link>
          </li>
        ))}
      </ol>
      <button
        type="button"
        className="button timeline__check"
        onClick={() => void shareResult(result.share).then(setShared)}
      >
        {label}
      </button>
      <GameStatsPanel stats={result.stats} today={result.solved ? result.tries : undefined} />
      <p className="games-page__lead">A new puzzle comes tomorrow.</p>
    </section>
  );
}
