import type { ReactNode } from 'react';

import type { components } from '../api/schema';
import { countOf } from '../trips/dates';
import { formatKm } from './format';

/** A kilometer value as its number and its unit, so the unit can be set smaller. */
function km(value: number): { value: string; unit: string } {
  return { value: formatKm(value).replace(' km', ''), unit: 'km' };
}

type Stats = components['schemas']['TripStats'];

function Icon({ children }: { children: ReactNode }) {
  return (
    <svg
      className="stat__icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {children}
    </svg>
  );
}

const number = new Intl.NumberFormat('en-US');

/** The five numbers of a trip, as tiles. */
export function StatTiles({ stats }: { stats: Stats }) {
  const tiles: { value: string; unit?: string; label: string; icon: ReactNode }[] = [
    {
      value: number.format(stats.days),
      label: stats.days === 1 ? 'Day' : 'Days',
      icon: (
        <Icon>
          <rect x="3" y="5" width="18" height="16" rx="2" />
          <path d="M3 10h18M8 3v4M16 3v4" />
        </Icon>
      ),
    },
    {
      value: number.format(stats.photos),
      label: stats.photos === 1 ? 'Photo' : 'Photos',
      icon: (
        <Icon>
          <rect x="3" y="6" width="18" height="14" rx="2" />
          <circle cx="12" cy="13" r="3.5" />
          <path d="M9 6l1.5-2h3L15 6" />
        </Icon>
      ),
    },
    {
      value: number.format(stats.stops),
      label: stats.stops === 1 ? 'Stop' : 'Stops',
      icon: (
        <Icon>
          <path d="M12 21s-6-5.5-6-10a6 6 0 0 1 12 0c0 4.5-6 10-6 10Z" />
          <circle cx="12" cy="11" r="2" />
        </Icon>
      ),
    },
    {
      ...km(stats.distanceKm),
      label: 'Distance',
      icon: (
        <Icon>
          <path d="M4 18c3 0 3-6 8-6s5-6 8-6" />
          <circle cx="4" cy="18" r="1.5" />
          <circle cx="20" cy="6" r="1.5" />
        </Icon>
      ),
    },
    {
      ...km(stats.farthestFromHomeKm),
      label: 'Farthest from home',
      icon: (
        <Icon>
          <path d="M3 11l9-7 9 7" />
          <path d="M5 10v9h5M14 19h6" />
          <path d="M17 16l3 3-3 3" />
        </Icon>
      ),
    },
  ];
  return (
    <section aria-labelledby="trip-numbers">
      <h3 className="visually-hidden" id="trip-numbers">
        Trip numbers
      </h3>
      <ul className="stats">
        {tiles.map((tile) => (
          <li key={tile.label} className="stat">
            {tile.icon}
            <span className="stat__value">
              {tile.value}
              {tile.unit && <span className="stat__unit"> {tile.unit}</span>}
            </span>
            <span className="stat__label">{tile.label}</span>
          </li>
        ))}
      </ul>
      {stats.unplaced > 0 && (
        <p className="settings__hint">{`${countOf(stats.unplaced, 'photo has', 'photos have')} no location.`}</p>
      )}
    </section>
  );
}
