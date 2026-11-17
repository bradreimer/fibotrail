import { Link, useParams } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { BackLink } from '../shell/BackLink';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';

/** The countries of one continent that you've visited, with trips and days for each. */
export function StatsContinentScreen() {
  const { continent = '' } = useParams();
  const page = useApi(
    '/api/stats/continents/{continent}',
    `/api/stats/continents/${encodeURIComponent(continent)}`,
  );

  if (page.loading && !page.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading the continent</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (page.errorStatus === 404) {
    return (
      <EmptyState
        title="No such continent"
        message="That continent doesn't exist."
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
      <BackLink to="/stats">Stats</BackLink>
      <h2 className="stats-page__title">{data.name}</h2>
      <p className="stats-page__lead">
        {data.visited} of {data.total} countries visited
      </p>
      {data.countries.length === 0 ? (
        <p>No visits here yet.</p>
      ) : (
        <ul className="stats-list stats-list--rows">
          {data.countries.map((country) => (
            <li key={country.id}>
              <Link to={`/stats/countries/${encodeURIComponent(country.id)}`}>{country.name}</Link>
              <span className="stats-list__meta">
                {country.trips} {country.trips === 1 ? 'trip' : 'trips'} · {country.days}{' '}
                {country.days === 1 ? 'day' : 'days'}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
