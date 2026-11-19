import { Link } from 'react-router-dom';

import type { components } from '../api/schema';
import type { GameModule } from './registry';

type GameEntry = components['schemas']['GameEntry'];

/** Today's state of a game, in a few words. */
export function todayLine(entry: GameEntry): string {
  if (!entry.playedToday) return "Today's puzzle is ready";
  return entry.solvedToday ? 'Solved today' : 'Played today';
}

/** A card on the Games screen: the game, today's state, and the current streak. */
export function GameCard({ game, entry }: { game: GameModule; entry: GameEntry }) {
  const streak = entry.stats.currentStreak;
  return (
    <Link className="game-card" to={`/games/${game.id}`}>
      {game.icon}
      <span className="game-card__name">{entry.name}</span>
      <span className="game-card__description">{entry.description}</span>
      <span className="game-card__state">
        {todayLine(entry)}
        {streak > 0 && ` · ${streak}-day streak`}
      </span>
    </Link>
  );
}
