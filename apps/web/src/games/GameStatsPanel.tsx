import type { components } from '../api/schema';
import { formatTime } from './time';

type GameStats = components['schemas']['GameStats'];

/**
 * A game's stats: played, win percentage, streaks, and how many tries each win took. The bar of
 * today's tries is highlighted. A game that is always one try, such as Slide, has no bars. A timed
 * game, such as Stamps, always ends solved, so it shows its best and average times instead of a win
 * percentage. See docs/specs/games.md#results-and-stats and docs/specs/games.md#timed-games.
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
        {!stats.times && <Tile label="Win %" value={stats.winPercent} />}
        <Tile label="Current streak" value={stats.currentStreak} />
        <Tile label="Best streak" value={stats.bestStreak} />
      </dl>
      {stats.times && stats.times.length > 0 && (
        <>
          <h3 className="game-stats__subtitle">Times</h3>
          <dl className="game-stats__times">
            {stats.times.map((time) => (
              <div key={time.label} className="game-stats__time">
                <dt>{time.label || 'All'}</dt>
                <dd>
                  best {formatTime(time.best)}, average {formatTime(time.average)}
                </dd>
              </div>
            ))}
          </dl>
        </>
      )}
      {stats.tries.length > 1 && (
        <>
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
        </>
      )}
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
