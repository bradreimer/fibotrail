import { useState } from 'react';
import { Link } from 'react-router-dom';

import type { components } from '../api/schema';
import { countOf, formatDateRange } from './dates';

export type TripSummary = components['schemas']['TripSummary'];

/** The line under the name, such as "Mar 15–24, 2027 · 10 days · Orlando". */
export function tripLine(trip: TripSummary): string {
  return [formatDateRange(trip.startDate, trip.endDate), countOf(trip.days, 'day'), trip.place]
    .filter(Boolean)
    .join(' · ');
}

/** The spiral of the logo, shown when a cover photo can't load. */
function SpiralMark() {
  return (
    <svg className="trip-card__mark" viewBox="0 0 15 10" aria-hidden="true" focusable="false">
      <path
        d="M1 9A8 8 0 0 1 9 1A5 5 0 0 1 14 6A3 3 0 0 1 11 9A2 2 0 0 1 9 7A1 1 0 0 1 10 6"
        fill="none"
        stroke="currentColor"
        strokeWidth="0.8"
        strokeLinecap="round"
      />
    </svg>
  );
}

/** A trip in the library: the cover photo, the name, and one line of facts. The card is one link. */
export function TripCard({ trip }: { trip: TripSummary }) {
  const [broken, setBroken] = useState(false);
  const dates = formatDateRange(trip.startDate, trip.endDate);
  return (
    // The label is read once, as the name, dates, and destination. Without it, a screen reader
    // reads the name twice: once from the cover's alt text and once from the caption.
    <Link
      className="trip-card"
      to={`/trips/${trip.id}`}
      draggable={false}
      aria-label={`${trip.name}, ${tripLine(trip)}`}
    >
      {broken ? (
        <SpiralMark />
      ) : (
        <img
          className="trip-card__cover"
          src={`/api/photos/${trip.coverPhotoId}/thumbnail`}
          alt={`${trip.name}, ${dates}`}
          loading="lazy"
          decoding="async"
          draggable={false}
          onError={() => setBroken(true)}
        />
      )}
      <span className="trip-card__text">
        <span className="trip-card__name">{trip.name}</span>
        <span className="trip-card__line">{tripLine(trip)}</span>
      </span>
    </Link>
  );
}
