import { useSyncExternalStore } from 'react';

/** "tabs" is the bottom tab bar. "rail" is a sidebar of icons. "sidebar" adds the labels. */
export type NavigationMode = 'tabs' | 'rail' | 'sidebar';

export const RAIL_MIN_WIDTH = 600;
export const SIDEBAR_MIN_WIDTH = 1024;

const RAIL_QUERY = `(min-width: ${RAIL_MIN_WIDTH}px)`;
const SIDEBAR_QUERY = `(min-width: ${SIDEBAR_MIN_WIDTH}px)`;

/** The navigation for a window width, per the app shell spec. */
export function navigationModeForWidth(width: number): NavigationMode {
  if (width >= SIDEBAR_MIN_WIDTH) return 'sidebar';
  return width >= RAIL_MIN_WIDTH ? 'rail' : 'tabs';
}

function read(): NavigationMode {
  if (window.matchMedia(SIDEBAR_QUERY).matches) return 'sidebar';
  return window.matchMedia(RAIL_QUERY).matches ? 'rail' : 'tabs';
}

function subscribe(onChange: () => void): () => void {
  const queries = [window.matchMedia(RAIL_QUERY), window.matchMedia(SIDEBAR_QUERY)];
  queries.forEach((query) => query.addEventListener('change', onChange));
  return () => queries.forEach((query) => query.removeEventListener('change', onChange));
}

/** Follows the window width, so the navigation changes when an iPad rotates. */
export function useNavigationMode(): NavigationMode {
  return useSyncExternalStore(subscribe, read);
}
