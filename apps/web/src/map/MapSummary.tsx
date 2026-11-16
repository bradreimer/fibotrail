import type { components } from '../api/schema';
import { formatKm } from '../trip/format';

type Totals = components['schemas']['MapTotals'];

const number = new Intl.NumberFormat('en-US');

/** The four numbers above the map: countries, trips, days away, and distance traveled. */
export function MapSummary({ totals }: { totals: Totals }) {
  const distance = formatKm(totals.distanceKm).replace(' km', '');
  const tiles = [
    {
      value: number.format(totals.countries),
      label: totals.countries === 1 ? 'Country' : 'Countries',
    },
    { value: number.format(totals.trips), label: totals.trips === 1 ? 'Trip' : 'Trips' },
    {
      value: number.format(totals.daysAway),
      label: totals.daysAway === 1 ? 'Day away' : 'Days away',
    },
    { value: distance, unit: 'km', label: 'Distance' },
  ];
  return (
    <section aria-labelledby="map-numbers">
      <h3 className="visually-hidden" id="map-numbers">
        Your travel in numbers
      </h3>
      <ul className="stats">
        {tiles.map((tile) => (
          <li key={tile.label} className="stat">
            <span className="stat__value">
              {tile.value}
              {tile.unit && <span className="stat__unit"> {tile.unit}</span>}
            </span>
            <span className="stat__label">{tile.label}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}
