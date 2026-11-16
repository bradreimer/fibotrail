import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { type CollectionPlace, PlaceSheet } from '../collections/PlaceSheet';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { countOf } from '../trips/dates';

/** A short word for a stamp's state, so the state never rests on color alone. */
function stateOf(place: CollectionPlace): 'visited' | 'not-visited' | 'planned' {
  if (place.status === 'planned') return 'planned';
  return place.days > 0 ? 'visited' : 'not-visited';
}

function labelOf(place: CollectionPlace): string {
  switch (stateOf(place)) {
    case 'visited':
      return `${place.name}, ${countOf(place.days, 'day')}, ${countOf(place.trips, 'trip')}`;
    case 'planned':
      return `${place.name}, coming`;
    default:
      return `${place.name}, not visited yet`;
  }
}

/** A collection's stamp book: a grid of stamps, grouped by resort. */
export function CollectionScreen() {
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

  const groups = [...new Set(data.places.map((place) => place.group))];
  const sheet = data.places.find((place) => place.id === open) ?? null;
  return (
    <div className="stats-screen">
      <p>
        <Link to="/collections">Collections</Link>
      </p>
      <h2 className="stats-page__title">{data.summary.name}</h2>
      <p className="stats-page__lead">
        {data.summary.visited} / {data.summary.total} visited ·{' '}
        {countOf(data.summary.totalDays, 'day')}
      </p>
      {data.summary.visited === 0 && <p>Add a trip to start your stamp book.</p>}
      {groups.map((group) => (
        <section key={group} aria-label={group || 'Parks'}>
          <h3 className="stats-view__title">{group}</h3>
          <ul className="stamps">
            {data.places
              .filter((place) => place.group === group)
              .map((place) => {
                const state = stateOf(place);
                const body = (
                  <>
                    <span className="stamp__name" aria-hidden="true">
                      {place.name}
                    </span>
                    <span className="stamp__detail" aria-hidden="true">
                      {state === 'visited' &&
                        `${countOf(place.days, 'day')} · ${countOf(place.trips, 'trip')}`}
                      {state === 'not-visited' && 'Not visited yet'}
                      {state === 'planned' && 'Coming'}
                    </span>
                  </>
                );
                return (
                  <li key={place.id}>
                    {state === 'visited' ? (
                      <button
                        type="button"
                        className="stamp stamp--visited"
                        aria-label={labelOf(place)}
                        onClick={() => setOpen(place.id)}
                      >
                        {body}
                      </button>
                    ) : (
                      <div
                        className={`stamp stamp--${state}`}
                        role="group"
                        aria-label={labelOf(place)}
                      >
                        {body}
                      </div>
                    )}
                  </li>
                );
              })}
          </ul>
        </section>
      ))}
      <p className="stats-page__note">{data.attribution}</p>
      {sheet && <PlaceSheet place={sheet} onClose={() => setOpen(null)} />}
    </div>
  );
}
