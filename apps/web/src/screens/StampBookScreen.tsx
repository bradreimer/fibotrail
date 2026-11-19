import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { type CollectionPlace, PlaceSheet } from '../insights/PlaceSheet';
import { BackLink } from '../shell/BackLink';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { countOf, formatDateRange } from '../trips/dates';

/** A place you've been to: open, with at least one day or trip. Planned and unvisited places aren't shown. */
function isVisited(place: CollectionPlace): boolean {
  return place.status !== 'planned' && (place.days > 0 || place.trips > 0);
}

/** "Last visit Mar 10, 2024", or an empty string when the date isn't known. */
function lastVisitText(place: CollectionPlace): string {
  return place.lastVisit ? `Last visit ${formatDateRange(place.lastVisit, place.lastVisit)}` : '';
}

function labelOf(place: CollectionPlace): string {
  return [
    place.name,
    countOf(place.days, 'day'),
    countOf(place.trips, 'trip'),
    lastVisitText(place),
  ]
    .filter(Boolean)
    .join(', ');
}

/**
 * A collection's stamp book: where you've been. One stamp for each park you visited, grouped by
 * resort, with its days, trips, and last visit. It shows no total and no parks left to visit.
 */
export function StampBookScreen() {
  const { id = '' } = useParams();
  const collection = useApi('/api/collections/{id}', `/api/collections/${encodeURIComponent(id)}`);
  const [open, setOpen] = useState<string | null>(null);

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
          <Link className="button" to="/insights">
            Back to Insights
          </Link>
        }
      />
    );
  }
  if (collection.error)
    return <ErrorState message={collection.error} onRetry={collection.reload} />;
  const data = collection.data;
  if (!data) return null;

  const visited = data.places.filter(isVisited);
  const groups = [...new Set(visited.map((place) => place.group))];
  const sheet = visited.find((place) => place.id === open) ?? null;
  return (
    <div className="insights-page">
      <BackLink to={`/insights/${encodeURIComponent(id)}`}>{data.summary.name}</BackLink>
      <h2 className="insights-page__title">Stamp book</h2>
      {visited.length > 0 ? (
        <p className="insights-page__lead">
          {countOf(data.summary.totalDays, 'day')} across {countOf(visited.length, 'park')}
        </p>
      ) : (
        <p>Add a trip to start your stamp book.</p>
      )}
      {groups.map((group) => (
        <section key={group} aria-label={group || 'Parks'}>
          <h3 className="insights-section__title">{group}</h3>
          <ul className="stamps">
            {visited
              .filter((place) => place.group === group)
              .map((place) => (
                <li key={place.id}>
                  <button
                    type="button"
                    className="stamp"
                    aria-label={labelOf(place)}
                    onClick={() => setOpen(place.id)}
                  >
                    <span className="stamp__name" aria-hidden="true">
                      {place.name}
                    </span>
                    <span className="stamp__detail" aria-hidden="true">
                      {countOf(place.days, 'day')} · {countOf(place.trips, 'trip')}
                    </span>
                    {place.lastVisit && (
                      <span className="stamp__detail" aria-hidden="true">
                        {lastVisitText(place)}
                      </span>
                    )}
                  </button>
                </li>
              ))}
          </ul>
        </section>
      ))}
      <p className="insights-page__note">{data.attribution}</p>
      {sheet && <PlaceSheet place={sheet} onClose={() => setOpen(null)} />}
    </div>
  );
}
