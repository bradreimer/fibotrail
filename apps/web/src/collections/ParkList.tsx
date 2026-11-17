import { useState } from 'react';

import { countOf } from '../trips/dates';
import { tripsText } from '../screens/CollectionScreen';
import { PlaceSheet, type CollectionPlace } from './PlaceSheet';

/** The open parks of one resort, most trips first. A visited park opens a sheet with its trips. */
export function ParkList({ places, resort }: { places: CollectionPlace[]; resort: string }) {
  const [open, setOpen] = useState<string | null>(null);
  const parks = places
    .filter((place) => place.group === resort && place.status === 'open')
    .sort((a, b) => b.trips - a.trips || a.name.localeCompare(b.name));
  const sheet = parks.find((place) => place.id === open) ?? null;
  return (
    <>
      <ul className="collections">
        {parks.map((park) => {
          const detail =
            park.trips === 0
              ? tripsText(0)
              : `${countOf(park.trips, 'trip')} · ${countOf(park.days, 'day')}`;
          const label =
            park.trips === 0
              ? `${park.name}, not visited yet`
              : `${park.name}, ${countOf(park.trips, 'trip')}, ${countOf(park.days, 'day')}`;
          const body = (
            <>
              <span className="collection-card__name" aria-hidden="true">
                {park.name}
              </span>
              <span className="collection-card__count" aria-hidden="true">
                {detail}
              </span>
            </>
          );
          return (
            <li key={park.id}>
              {park.trips > 0 ? (
                <button
                  type="button"
                  className="collection-card collection-card--button"
                  aria-label={label}
                  onClick={() => setOpen(park.id)}
                >
                  {body}
                </button>
              ) : (
                <div className="collection-card" role="group" aria-label={label}>
                  {body}
                </div>
              )}
            </li>
          );
        })}
      </ul>
      {sheet && <PlaceSheet place={sheet} onClose={() => setOpen(null)} />}
    </>
  );
}
