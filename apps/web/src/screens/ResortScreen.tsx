import { Link, useParams } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { TripCard } from '../trips/TripCard';
import { tripsText } from './CollectionScreen';

/** A resort, level 3: every added trip that visits it, newest first, each with a badge for each park. */
export function ResortScreen() {
  const { id = '', resort = '' } = useParams();
  const collection = useApi('/api/collections/{id}', `/api/collections/${encodeURIComponent(id)}`);
  const trips = useApi('/api/trips');

  if ((collection.loading && !collection.data) || (trips.loading && !trips.data)) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading the resort</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (collection.error && collection.errorStatus !== 404)
    return <ErrorState message={collection.error} onRetry={collection.reload} />;
  if (trips.error) return <ErrorState message={trips.error} onRetry={trips.reload} />;
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

  // The trip library is newest first, so its order is the order here.
  const parksOf = new Map(info.tripList.map((trip) => [trip.tripId, trip.parks]));
  const visits = (trips.data ?? []).filter((trip) => parksOf.has(trip.id));

  return (
    <div className="stats-screen">
      <p>
        <Link to={`/collections/${encodeURIComponent(id)}`}>{data.summary.name}</Link>
      </p>
      <h2 className="stats-page__title">{info.name}</h2>
      <p className="stats-page__lead">{tripsText(info.trips)}</p>
      <ul className="resort-trips">
        {visits.map((trip) => {
          const parks = parksOf.get(trip.id) ?? [];
          return (
            <li key={trip.id} className="resort-trip">
              <TripCard trip={trip} />
              <ul className="badges" aria-label={`Parks on ${trip.name}`}>
                {parks.map((park) => (
                  <li key={park} className="badge">
                    <span aria-hidden="true">✓ </span>
                    {park}
                  </li>
                ))}
              </ul>
            </li>
          );
        })}
      </ul>
      <p className="stats-page__note">{data.attribution}</p>
    </div>
  );
}
