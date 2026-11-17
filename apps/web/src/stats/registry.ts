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
    id: 'trip-spiral',
    slot: 'stats',
    title: 'Trip spiral',
    component: lazy(() => import('./views/trip-spiral')),
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
