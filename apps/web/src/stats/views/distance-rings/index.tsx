import { useState } from 'react';
import { Link } from 'react-router-dom';

import { useApi } from '../../../api/useApi';
import { ErrorState } from '../../../states/ErrorState';
import { formatKm } from '../../../trip/format';
import {
  BANDS,
  compassWord,
  dotRadius,
  inBand,
  OUTER_KM,
  polar,
  radiusShare,
  RING_KM,
} from './geometry';

const SIZE = 200;
const MID = SIZE / 2;
const RADIUS = 80;

const number = new Intl.NumberFormat('en-US');

/** Every added trip on rings around home, by how far and in which direction. See docs/specs/distance-rings.md. */
export default function DistanceRings() {
  const rings = useApi('/api/stats/distance-rings');
  const [chosen, setChosen] = useState<number | null>(null);
  const [band, setBand] = useState<number | null>(null);

  if (rings.loading && !rings.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading the distance rings</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (rings.error) return <ErrorState message={rings.error} onRetry={rings.reload} />;
  const trips = rings.data;
  if (!trips) return null;
  if (trips.length === 0) {
    return (
      <p>No distances yet. Confirm your home location and add a trip, and the rings fill in.</p>
    );
  }

  // The server gives the nearest trip first, so the last is the farthest.
  const selectedIndex = trips.findIndex((item) => item.trip.id === chosen);
  const index = selectedIndex === -1 ? trips.length - 1 : selectedIndex;
  const selected = trips[index]!;
  const step = (by: number) => {
    const next = trips[Math.min(Math.max(index + by, 0), trips.length - 1)]!;
    setChosen(next.trip.id);
  };
  const describe = (item: (typeof trips)[number]) =>
    `${item.trip.name} · ${formatKm(item.distanceKm)} ${compassWord(item.bearingDegrees)} · ${item.trip.days} days`;
  const counts = BANDS.map((item) => trips.filter((trip) => inBand(trip.distanceKm, item)).length);
  const activeBand = band === null ? null : BANDS[band]!;

  return (
    <div className="rings">
      <svg
        className="rings__chart"
        viewBox={`0 0 ${SIZE} ${SIZE}`}
        aria-hidden="true"
        focusable="false"
      >
        <circle className="rings__edge" cx={MID} cy={MID} r={RADIUS} />
        {RING_KM.map((km) => (
          <g key={km}>
            <circle className="rings__ring" cx={MID} cy={MID} r={RADIUS * radiusShare(km)} />
            <text
              className="rings__label"
              x={MID}
              y={MID - RADIUS * radiusShare(km) - 1}
              textAnchor="middle"
            >
              {number.format(km)}
            </text>
          </g>
        ))}
        {(
          [
            ['N', 0],
            ['E', 90],
            ['S', 180],
            ['W', 270],
          ] as const
        ).map(([letter, bearing]) => {
          const place = polar(bearing, RADIUS + 12);
          return (
            <text
              key={letter}
              className="rings__compass"
              x={MID + place.x}
              y={MID + place.y}
              textAnchor="middle"
              dominantBaseline="central"
            >
              {letter}
            </text>
          );
        })}
        <circle className="rings__home" cx={MID} cy={MID} r="3" />
        {trips.map((item) => {
          const place = polar(item.bearingDegrees, RADIUS * radiusShare(item.distanceKm));
          const faded = activeBand !== null && !inBand(item.distanceKm, activeBand);
          const isSelected = item.trip.id === selected.trip.id;
          return (
            <g
              key={item.trip.id}
              className={faded ? 'rings__dot rings__dot--faded' : 'rings__dot'}
              transform={`translate(${MID + place.x} ${MID + place.y})`}
            >
              <circle
                className={isSelected ? 'rings__mark rings__mark--selected' : 'rings__mark'}
                r={dotRadius(item.trip.days)}
              />
              <circle className="rings__hit" r="14" onClick={() => setChosen(item.trip.id)} />
            </g>
          );
        })}
      </svg>
      <p className="visually-hidden">
        {`${trips.length} ${trips.length === 1 ? 'trip' : 'trips'} by distance from home. `}
        {BANDS.map((item, position) => `${item.label}: ${counts[position]}.`).join(' ')}{' '}
        {`Farthest: ${describe(trips[trips.length - 1]!)}.`}
      </p>
      <ul className="rings__bands" aria-label="Distance bands">
        {BANDS.map((item, position) => (
          <li key={item.label}>
            <button
              type="button"
              className="rings__band"
              aria-pressed={band === position}
              onClick={() => setBand(band === position ? null : position)}
            >
              {item.label} · {counts[position]} {counts[position] === 1 ? 'trip' : 'trips'}
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
          Nearer
        </button>
        <button
          type="button"
          className="button button--secondary"
          disabled={index === trips.length - 1}
          onClick={() => step(1)}
        >
          Farther
        </button>
      </div>
      <section className="clock__card" aria-live="polite" aria-label="Selected trip">
        <p>
          <Link to={`/trips/${selected.trip.id}`}>{selected.trip.name}</Link>
          {` · ${formatKm(selected.distanceKm)} ${compassWord(selected.bearingDegrees)} · ${selected.trip.days} days`}
        </p>
        <p className="stats-page__note">
          The rings run to {number.format(OUTER_KM)} km on a scale that gives near trips room.
        </p>
      </section>
    </div>
  );
}
