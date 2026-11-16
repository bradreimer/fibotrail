import { useState } from 'react';
import { Link } from 'react-router-dom';

import { useApi } from '../../../api/useApi';
import { ErrorState } from '../../../states/ErrorState';
import { formatDateRange } from '../../../trips/dates';

/** Added trips by the year they started: one bar a year, and a list of the chosen year's trips. */
export default function TripsPerYear() {
  const years = useApi('/api/stats/trips-per-year');
  const [chosen, setChosen] = useState<number | null>(null);

  if (years.loading && !years.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading trips per year</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (years.error) return <ErrorState message={years.error} onRetry={years.reload} />;
  if (!years.data) return null;
  if (years.data.length === 0) return <p>No trips yet. Add a trip, and its year shows here.</p>;

  const max = Math.max(...years.data.map((year) => year.trips.length), 1);
  const currentYear = new Date().getFullYear();
  const year = years.data.find((item) => item.year === chosen) ?? null;
  return (
    <div>
      <ul className="bars" aria-label="Trips per year">
        {years.data.map((item) => {
          const count = item.trips.length;
          return (
            <li key={item.year} className="bars__item">
              <button
                type="button"
                className={`bars__button${item.year === currentYear ? ' bars__button--current' : ''}`}
                aria-pressed={item.year === chosen}
                aria-label={`${item.year}: ${count} ${count === 1 ? 'trip' : 'trips'}`}
                disabled={count === 0}
                onClick={() => setChosen(item.year === chosen ? null : item.year)}
              >
                <span className="bars__count" aria-hidden="true">
                  {count}
                </span>
                <span
                  className="bars__bar"
                  aria-hidden="true"
                  style={{ height: `${(count / max) * 100}%` }}
                />
                <span className="bars__label" aria-hidden="true">
                  {String(item.year).slice(2)}
                </span>
              </button>
            </li>
          );
        })}
      </ul>
      {year && (
        <section aria-label={`Trips in ${year.year}`}>
          <h4 className="stats-view__subtitle">{year.year}</h4>
          <ul className="stats-list">
            {year.trips.map((trip) => (
              <li key={trip.id}>
                <Link to={`/trips/${trip.id}`}>{trip.name}</Link>
                <span className="stats-list__meta">
                  {formatDateRange(trip.startDate, trip.endDate)}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
