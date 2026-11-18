import { Link, useSearchParams } from 'react-router-dom';

import { useInboxBadge } from '../api/inbox';
import { useScan } from '../api/scan';
import { useSession } from '../api/session';
import { useApi } from '../api/useApi';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { countOf } from '../trips/dates';
import { TripCard } from '../trips/TripCard';
import { searchTrips } from '../trips/search';

/** The trip library: every trip you added, as cards, newest first. */
export function TripsScreen() {
  const session = useSession();
  const scan = useScan();
  const badge = useInboxBadge();
  const scanState = scan.status?.state;
  const trips = useApi('/api/trips', undefined, scanState);
  // The search lives in the address, so going back from a trip page keeps it.
  const [params, setParams] = useSearchParams();
  const query = params.get('q') ?? '';
  const setQuery = (value: string) =>
    setParams(value ? { q: value } : {}, { replace: true, preventScrollReset: true });

  if (trips.loading && !trips.data) {
    return (
      <div className="trip-grid" role="status" aria-busy="true">
        <span className="visually-hidden">Loading your trips</span>
        {Array.from({ length: 6 }, (_, index) => (
          <span key={index} className="skeleton skeleton--card" aria-hidden="true" />
        ))}
      </div>
    );
  }
  if (trips.error) return <ErrorState message={trips.error} onRetry={trips.reload} />;
  if (!trips.data) return null;

  if (trips.data.length === 0) {
    if (badge.count > 0) {
      return (
        <EmptyState
          title="No trips yet"
          message={`No trips yet. You have ${countOf(badge.count, 'possible trip')} to review.`}
          action={
            <Link className="button" to="/inbox">
              Open the inbox
            </Link>
          }
        />
      );
    }
    if (scanState === 'running') {
      return (
        <EmptyState
          title="No trips yet"
          message="No trips yet. Fibotrail is still scanning your library."
        />
      );
    }
    return (
      <EmptyState
        title="No trips yet"
        message="No trips yet. Scan your library, and Fibotrail suggests the trips it finds."
        action={
          <button
            type="button"
            className="button"
            disabled={!session || !scan.status}
            onClick={() => void scan.start()}
          >
            Scan now
          </button>
        }
      />
    );
  }

  const found = searchTrips(trips.data, query);
  return (
    <>
      <div className="trip-search" role="search">
        <label htmlFor="trip-search" className="visually-hidden">
          Search your trips
        </label>
        <input
          id="trip-search"
          className="trip-search__field"
          type="search"
          placeholder="Search trips, years, and places"
          autoComplete="off"
          autoCorrect="off"
          enterKeyHint="search"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') event.currentTarget.blur();
          }}
        />
        <p className="visually-hidden" role="status">
          {query.trim() ? `${countOf(found.length, 'trip')} found` : ''}
        </p>
      </div>
      {found.length === 0 ? (
        <EmptyState
          title="No matching trips"
          message={`No trip matches “${query.trim()}”. Try a trip name, a year, or a place.`}
          action={
            <button type="button" className="button" onClick={() => setQuery('')}>
              Clear search
            </button>
          }
        />
      ) : (
        <ul className="trip-grid" aria-label="Your trips">
          {found.map((trip) => (
            <li key={trip.id}>
              <TripCard trip={trip} />
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
