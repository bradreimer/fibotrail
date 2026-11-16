import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { CountrySheet, type MapCountry } from '../map/CountrySheet';
import { LazyMap } from '../map/LazyMap';
import { MapSummary } from '../map/MapSummary';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { formatDateRange } from '../trips/dates';

/** The key that ties a country to its outline on the map: the ISO code, or the name when it has none. */
function keyOf(country: MapCountry): string {
  return country.id ?? country.name;
}

/**
 * The life map: the countries of your added trips filled in on a world map, with the numbers, your
 * first visit to each country, and a switch for the routes. See docs/specs/life-map.md.
 */
export function MapScreen() {
  const map = useApi('/api/map');
  const [selected, setSelected] = useState<string | null>(null);
  const [showRoutes, setShowRoutes] = useState(false);

  const data = map.data;
  const visited = useMemo(() => data?.countries.map(keyOf) ?? [], [data]);
  const routes = useMemo(
    () => (showRoutes ? (data?.routes.map((route) => route.points) ?? []) : []),
    [data, showRoutes],
  );

  if (map.loading && !data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading the map</span>
        <div className="map-skeleton">
          <span className="skeleton skeleton--text" aria-hidden="true" />
          <span className="skeleton skeleton--card" aria-hidden="true" />
        </div>
      </div>
    );
  }
  if (map.error) return <ErrorState message={map.error} onRetry={map.reload} />;
  if (!data) return null;

  if (data.countries.length === 0) {
    return (
      <EmptyState
        title="No places yet"
        message="No places yet. Add a trip, and its countries fill in."
        action={
          <Link className="button" to="/inbox">
            Open the inbox
          </Link>
        }
      />
    );
  }

  const country = data.countries.find((item) => keyOf(item) === selected) ?? null;
  return (
    <div className="life-map">
      <MapSummary totals={data.totals} />
      <LazyMap
        className="life-map__map"
        label="World map. The countries you've visited are filled in green."
        markers={[]}
        visited={visited}
        routes={routes}
        onCountrySelect={setSelected}
      />
      <label className="setting-row">
        <span>Show routes</span>
        <input
          type="checkbox"
          role="switch"
          className="switch"
          checked={showRoutes}
          onChange={(event) => setShowRoutes(event.target.checked)}
        />
      </label>
      <section aria-labelledby="first-visits">
        <h3 className="life-map__heading" id="first-visits">
          First visits
        </h3>
        <ol className="first-visits">
          {data.countries.map((item) => (
            <li key={keyOf(item)}>
              <button
                type="button"
                className="first-visits__item"
                onClick={() => setSelected(keyOf(item))}
              >
                <span className="first-visits__name">{item.name}</span>
                <span className="first-visits__date">
                  {formatDateRange(item.firstVisit, item.firstVisit)}
                </span>
              </button>
            </li>
          ))}
        </ol>
      </section>
      {country && <CountrySheet country={country} onClose={() => setSelected(null)} />}
    </div>
  );
}
