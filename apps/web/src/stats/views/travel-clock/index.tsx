import { useState } from 'react';
import { Link } from 'react-router-dom';

import { useApi } from '../../../api/useApi';
import { ErrorState } from '../../../states/ErrorState';
import { formatDateRange } from '../../../trips/dates';

const MONTHS = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];

type Measure = 'days' | 'trips';

const CENTER = 100;
const INNER = 30;
const OUTER = 85;
const LABEL = 94;
const WEDGE = Math.PI / 6;
/** The gap between wedges, in radians. */
const GAP = 0.04;

function point(radius: number, angle: number): string {
  return `${(CENTER + radius * Math.sin(angle)).toFixed(2)} ${(CENTER - radius * Math.cos(angle)).toFixed(2)}`;
}

/** A ring segment for a month: January starts at the top and the months run clockwise. */
function wedge(index: number, outer: number): string {
  const from = index * WEDGE + GAP;
  const to = (index + 1) * WEDGE - GAP;
  return `M ${point(INNER, from)} L ${point(outer, from)} A ${outer} ${outer} 0 0 1 ${point(outer, to)} L ${point(INNER, to)} A ${INNER} ${INNER} 0 0 0 ${point(INNER, from)} Z`;
}

function plural(count: number, one: string, many: string): string {
  return `${count} ${count === 1 ? one : many}`;
}

/** Days away and trips in each month of the year, across every year. See docs/specs/travel-clock.md. */
export default function TravelClock() {
  const clock = useApi('/api/stats/travel-clock');
  const [measure, setMeasure] = useState<Measure>('days');
  const [chosen, setChosen] = useState<number | null>(null);

  if (clock.loading && !clock.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading the travel clock</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (clock.error) return <ErrorState message={clock.error} onRetry={clock.reload} />;
  const months = clock.data;
  if (!months) return null;
  if (months.every((month) => month.days === 0)) {
    return <p>No trips yet. Add a trip, and your travel months show here.</p>;
  }

  const value = (index: number) =>
    measure === 'days' ? months[index]!.days : months[index]!.trips.length;
  const max = Math.max(...months.map((_, index) => value(index)), 1);
  const busiestIndex = months.reduce(
    (best, _, index) => (value(index) > value(best) ? index : best),
    0,
  );
  const daysBusiest = months.reduce(
    (best, month, index) => (month.days > months[best]!.days ? index : best),
    0,
  );
  const selected = chosen ?? daysBusiest;
  const unit = (count: number) =>
    measure === 'days' ? plural(count, 'day', 'days') : plural(count, 'trip', 'trips');
  const step = (by: number) => setChosen((selected + by + 12) % 12);
  const picked = months[selected]!;

  return (
    <div className="clock">
      <div className="clock__switch" role="group" aria-label="Show">
        {(['days', 'trips'] as const).map((option) => (
          <button
            key={option}
            type="button"
            className="clock__option"
            aria-pressed={measure === option}
            onClick={() => setMeasure(option)}
          >
            {option === 'days' ? 'Days' : 'Trips'}
          </button>
        ))}
      </div>
      <svg className="clock__wheel" viewBox="0 0 200 200" aria-hidden="true" focusable="false">
        <circle className="clock__core" cx={CENTER} cy={CENTER} r={INNER - 2} />
        {months.map((month, index) => {
          const share = value(index) / max;
          const outer = INNER + (OUTER - INNER) * share;
          const middle = (index + 0.5) * WEDGE;
          return (
            <g key={month.month}>
              <path
                className="clock__line"
                d={wedge(index, INNER + 2)}
                style={{ pointerEvents: 'none' }}
              />
              {value(index) > 0 && (
                <path
                  className={`clock__bar${index === selected ? ' clock__bar--selected' : ''}`}
                  d={wedge(index, outer)}
                  style={{ ['--i' as string]: index }}
                />
              )}
              <path
                className="clock__hit"
                d={wedge(index, OUTER)}
                onClick={() => setChosen(index)}
              />
              <text
                className="clock__month"
                x={CENTER + LABEL * Math.sin(middle) * 1.0}
                y={CENTER - LABEL * Math.cos(middle) * 1.0}
                textAnchor="middle"
                dominantBaseline="central"
              >
                {MONTHS[index]!.slice(0, 3)}
              </text>
            </g>
          );
        })}
        <text className="clock__center" x={CENTER} y={CENTER - 4} textAnchor="middle">
          {MONTHS[selected]!.slice(0, 3)}
        </text>
        <text className="clock__center-value" x={CENTER} y={CENTER + 10} textAnchor="middle">
          {value(selected)}
        </text>
      </svg>
      <p className="visually-hidden">
        {`${measure === 'days' ? 'Days away' : 'Trips'} by month. `}
        {months
          .map(
            (_, index) =>
              `${MONTHS[index]}: ${value(index)}${index === busiestIndex ? ', the busiest' : ''}.`,
          )
          .join(' ')}
      </p>
      <div className="clock__steps">
        <button type="button" className="button button--secondary" onClick={() => step(-1)}>
          Earlier
        </button>
        <button type="button" className="button button--secondary" onClick={() => step(1)}>
          Later
        </button>
      </div>
      <section className="clock__card" aria-live="polite" aria-label="Selected month">
        <h4 className="stats-view__subtitle">
          {MONTHS[selected]} · {plural(picked.days, 'day', 'days')} ·{' '}
          {plural(picked.trips.length, 'trip', 'trips')}
        </h4>
        {measure === 'trips' && <p className="visually-hidden">Showing {unit(value(selected))}</p>}
        <ul className="stats-list">
          {picked.trips.map((trip) => (
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
