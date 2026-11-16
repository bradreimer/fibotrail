import { Link } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { ProgressRing } from '../stats/ProgressRing';
import { ViewSlot } from '../stats/ViewSlot';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { formatKm } from '../trip/format';

const number = new Intl.NumberFormat('en-US');

/**
 * The Stats screen: summary tiles, the continents, and then the views in the Stats slot. The tiles
 * and continents belong to the host, so the screen is never empty. See docs/specs/stats-screen.md.
 */
export function StatsScreen() {
  const summary = useApi('/api/stats/summary');

  if (summary.loading && !summary.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading your stats</span>
        <span className="skeleton skeleton--text" aria-hidden="true" />
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (summary.error) return <ErrorState message={summary.error} onRetry={summary.reload} />;
  const data = summary.data;
  if (!data) return null;

  if (data.trips === 0) {
    return (
      <EmptyState
        title="No stats yet"
        message="No stats yet. Add a trip, and the numbers fill in."
        action={
          <Link className="button" to="/inbox">
            Open the inbox
          </Link>
        }
      />
    );
  }

  const share = data.outlineCountries > 0 ? (data.countries / data.outlineCountries) * 100 : 0;
  const tiles = [
    { value: number.format(data.trips), label: data.trips === 1 ? 'Trip' : 'Trips' },
    {
      value: number.format(data.countries),
      label: data.countries === 1 ? 'Country' : 'Countries',
      detail: `${data.countries} / ${data.outlineCountries} · ${share.toFixed(1)}%`,
    },
    { value: number.format(data.cities), label: data.cities === 1 ? 'City' : 'Cities' },
    {
      value: `${data.continents} / 7`,
      label: data.continents === 1 ? 'Continent' : 'Continents',
    },
    { value: number.format(data.daysAway), label: data.daysAway === 1 ? 'Day away' : 'Days away' },
    { value: formatKm(data.distanceKm).replace(' km', ''), unit: 'km', label: 'Distance' },
  ];
  return (
    <div className="stats-screen">
      <section aria-labelledby="stats-numbers">
        <h3 className="visually-hidden" id="stats-numbers">
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
              {tile.detail && <span className="stat__detail">{tile.detail}</span>}
            </li>
          ))}
        </ul>
      </section>
      <section aria-labelledby="stats-continents">
        <h3 className="stats-view__title" id="stats-continents">
          Continents
        </h3>
        <ul className="continents">
          {data.continentCards.map((card) => (
            <li key={card.id}>
              <Link
                className={`continent${card.visited === 0 ? ' continent--none' : ''}`}
                to={`/stats/continents/${card.id}`}
              >
                <ProgressRing visited={card.visited} total={card.total} />
                <span className="continent__name">{card.name}</span>
                <span className="continent__count">
                  {card.visited} / {card.total}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </section>
      <ViewSlot slot="stats" />
    </div>
  );
}
