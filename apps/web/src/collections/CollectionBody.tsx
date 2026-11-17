import { Link } from 'react-router-dom';

import { useApi } from '../api/useApi';
import type { components } from '../api/schema';
import { ErrorState } from '../states/ErrorState';
import { countOf } from '../trips/dates';
import { tripsText } from '../screens/CollectionScreen';

type Detail = components['schemas']['CollectionDetail'];

/** One card for each resort, most trips first and then by name, with the note about shared trips. */
export function ResortList({ id, detail }: { id: string; detail: Detail }) {
  const base = `/collections/${encodeURIComponent(id)}`;
  const resorts = [...detail.resorts].sort(
    (a, b) => b.trips - a.trips || a.name.localeCompare(b.name),
  );
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
              <span className="collection-card__count">{tripsText(resort.trips)}</span>
            </Link>
          </li>
        ))}
      </ul>
      {resorts.length > 1 && (
        <p className="stats-page__note">A trip to two resorts counts once for each.</p>
      )}
    </>
  );
}

/** What an open collection shows: its totals, its resorts, and the way to the stamp book. */
export function CollectionBody({ id }: { id: string }) {
  const collection = useApi('/api/collections/{id}', `/api/collections/${encodeURIComponent(id)}`);

  if (collection.loading && !collection.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading the collection</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (collection.error)
    return <ErrorState message={collection.error} onRetry={collection.reload} />;
  const detail = collection.data;
  if (!detail) return null;

  const { summary } = detail;
  return (
    <div className="collection-body">
      <p className="stats-page__lead">
        {summary.visited} / {summary.total} parks · {countOf(summary.totalDays, 'day')} ·{' '}
        {countOf(summary.trips, 'trip')}
      </p>
      <ResortList id={id} detail={detail} />
      <p>
        <Link className="button" to={`/collections/${encodeURIComponent(id)}/stamps`}>
          Stamp book
        </Link>
      </p>
    </div>
  );
}
