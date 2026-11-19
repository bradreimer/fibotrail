import { Suspense } from 'react';
import { Navigate, useParams } from 'react-router-dom';

import { gameFor } from '../games/registry';
import { BackLink } from '../shell/BackLink';

/** One game, at /games/<id>. An unknown game opens the Games screen. See docs/specs/games.md. */
export function GameScreen() {
  const { id } = useParams();
  const game = gameFor(id);
  if (!game) return <Navigate to="/games" replace />;

  return (
    <div className="games-page">
      <BackLink to="/games">Games</BackLink>
      <Suspense
        fallback={
          <div role="status" aria-busy="true">
            <span className="visually-hidden">Loading the game</span>
            <span className="skeleton skeleton--card" aria-hidden="true" />
          </div>
        }
      >
        <game.Component />
      </Suspense>
    </div>
  );
}
