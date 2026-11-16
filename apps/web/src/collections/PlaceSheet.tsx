import { useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';

import type { components } from '../api/schema';
import { countOf, formatDateRange } from '../trips/dates';

export type CollectionPlace = components['schemas']['CollectionPlaceInfo'];

/** A park's days, trips, and first visit. Opens from a visited stamp. */
export function PlaceSheet({ place, onClose }: { place: CollectionPlace; onClose: () => void }) {
  const close = useRef<HTMLButtonElement>(null);

  // Focus starts on Close, and returns to the stamp that opened the sheet.
  useEffect(() => {
    const before = document.activeElement as HTMLElement | null;
    close.current?.focus();
    return () => before?.focus?.();
  }, []);

  return (
    <div className="sheet-backdrop">
      {/* The backdrop is a button for a pointer only. The keyboard uses Close or Escape. */}
      <button
        type="button"
        className="sheet-backdrop__close"
        tabIndex={-1}
        aria-hidden="true"
        onClick={onClose}
      />
      {/* eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- Escape closes the sheet. */}
      <div
        className="sheet"
        role="dialog"
        aria-modal="true"
        aria-labelledby="place-title"
        onKeyDown={(event) => event.key === 'Escape' && onClose()}
      >
        <h3 className="state__title" id="place-title">
          {place.name}
        </h3>
        <p>
          {countOf(place.days, 'day')} · {countOf(place.trips, 'trip')}
          {place.firstVisit && (
            <> · First visit {formatDateRange(place.firstVisit, place.firstVisit)}</>
          )}
        </p>
        <ul className="country-trips">
          {place.tripList.map((trip) => (
            <li key={trip.tripId}>
              <Link className="country-trips__link" to={`/trips/${trip.tripId}`}>
                <span className="country-trips__name">{trip.name}</span>
                <span className="country-trips__dates">{countOf(trip.days, 'day')} here</span>
              </Link>
            </li>
          ))}
        </ul>
        <div className="sheet__actions">
          <button type="button" className="button button--secondary" ref={close} onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
