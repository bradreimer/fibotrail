import { Link, useParams } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { countOf } from '../trips/dates';

/** "3 trips", or "0 trips · Not visited yet", so a place with no trips says so in words. */
export function tripsText(trips: number): string {
  return trips === 0 ? '0 trips · Not visited yet' : countOf(trips, 'trip');
}

/** A collection, level 2: its trips, then one row for each resort. The stamp book is one tap deeper. */
export function CollectionScreen() {
  const { id = '' } = useParams();
  const collection = useApi('/api/collections/{id}', `/api/collections/${encodeURIComponent(id)}`);

  if (collection.loading && !collection.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading the collection</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (collection.errorStatus === 404) {
    return (
      <EmptyState
        title="No such collection"
        message="That collection doesn't exist."
        action={
          <Link className="button" to="/collections">
            Back to Collections
          </Link>
        }
      />
    );
  }
  if (collection.error)
    return <ErrorState message={collection.error} onRetry={collection.reload} />;
  const data = collection.data;
  if (!data) return null;

  const base = `/collections/${encodeURIComponent(id)}`;
  const resorts = [...data.resorts].sort(
    (a, b) => b.trips - a.trips || a.name.localeCompare(b.name),
  );
  return (
    <div className="stats-screen">
      <p>
        <Link to="/collections">Collections</Link>
      </p>
      <h2 className="stats-page__title">{data.summary.name}</h2>
      <p className="stats-page__lead">{tripsText(data.summary.trips)}</p>
      {data.summary.trips === 0 && <p>Add a trip to start your stamp book.</p>}
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
      <p>
        <Link className="button" to={`${base}/stamps`}>
          Stamp book
        </Link>
      </p>
      <p className="stats-page__note">{data.attribution}</p>
    </div>
  );
}
