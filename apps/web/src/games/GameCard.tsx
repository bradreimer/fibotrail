import { Link } from 'react-router-dom';

import type { components } from '../api/schema';
import type { GameModule } from './registry';
import { formatTime } from './time';

type GameEntry = components['schemas']['GameEntry'];

/** Today's state of a game, in a few words. */
export function todayLine(entry: GameEntry): string {
  if (!entry.playedToday) return "Today's puzzle is ready";
  return entry.solvedToday ? 'Solved today' : 'Played today';
}

/** The best time of a timed game over every label, or null when it has none. */
export function bestTime(entry: GameEntry): number | null {
  const times = entry.stats.times ?? [];
  return times.length === 0 ? null : Math.min(...times.map((time) => time.best));
}

/** A card on the Games screen: the game, today's state, the current streak, and a best time. */
export function GameCard({ game, entry }: { game: GameModule; entry: GameEntry }) {
  const streak = entry.stats.currentStreak;
  const best = bestTime(entry);
  return (
    <Link className="game-card" to={`/games/${game.id}`}>
      {game.icon}
      <span className="game-card__name">{entry.name}</span>
      <span className="game-card__description">{entry.description}</span>
      <span className="game-card__state">
        {todayLine(entry)}
        {streak > 0 && ` · ${streak}-day streak`}
        {best !== null && ` · best ${formatTime(best)}`}
      </span>
    </Link>
  );
}
