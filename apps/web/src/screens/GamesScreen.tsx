import { useApi } from '../api/useApi';
import { GameCard } from '../games/GameCard';
import { GAMES } from '../games/registry';
import { localDate } from '../games/today';
import { ErrorState } from '../states/ErrorState';

/**
 * The Games screen: one card for each game that both the server and the web registry list. See
 * docs/specs/games.md.
 */
export function GamesScreen() {
  const games = useApi('/api/games', `/api/games?date=${localDate()}`);

  if (games.loading) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading your games</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (games.error) return <ErrorState message={games.error} onRetry={games.reload} />;
  if (!games.data) return null;

  const cards = GAMES.flatMap((game) => {
    const entry = games.data!.find((item) => item.id === game.id);
    return entry ? [{ game, entry }] : [];
  });
  return (
    <div className="games-page">
      <p className="games-page__lead">A new puzzle each day, made from your trips.</p>
      <ul className="games-list">
        {cards.map(({ game, entry }) => (
          <li key={game.id}>
            <GameCard game={game} entry={entry} />
          </li>
        ))}
      </ul>
    </div>
  );
}
