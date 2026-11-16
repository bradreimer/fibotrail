import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { CountrySheet, type MapCountry } from '../map/CountrySheet';
import { RegionLists, RegionProgress } from '../map/DetailRegions';
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
  const [selectedRegion, setSelectedRegion] = useState<string | null>(null);
  const [selectedCity, setSelectedCity] = useState<string | null>(null);
  const [showRoutes, setShowRoutes] = useState(false);

  const data = map.data;
  const visited = useMemo(() => data?.countries.map(keyOf) ?? [], [data]);
  const detailKeys = useMemo(() => data?.detailCountries.map((item) => item.id) ?? [], [data]);
  const visitedRegions = useMemo(
    () => data?.detailCountries.flatMap((item) => item.regions.map((region) => region.id)) ?? [],
    [data],
  );
  const pins = useMemo(
    () =>
      data?.cities.map((city, index) => ({
        key: String(index),
        latitude: city.latitude,
        longitude: city.longitude,
        isPark: city.isPark,
      })) ?? [],
    [data],
  );
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
  const region =
    data.detailCountries
      .flatMap((item) => item.regions)
      .find((item) => item.id === selectedRegion) ?? null;
  const hasDetail = data.detailCountries.length > 0;
  const city = selectedCity === null ? null : (data.cities[Number(selectedCity)] ?? null);
  const selectCountry = (key: string | null) => {
    setSelectedRegion(null);
    setSelectedCity(null);
    setSelected(key);
  };
  const selectRegion = (key: string | null) => {
    setSelected(null);
    setSelectedCity(null);
    setSelectedRegion(key);
  };
  const selectCity = (key: string | null) => {
    setSelected(null);
    setSelectedRegion(null);
    setSelectedCity(key);
  };
  return (
    <div className="life-map">
      <MapSummary totals={data.totals} />
      <RegionProgress countries={data.detailCountries} />
      <LazyMap
        className="life-map__map"
        label={`World map. The countries you've visited are filled in green.${hasDetail ? " Zoom in to a country with regions to see the ones you've visited." : ''}`}
        markers={[]}
        visited={visited}
        detailCountries={hasDetail ? detailKeys : undefined}
        visitedRegions={hasDetail ? visitedRegions : undefined}
        routes={routes}
        onCountrySelect={selectCountry}
        cities={pins}
        onCitySelect={selectCity}
        onRegionSelect={selectRegion}
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
                onClick={() => selectCountry(keyOf(item))}
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
      <RegionLists countries={data.detailCountries} onSelect={(item) => selectRegion(item.id)} />
      {data.cities.length > 0 && (
        <section aria-labelledby="visited-places">
          <h3 className="life-map__heading" id="visited-places">
            Cities and parks
          </h3>
          <ol className="first-visits">
            {data.cities.map((item, index) => (
              <li key={`${item.name}-${index}`}>
                <button
                  type="button"
                  className="first-visits__item"
                  onClick={() => selectCity(String(index))}
                >
                  <span className="first-visits__name">
                    {item.name}
                    {item.isPark && <span className="visually-hidden"> (theme park)</span>}
                  </span>
                  <span className="first-visits__date">
                    {item.isPark ? 'Park · ' : ''}
                    {formatDateRange(item.firstVisit, item.firstVisit)}
                  </span>
                </button>
              </li>
            ))}
          </ol>
        </section>
      )}
      {country && <CountrySheet country={country} onClose={() => setSelected(null)} />}
      {city && (
        <CountrySheet
          country={{ id: null, name: city.name, firstVisit: city.firstVisit, trips: city.trips }}
          onClose={() => setSelectedCity(null)}
        />
      )}
      {region && <CountrySheet country={region} onClose={() => setSelectedRegion(null)} />}
    </div>
  );
}
