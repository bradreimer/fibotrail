import type { ReactNode } from 'react';

export interface Destination {
  path: string;
  label: string;
  icon: ReactNode;
}

/** Icons use a 2 px rounded line, per the design and branding document. */
function Icon({ children }: { children: ReactNode }) {
  return (
    <svg
      className="nav__icon"
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
 * The MVP destinations, per ADR-007. A destination is listed only when its screen exists.
 */
export const DESTINATIONS: readonly Destination[] = [
  {
    path: '/trips',
    label: 'Trips',
    icon: (
      <Icon>
        <path d="M9 4 3 6v14l6-2 6 2 6-2V4l-6 2-6-2Z" />
        <path d="M9 4v14M15 6v14" />
      </Icon>
    ),
  },
  {
    path: '/inbox',
    label: 'Inbox',
    icon: (
      <Icon>
        <path d="M3 13h5l1.5 3h5L16 13h5" />
        <path d="M5.5 5h13L21 13v5a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-5l2.5-8Z" />
      </Icon>
    ),
  },
  {
    path: '/settings',
    label: 'Settings',
    icon: (
      <Icon>
        <path d="M4 7h10M18 7h2M4 17h2M10 17h10" />
        <circle cx="16" cy="7" r="2" />
        <circle cx="8" cy="17" r="2" />
      </Icon>
    ),
  },
];

export function destinationFor(pathname: string): Destination | undefined {
  return DESTINATIONS.find(
    (destination) => pathname === destination.path || pathname.startsWith(`${destination.path}/`),
  );
}
