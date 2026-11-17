import { Link, useParams } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { ViewSlot } from '../stats/ViewSlot';
import { BackLink } from '../shell/BackLink';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { formatDateRange } from '../trips/dates';

/** One country: its first visit, trips and days, the views in the country slot, and its trips. */
export function StatsCountryScreen() {
  const { id = '' } = useParams();
  const page = useApi(
    '/api/stats/countries/{id}',
    `/api/stats/countries/${encodeURIComponent(id)}`,
  );

  if (page.loading && !page.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading the country</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (page.errorStatus === 404) {
    return (
      <EmptyState
        title="No visits here"
        message="No added trip has a stop in this country."
        action={
          <Link className="button" to="/stats">
            Back to Stats
          </Link>
        }
      />
    );
  }
  if (page.error) return <ErrorState message={page.error} onRetry={page.reload} />;
  const data = page.data;
  if (!data) return null;

  return (
    <div className="stats-screen">
      <BackLink to={`/stats/continents/${data.continentId}`}>{data.continentName}</BackLink>
      <h2 className="stats-page__title">{data.name}</h2>
      <p className="stats-page__lead">
        First visit {formatDateRange(data.firstVisit, data.firstVisit)}
      </p>
      <ul className="stats">
        <li className="stat">
          <span className="stat__value">{data.trips}</span>
          <span className="stat__label">{data.trips === 1 ? 'Trip' : 'Trips'}</span>
        </li>
        <li className="stat">
          <span className="stat__value">{data.days}</span>
          <span className="stat__label">{data.days === 1 ? 'Day' : 'Days'}</span>
        </li>
      </ul>
      {data.hasUnpositionedDays && (
        <p className="stats-page__note">Days count only photos with a location.</p>
      )}
      <ViewSlot slot="country" countryId={data.id} />
      <section aria-labelledby="country-trips">
        <h3 className="stats-view__title" id="country-trips">
          Trips
        </h3>
        <ul className="stats-list stats-list--rows">
          {data.tripList.map((trip) => (
            <li key={trip.id}>
              <Link to={`/trips/${trip.id}`}>{trip.name}</Link>
              <span className="stats-list__meta">
                {formatDateRange(trip.startDate, trip.endDate)}
              </span>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
