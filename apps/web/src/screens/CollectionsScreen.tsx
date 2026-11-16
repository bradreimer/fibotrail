import { Link } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { ProgressRing } from '../stats/ProgressRing';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { countOf } from '../trips/dates';

/** The collections: one card for each, with the parks visited and the days across them. */
export function CollectionsScreen() {
  const collections = useApi('/api/collections');

  if (collections.loading && !collections.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading your collections</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (collections.error)
    return <ErrorState message={collections.error} onRetry={collections.reload} />;
  if (!collections.data) return null;

  if (collections.data.length === 0) {
    return (
      <EmptyState
        title="No collections yet"
        message="No collections yet. A collection is a data file in the collections folder."
      />
    );
  }

  const noTrips = collections.data.every((item) => item.visited === 0);
  return (
    <div className="stats-screen">
      {noTrips && <p className="stats-page__lead">Add a trip to start your stamp book.</p>}
      <ul className="collections">
        {collections.data.map((item) => (
          <li key={item.id}>
            <Link className="collection-card" to={`/collections/${item.id}`}>
              <ProgressRing visited={item.visited} total={item.total} />
              <span className="collection-card__name">{item.name}</span>
              <span className="collection-card__count">
                {item.visited} / {item.total} · {countOf(item.totalDays, 'day')} across{' '}
                {countOf(item.visited, 'park')}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
