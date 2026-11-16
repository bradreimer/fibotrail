import { Link } from 'react-router-dom';

import { useApi } from '../../../api/useApi';
import { ErrorState } from '../../../states/ErrorState';
import { countOf } from '../../../trips/dates';

/** The days and trips in each theme park, grouped by collection. See docs/specs/theme-parks-view.md. */
export default function ThemeParks() {
  const groups = useApi('/api/stats/theme-parks');

  if (groups.loading && !groups.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading theme parks</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (groups.error) return <ErrorState message={groups.error} onRetry={groups.reload} />;
  if (!groups.data) return null;
  if (groups.data.length === 0) return <p>No collections yet, so there are no parks to count.</p>;

  return (
    <div className="parks">
      {groups.data.map((group) => (
        <section key={group.collectionId} aria-label={group.name}>
          <h4 className="stats-view__subtitle">
            <Link to={`/collections/${group.collectionId}`}>{group.name}</Link> ·{' '}
            {countOf(group.totalDays, 'day')}
          </h4>
          <ul className="stats-list stats-list--rows">
            {group.parks.map((park) => (
              <li key={park.id}>
                <span>{park.name}</span>
                <span className="stats-list__meta">
                  {park.days > 0
                    ? `${countOf(park.days, 'day')} · ${countOf(park.trips, 'trip')}`
                    : '0 days · Not visited yet'}
                </span>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}
