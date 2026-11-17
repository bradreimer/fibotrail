import { Link, useParams } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { ParkList } from '../collections/ParkList';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { tripsText } from './CollectionScreen';

/** A resort, level 3: its parks with trips and days. A visited park opens a sheet with its trips. */
export function ResortScreen() {
  const { id = '', resort = '' } = useParams();
  const collection = useApi('/api/collections/{id}', `/api/collections/${encodeURIComponent(id)}`);

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

  return (
    <div className="stats-screen">
      <p>
        <Link to={`/collections/${encodeURIComponent(id)}`}>{data.summary.name}</Link>
      </p>
      <h2 className="stats-page__title">{info.name}</h2>
      <p className="stats-page__lead">{tripsText(info.trips)}</p>
      <ParkList places={data.places} resort={resort} />
      <p className="stats-page__note">{data.attribution}</p>
    </div>
  );
}
