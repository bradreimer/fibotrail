import { GAMES } from '../games/registry';

const stats = { played: 1, winPercent: 100, currentStreak: 1, bestStreak: 1, tries: [1] };

/**
 * The answer of `GET /api/games` with every game in the web registry, and the listed ones played
 * today. For tests of the next-game link at the end of a game.
 */
export function gamesList(played: readonly string[]) {
  return GAMES.map((game) => ({
    id: game.id,
    name: game.id[0]!.toUpperCase() + game.id.slice(1),
    description: 'A daily game.',
    playedToday: played.includes(game.id),
    solvedToday: played.includes(game.id) ? true : null,
    stats,
    share: null,
  }));
}
