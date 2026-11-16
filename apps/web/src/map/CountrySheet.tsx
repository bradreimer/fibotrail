import { useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';

import type { components } from '../api/schema';
import { countOf, formatDateRange } from '../trips/dates';

export type MapCountry = components['schemas']['MapCountry'];

/** The trips that visited one country, and the date of the first visit. Opens from the map or the list. */
export function CountrySheet({ country, onClose }: { country: MapCountry; onClose: () => void }) {
  const close = useRef<HTMLButtonElement>(null);

  // Focus starts on Close, and returns to what opened the sheet.
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
        aria-labelledby="country-title"
        onKeyDown={(event) => event.key === 'Escape' && onClose()}
      >
        <h3 className="state__title" id="country-title">
          {country.name}
        </h3>
        <p>
          {countOf(country.trips.length, 'trip')} · First visit{' '}
          {formatDateRange(country.firstVisit, country.firstVisit)}
        </p>
        <ul className="country-trips">
          {country.trips.map((trip) => (
            <li key={trip.id}>
              <Link className="country-trips__link" to={`/trips/${trip.id}`}>
                <span className="country-trips__name">{trip.name}</span>
                <span className="country-trips__dates">
                  {formatDateRange(trip.startDate, trip.endDate)}
                </span>
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
