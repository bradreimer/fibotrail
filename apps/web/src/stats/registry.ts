import { type ComponentType, type LazyExoticComponent, lazy } from 'react';

/** Where a view appears: on the Stats screen, or on a country page. */
export type Slot = 'stats' | 'country';

export interface ViewProps {
  /** The country's key. Only a view in the "country" slot receives it. */
  countryId?: string;
}

export interface StatsView {
  id: string;
  slot: Slot;
  /** The heading the host shows above the view. */
  title: string;
  component: LazyExoticComponent<ComponentType<ViewProps>>;
}

/**
 * The one list of views, per ADR-014. The host screens render whatever is listed here, in this
 * order, and nothing else imports a view's folder. Removing a view is deleting its line.
 */
export const VIEWS: readonly StatsView[] = [
  {
    id: 'trips-per-year',
    slot: 'stats',
    title: 'Trips per year',
    component: lazy(() => import('./views/trips-per-year')),
  },
  {
    id: 'trip-length',
    slot: 'stats',
    title: 'Trip length',
    component: lazy(() => import('./views/trip-length')),
  },
  {
    id: 'travel-clock',
    slot: 'stats',
    title: 'Travel clock',
    component: lazy(() => import('./views/travel-clock')),
  },
  {
    id: 'distance-rings',
    slot: 'stats',
    title: 'Distance rings',
    component: lazy(() => import('./views/distance-rings')),
  },
  {
    id: 'season-wheel',
    slot: 'country',
    title: 'Seasons',
    component: lazy(() => import('./views/season-wheel')),
  },
  {
    id: 'records',
    slot: 'stats',
    title: 'Records',
    component: lazy(() => import('./views/records')),
  },
];

export function viewsFor(slot: Slot, views: readonly StatsView[] = VIEWS): StatsView[] {
  return views.filter((view) => view.slot === slot);
}
