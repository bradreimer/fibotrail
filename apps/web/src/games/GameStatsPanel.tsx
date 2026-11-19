import type { components } from '../api/schema';

type GameStats = components['schemas']['GameStats'];

/**
 * A game's stats: played, win percentage, streaks, and how many tries each win took. The bar of
 * today's tries is highlighted. See docs/specs/games.md#results-and-stats.
 */
export function GameStatsPanel({ stats, today }: { stats: GameStats; today?: number }) {
  const most = Math.max(1, ...stats.tries);
  return (
    <section className="game-stats" aria-labelledby="game-stats-title">
      <h2 className="game-stats__title" id="game-stats-title">
        Stats
      </h2>
      <dl className="game-stats__tiles">
        <Tile label="Played" value={stats.played} />
        <Tile label="Win %" value={stats.winPercent} />
        <Tile label="Current streak" value={stats.currentStreak} />
        <Tile label="Best streak" value={stats.bestStreak} />
      </dl>
      <h3 className="game-stats__subtitle">Tries to win</h3>
      <ol className="game-stats__bars">
        {stats.tries.map((count, index) => (
          <li key={index} className="game-stats__row">
            <span className="game-stats__tries">{index + 1}</span>
            <span
              className={`game-stats__bar${today === index + 1 ? ' game-stats__bar--today' : ''}`}
              style={{ inlineSize: `${Math.max(8, (count / most) * 100)}%` }}
              aria-label={`${index + 1} ${index === 0 ? 'try' : 'tries'}: ${count}`}
            >
              {count}
            </span>
          </li>
        ))}
      </ol>
    </section>
  );
}

function Tile({ label, value }: { label: string; value: number }) {
  return (
    <div className="game-stats__tile">
      <dt className="game-stats__label">{label}</dt>
      <dd className="game-stats__value">{value}</dd>
    </div>
  );
}
