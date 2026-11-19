import { Link } from 'react-router-dom';

import { useApi } from '../api/useApi';
import type { components } from '../api/schema';
import { ErrorState } from '../states/ErrorState';
import { countOf } from '../trips/dates';

type Detail = components['schemas']['CollectionDetail'];

/**
 * One card for each resort you've been to, most trips first and then by name, with the note about
 * shared trips. A resort with no trips isn't shown, so the list is where you've been, not what's left.
 */
export function ResortList({ id, detail }: { id: string; detail: Detail }) {
  const base = `/insights/${encodeURIComponent(id)}`;
  const resorts = detail.resorts
    .filter((resort) => resort.trips > 0)
    .sort((a, b) => b.trips - a.trips || a.name.localeCompare(b.name));
  return (
    <>
      <ul className="collections">
        {resorts.map((resort) => (
          <li key={resort.name}>
            <Link
              className="collection-card"
              to={`${base}/resorts/${encodeURIComponent(resort.name)}`}
            >
              <span className="collection-card__name">{resort.name}</span>
              <span className="collection-card__count">{countOf(resort.trips, 'trip')}</span>
            </Link>
          </li>
        ))}
      </ul>
      {resorts.length > 1 && (
        <p className="insights-page__note">A trip to two resorts counts once for each.</p>
      )}
    </>
  );
}

/** The loading state of an open collection: the shape of its summary line, resorts, and button. */
export function CollectionBodySkeleton() {
  return (
    <div className="collection-body" role="status" aria-busy="true">
      <span className="visually-hidden">Loading the collection</span>
      <span className="skeleton skeleton--text skeleton--lead" aria-hidden="true" />
      <div className="collections" aria-hidden="true">
        {[0, 1, 2].map((row) => (
          <span key={row} className="skeleton skeleton--row" />
        ))}
      </div>
      <span className="skeleton skeleton--button" aria-hidden="true" />
    </div>
  );
}

/** What an open collection shows: its totals, its resorts, and the way to the stamp book. */
export function CollectionBody({ id }: { id: string }) {
  const collection = useApi('/api/collections/{id}', `/api/collections/${encodeURIComponent(id)}`);

  if (collection.loading && !collection.data) return <CollectionBodySkeleton />;
  if (collection.error)
    return <ErrorState message={collection.error} onRetry={collection.reload} />;
  const detail = collection.data;
  if (!detail) return null;

  const { summary } = detail;
  return (
    <div className="collection-body">
      <p className="insights-page__lead">
        {countOf(summary.visited, 'park')} · {countOf(summary.totalDays, 'day')} ·{' '}
        {countOf(summary.trips, 'trip')}
      </p>
      <ResortList id={id} detail={detail} />
      <p>
        <Link className="button" to={`/insights/${encodeURIComponent(id)}/stamps`}>
          Stamp book
        </Link>
      </p>
    </div>
  );
}
