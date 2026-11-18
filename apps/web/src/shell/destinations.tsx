import type { ReactNode } from 'react';

export interface Destination {
  path: string;
  label: string;
  icon: ReactNode;
  /**
   * "main" destinations are always in the navigation. "tool" destinations, the Inbox and Settings,
   * move to the top bar when the navigation is a tab bar, and stay in the sidebar on wider screens.
   */
  placement: 'main' | 'tool';
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
 * The destinations, per ADR-007. The tab bar holds Trips, Map, and Insights. Insights replaced the
 * Collections and Stats tabs, per ADR-019.
 */
export const DESTINATIONS: readonly Destination[] = [
  {
    path: '/trips',
    label: 'Trips',
    placement: 'main',
    icon: (
      <Icon>
        <path d="M9 4 3 6v14l6-2 6 2 6-2V4l-6 2-6-2Z" />
        <path d="M9 4v14M15 6v14" />
      </Icon>
    ),
  },
  {
    path: '/map',
    label: 'Map',
    placement: 'main',
    icon: (
      <Icon>
        <circle cx="12" cy="12" r="9" />
        <path d="M3 12h18M12 3c2.5 2.5 3.5 5.5 3.5 9S14.5 18.5 12 21c-2.5-2.5-3.5-5.5-3.5-9S9.5 5.5 12 3Z" />
      </Icon>
    ),
  },
  {
    path: '/insights',
    label: 'Insights',
    placement: 'main',
    icon: (
      <Icon>
        <path d="M4 20V10M10 20V4M16 20v-7M22 20H2" />
      </Icon>
    ),
  },
  {
    path: '/inbox',
    label: 'Inbox',
    placement: 'tool',
    icon: (
      <Icon>
        <path d="M3 13h5l1.5 3h5L16 13h5" />
        <path d="M5.5 5h13L21 13v5a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-5l2.5-8Z" />
      </Icon>
    ),
  },
  {
    path: '/mcp-setup',
    label: 'MCP',
    placement: 'tool',
    icon: (
      <Icon>
        <path d="M9 3v5M15 3v5M6 8h12v3a6 6 0 0 1-12 0V8ZM12 17v4" />
      </Icon>
    ),
  },
  {
    path: '/settings',
    label: 'Settings',
    placement: 'tool',
    icon: (
      <Icon>
        <path d="M4 7h10M18 7h2M4 17h2M10 17h10" />
        <circle cx="16" cy="7" r="2" />
        <circle cx="8" cy="17" r="2" />
      </Icon>
    ),
  },
];

/** The destinations in the tab bar, and in the sidebar. In the tab bar the tools are in the top bar. */
export function navigationDestinations(tabs: boolean): readonly Destination[] {
  return tabs
    ? DESTINATIONS.filter((destination) => destination.placement === 'main')
    : DESTINATIONS;
}

/** The Inbox, MCP, and Settings, for the top bar of a phone. */
export const TOOLS: readonly Destination[] = DESTINATIONS.filter(
  (destination) => destination.placement === 'tool',
);

export function destinationFor(pathname: string): Destination | undefined {
  return DESTINATIONS.find(
    (destination) => pathname === destination.path || pathname.startsWith(`${destination.path}/`),
  );
}
