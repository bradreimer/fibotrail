import type { components } from '../api/schema';
import { formatKm } from '../trip/format';

type Summary = components['schemas']['InsightsSummary'];

const number = new Intl.NumberFormat('en-US');

/**
 * The six totals at the top of the Insights screen: trips, countries, cities, continents, days
 * away, and distance. Every number comes from added trips. See docs/specs/insights.md.
 */
export function InsightsTotals({ summary }: { summary: Summary }) {
  const share =
    summary.outlineCountries > 0 ? (summary.countries / summary.outlineCountries) * 100 : 0;
  const tiles = [
    { value: number.format(summary.trips), label: summary.trips === 1 ? 'Trip' : 'Trips' },
    {
      value: number.format(summary.countries),
      label: summary.countries === 1 ? 'Country' : 'Countries',
      detail: `${summary.countries} / ${summary.outlineCountries} · ${share.toFixed(1)}%`,
    },
    { value: number.format(summary.cities), label: summary.cities === 1 ? 'City' : 'Cities' },
    {
      value: `${summary.continents} / 7`,
      label: summary.continents === 1 ? 'Continent' : 'Continents',
    },
    {
      value: number.format(summary.daysAway),
      label: summary.daysAway === 1 ? 'Day away' : 'Days away',
    },
    { value: formatKm(summary.distanceKm).replace(' km', ''), unit: 'km', label: 'Distance' },
  ];
  return (
    <section aria-labelledby="insights-totals">
      <h2 className="visually-hidden" id="insights-totals">
        Your travel in numbers
      </h2>
      <ul className="stats">
        {tiles.map((tile) => (
          <li key={tile.label} className="stat">
            <span className="stat__value">
              {tile.value}
              {tile.unit && <span className="stat__unit"> {tile.unit}</span>}
            </span>
            <span className="stat__label">{tile.label}</span>
            {tile.detail && <span className="stat__detail">{tile.detail}</span>}
          </li>
        ))}
      </ul>
    </section>
  );
}
