import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';

import { useApi } from '../../../api/useApi';
import { ErrorState } from '../../../states/ErrorState';
import { formatDateRange } from '../../../trips/dates';
import {
  dotRadius,
  isLabeledYear,
  pointAt,
  spiralPath,
  tallestSpiral,
  tripShares,
} from './geometry';

const PADDING = 6;
/** About how many screen pixels one unit of the spiral takes on a phone, to size the dots. */
const UNIT_PX = 3.8;

/** Every added trip along a Fibonacci spiral, oldest at the center. See docs/specs/trip-spiral.md. */
export default function TripSpiral() {
  const spiral = useApi('/api/stats/trip-spiral');
  const layout = useMemo(() => tallestSpiral(), []);
  const [chosen, setChosen] = useState<number | null>(null);
  const [continent, setContinent] = useState<string | null>(null);

  if (spiral.loading && !spiral.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading the trip spiral</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (spiral.error) return <ErrorState message={spiral.error} onRetry={spiral.reload} />;
  const trips = spiral.data;
  if (!trips) return null;
  if (trips.length === 0) return <p>No trips yet. Add a trip, and it joins the spiral.</p>;

  const shares = tripShares(trips.length);
  const positions = shares.map((share) => pointAt(layout, share));
  const chosenIndex = trips.findIndex((item) => item.trip.id === chosen);
  const index = chosenIndex === -1 ? trips.length - 1 : chosenIndex;
  const selected = trips[index]!;
  const { bounds } = layout;
  const viewBox = `${bounds.minX - PADDING} ${bounds.minY - PADDING} ${bounds.maxX - bounds.minX + 2 * PADDING} ${bounds.maxY - bounds.minY + 2 * PADDING}`;
  const continents = [
    ...new Map(
      trips.flatMap((item) =>
        item.continentId && item.continentName ? [[item.continentId, item.continentName]] : [],
      ),
    ),
  ];
  const years = trips.map((item) => Number(item.trip.startDate.slice(0, 4)));
  const labeled = years.flatMap((year, position) =>
    isLabeledYear(year) && years.indexOf(year) === position ? [{ year, position }] : [],
  );
  const step = (by: number) => {
    const next = trips[Math.min(Math.max(index + by, 0), trips.length - 1)]!;
    setChosen(next.trip.id);
  };
  const first = years[0]!;
  const last = years[years.length - 1]!;

  return (
    <div className="spiral">
      <svg className="spiral__chart" viewBox={viewBox} aria-hidden="true" focusable="false">
        {layout.squares.map((corners, position) => (
          <polygon
            key={position}
            className="spiral__square"
            points={corners.map((corner) => `${corner.x},${corner.y}`).join(' ')}
          />
        ))}
        <path className="spiral__line" d={spiralPath(layout)} pathLength={1} />
        {labeled.map(({ year, position }) => (
          <text
            key={year}
            className="spiral__year"
            fontSize={4}
            x={positions[position]!.x}
            y={positions[position]!.y - 4}
            textAnchor="middle"
          >
            {year}
          </text>
        ))}
        {trips.map((item, position) => {
          const place = positions[position]!;
          const faded = continent !== null && item.continentId !== continent;
          const isSelected = position === index;
          return (
            <g
              key={item.trip.id}
              className={faded ? 'spiral__dot spiral__dot--faded' : 'spiral__dot'}
              style={{ ['--i' as string]: position }}
            >
              <circle
                className={`spiral__mark spiral__mark--${item.continentId ?? 'none'}${
                  isSelected ? ' spiral__mark--selected' : ''
                }`}
                cx={place.x}
                cy={place.y}
                r={dotRadius(item.trip.days) / UNIT_PX}
              />
              <circle
                className="spiral__hit"
                cx={place.x}
                cy={place.y}
                r="4.7"
                onClick={() => setChosen(item.trip.id)}
              />
            </g>
          );
        })}
      </svg>
      <p className="visually-hidden">
        {`${trips.length} ${trips.length === 1 ? 'trip' : 'trips'} from ${first} to ${last}, placed along a spiral from the oldest at the center to the newest at the edge. Selected: ${selected.trip.name}, ${selected.trip.days} ${selected.trip.days === 1 ? 'day' : 'days'}.`}
      </p>
      <ul className="spiral__chips" aria-label="Continents">
        {[['', 'All'] as const, ...continents].map(([id, name]) => (
          <li key={id || 'all'}>
            <button
              type="button"
              className="rings__band"
              aria-pressed={(continent ?? '') === id}
              onClick={() => setContinent(id === '' ? null : id)}
            >
              {name}
            </button>
          </li>
        ))}
      </ul>
      <div className="clock__steps">
        <button
          type="button"
          className="button button--secondary"
          disabled={index === 0}
          onClick={() => step(-1)}
        >
          Earlier
        </button>
        <button
          type="button"
          className="button button--secondary"
          disabled={index === trips.length - 1}
          onClick={() => step(1)}
        >
          Later
        </button>
      </div>
      <section className="clock__card" aria-live="polite" aria-label="Selected trip">
        <p className="stats-page__note">
          Trip {index + 1} of {trips.length}
        </p>
        <p>
          <Link to={`/trips/${selected.trip.id}`}>{selected.trip.name}</Link>
        </p>
        <p className="stats-page__note">
          {formatDateRange(selected.trip.startDate, selected.trip.endDate)} ·{' '}
          {selected.continentName ?? 'No country'} · {selected.trip.days}{' '}
          {selected.trip.days === 1 ? 'day' : 'days'}
        </p>
      </section>
    </div>
  );
}
