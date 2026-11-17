import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { PlaceSheet } from '../collections/PlaceSheet';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { countOf } from '../trips/dates';
import { tripsText } from './CollectionScreen';

/** A resort, level 3: its parks with trips and days. A visited park opens a sheet with its trips. */
export function ResortScreen() {
  const { id = '', resort = '' } = useParams();
  const collection = useApi('/api/collections/{id}', `/api/collections/${encodeURIComponent(id)}`);
  const [open, setOpen] = useState<string | null>(null);

  if (collection.loading && !collection.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading the resort</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (collection.error && collection.errorStatus !== 404)
    return <ErrorState message={collection.error} onRetry={collection.reload} />;
  const data = collection.data;
  const info = data?.resorts.find((item) => item.name === resort);
  if (!data || !info) {
    return (
      <EmptyState
        title="No such resort"
        message="That resort isn't in this collection."
        action={
          <Link className="button" to={`/collections/${encodeURIComponent(id)}`}>
            Back to the collection
          </Link>
        }
      />
    );
  }

  const parks = data.places
    .filter((place) => place.group === resort && place.status === 'open')
    .sort((a, b) => b.trips - a.trips || a.name.localeCompare(b.name));
  const sheet = parks.find((place) => place.id === open) ?? null;
  return (
    <div className="stats-screen">
      <p>
        <Link to={`/collections/${encodeURIComponent(id)}`}>{data.summary.name}</Link>
      </p>
      <h2 className="stats-page__title">{info.name}</h2>
      <p className="stats-page__lead">{tripsText(info.trips)}</p>
      <ul className="collections">
        {parks.map((park) => {
          const detail =
            park.trips === 0
              ? tripsText(0)
              : `${countOf(park.trips, 'trip')} · ${countOf(park.days, 'day')}`;
          const label =
            park.trips === 0
              ? `${park.name}, not visited yet`
              : `${park.name}, ${countOf(park.trips, 'trip')}, ${countOf(park.days, 'day')}`;
          const body = (
            <>
              <span className="collection-card__name" aria-hidden="true">
                {park.name}
              </span>
              <span className="collection-card__count" aria-hidden="true">
                {detail}
              </span>
            </>
          );
          return (
            <li key={park.id}>
              {park.trips > 0 ? (
                <button
                  type="button"
                  className="collection-card collection-card--button"
                  aria-label={label}
                  onClick={() => setOpen(park.id)}
                >
                  {body}
                </button>
              ) : (
                <div className="collection-card" role="group" aria-label={label}>
                  {body}
                </div>
              )}
            </li>
          );
        })}
      </ul>
      <p className="stats-page__note">{data.attribution}</p>
      {sheet && <PlaceSheet place={sheet} onClose={() => setOpen(null)} />}
    </div>
  );
}
