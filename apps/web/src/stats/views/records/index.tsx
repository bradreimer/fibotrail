import { Link } from 'react-router-dom';

import { useApi } from '../../../api/useApi';
import { ErrorState } from '../../../states/ErrorState';
import { formatKm } from '../../../trip/format';
import { formatDateRange } from '../../../trips/dates';

/** The longest trip, the farthest, the most countries, the most-visited country, and the longest gap. */
export default function Records() {
  const records = useApi('/api/stats/records');

  if (records.loading && !records.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading records</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (records.error) return <ErrorState message={records.error} onRetry={records.reload} />;
  const data = records.data;
  if (!data) return null;

  const rows: { label: string; value: React.ReactNode }[] = [];
  if (data.longestTrip) {
    const { trip } = data.longestTrip;
    rows.push({
      label: 'Longest trip',
      value: (
        <>
          <Link to={`/trips/${trip.id}`}>{trip.name}</Link>, {trip.days} days
        </>
      ),
    });
  }
  if (data.farthestTrip) {
    const { trip, value } = data.farthestTrip;
    rows.push({
      label: 'Farthest from home',
      value: (
        <>
          <Link to={`/trips/${trip.id}`}>{trip.name}</Link>, {formatKm(value)}
        </>
      ),
    });
  }
  if (data.mostCountriesTrip) {
    const { trip, value } = data.mostCountriesTrip;
    rows.push({
      label: 'Most countries',
      value: (
        <>
          <Link to={`/trips/${trip.id}`}>{trip.name}</Link>, {value}{' '}
          {value === 1 ? 'country' : 'countries'}
        </>
      ),
    });
  }
  if (data.mostVisitedCountry) {
    const country = data.mostVisitedCountry;
    rows.push({
      label: 'Most visited country',
      value: (
        <>
          <Link to={`/stats/countries/${encodeURIComponent(country.countryId)}`}>
            {country.name}
          </Link>
          , {country.trips} {country.trips === 1 ? 'trip' : 'trips'} · {country.days}{' '}
          {country.days === 1 ? 'day' : 'days'}
        </>
      ),
    });
  }
  if (data.longestGap) {
    const gap = data.longestGap;
    rows.push({
      label: 'Longest gap between trips',
      value: (
        <>
          {gap.days} days, from <Link to={`/trips/${gap.after.id}`}>{gap.after.name}</Link> (
          {formatDateRange(gap.after.startDate, gap.after.endDate)}) to{' '}
          <Link to={`/trips/${gap.before.id}`}>{gap.before.name}</Link>
        </>
      ),
    });
  }

  if (rows.length === 0) return <p>No trips yet. Add a trip, and your records show here.</p>;
  return (
    <dl className="records">
      {rows.map((row) => (
        <div key={row.label} className="records__row">
          <dt>{row.label}</dt>
          <dd>{row.value}</dd>
        </div>
      ))}
    </dl>
  );
}
