import { lazy, type ComponentType, type LazyExoticComponent, type ReactNode } from 'react';

/**
 * A game in the web app. The name and description come from the server's `GET /api/games`, so a
 * game shows only when both sides list it. See docs/specs/games.md and ADR-022.
 */
export interface GameModule {
  /** The game's ID, the same as on the server. It's the last part of its address, `/games/<id>`. */
  id: string;
  icon: ReactNode;
  /** The game, loaded on first use, so a removed game leaves nothing in the bundle. */
  Component: LazyExoticComponent<ComponentType>;
}

/** Icons use a 2 px rounded line, per the design and branding document. */
function Icon({ children }: { children: ReactNode }) {
  return (
    <svg
      className="game-card__icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {children}
    </svg>
  );
}

/**
 * The games, in the order the Games screen shows them. This file is the only one that imports a
 * game's folder; a test enforces it. Adding or removing a game is one entry here and one in the
 * server's `GameRegistry`.
 */
export const GAMES: readonly GameModule[] = [
  {
    id: 'timeline',
    icon: (
      <Icon>
        <path d="M3 12h18" />
        <circle cx="6" cy="12" r="2" />
        <circle cx="12" cy="12" r="2" />
        <circle cx="18" cy="12" r="2" />
        <path d="M6 5v3M12 16v3M18 5v3" />
      </Icon>
    ),
    Component: lazy(() => import('./timeline/TimelineGame')),
  },
  {
    id: 'slide',
    icon: (
      <Icon>
        <rect x="3" y="3" width="18" height="18" rx="2" />
        <path d="M9 3v18M15 3v12M3 9h18M3 15h12" />
      </Icon>
    ),
    Component: lazy(() => import('./slide/SlideGame')),
  },
  {
    id: 'pin',
    icon: (
      <Icon>
        <path d="M12 21s-6-5.6-6-11a6 6 0 0 1 12 0c0 5.4-6 11-6 11z" />
        <circle cx="12" cy="10" r="2" />
      </Icon>
    ),
    Component: lazy(() => import('./pin/PinGame')),
  },
  {
    id: 'fetch',
    icon: (
      <Icon>
        <path d="M3 17c3-6 6-9 9-9s5 2 9-3" />
        <circle cx="7" cy="7" r="2" />
        <circle cx="17" cy="15" r="2" />
      </Icon>
    ),
    Component: lazy(() => import('./fetch/FetchGame')),
  },
  {
    id: 'stamps',
    icon: (
      <Icon>
        <rect x="3" y="3" width="18" height="18" rx="2" />
        <path d="M9 3v6H3M15 21v-6h6" />
        <circle cx="15.5" cy="8.5" r="2.5" />
      </Icon>
    ),
    Component: lazy(() => import('./stamps/StampsGame')),
  },
];

export function gameFor(id: string | undefined): GameModule | undefined {
  return GAMES.find((game) => game.id === id);
}
