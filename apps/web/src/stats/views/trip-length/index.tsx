import { useApi } from '../../../api/useApi';
import { ErrorState } from '../../../states/ErrorState';

/** Added trips grouped by how many days they lasted, in five bins. */
export default function TripLength() {
  const bins = useApi('/api/stats/trip-length');

  if (bins.loading && !bins.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading trip length</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (bins.error) return <ErrorState message={bins.error} onRetry={bins.reload} />;
  if (!bins.data) return null;
  if (bins.data.every((bin) => bin.count === 0)) {
    return <p>No trips yet. Add a trip, and its length shows here.</p>;
  }

  const max = Math.max(...bins.data.map((bin) => bin.count), 1);
  return (
    <ul className="hbars" aria-label="Trips by length">
      {bins.data.map((bin) => (
        <li key={bin.label} className="hbars__row">
          <span className="hbars__label">{bin.label}</span>
          <span className="hbars__track" aria-hidden="true">
            <span className="hbars__bar" style={{ width: `${(bin.count / max) * 100}%` }} />
          </span>
          <span className="hbars__count">
            {bin.count}
            <span className="visually-hidden"> {bin.count === 1 ? 'trip' : 'trips'}</span>
          </span>
        </li>
      ))}
    </ul>
  );
}
