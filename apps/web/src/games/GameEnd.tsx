import { useState } from 'react';
import { Link } from 'react-router-dom';

import type { components } from '../api/schema';
import { useApi } from '../api/useApi';
import { GameStatsPanel } from './GameStatsPanel';
import { GAMES } from './registry';
import { shareResult, type ShareOutcome } from './share';
import { localDate } from './today';

type GameEntry = components['schemas']['GameEntry'];
type GameStats = components['schemas']['GameStats'];

/**
 * The next game to play today: the first in Games screen order that both registries list, isn't
 * played today, and isn't the game you just finished. Null when none is left.
 */
export function nextGame(current: string, entries: readonly GameEntry[]): GameEntry | null {
  for (const game of GAMES) {
    if (game.id === current) continue;
    const entry = entries.find((item) => item.id === game.id);
    if (entry && !entry.playedToday) return entry;
  }
  return null;
}

/**
 * The end of every game, under its own verdict and photos: Share, the stats, and a link to the
 * next game you haven't played today, or back to Games. Games use this shared component, never
 * each other, which keeps to ADR-022. See docs/specs/daily-set.md#the-shared-end-screen.
 */
export function GameEnd({
  game,
  share,
  stats,
  today,
}: {
  /** The ID of the game that just ended. */
  game: string;
  share: string;
  stats: GameStats;
  /** Today's tries, to highlight its bar in the stats. */
  today?: number;
}) {
  const [shared, setShared] = useState<ShareOutcome | null>(null);
  const games = useApi('/api/games', `/api/games?date=${localDate()}`);
  const label =
    shared === 'copied' ? 'Copied' : shared === 'failed' ? "Couldn't share. Try again." : 'Share';
  const next = games.data ? nextGame(game, games.data) : null;
  return (
    <>
      <button
        type="button"
        className="button timeline__check"
        onClick={() => void shareResult(share).then(setShared)}
      >
        {label}
      </button>
      <GameStatsPanel stats={stats} today={today} />
      {!games.loading &&
        (next ? (
          <Link className="button game-end__next" to={`/games/${next.id}`}>
            Next game: {next.name} →
          </Link>
        ) : (
          <Link className="button game-end__next" to="/games">
            Back to Games
          </Link>
        ))}
    </>
  );
}
