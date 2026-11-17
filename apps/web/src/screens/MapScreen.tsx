import { useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { CollapsibleSection } from '../map/CollapsibleSection';
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
  const [openSections, setOpenSections] = useState<ReadonlySet<string>>(new Set());
  const pendingJump = useRef<string | null>(null);

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

  // After a link opens a section, bring it into view and put focus on its summary.
  useEffect(() => {
    if (pendingJump.current === null) return;
    const section = document.getElementById(pendingJump.current);
    pendingJump.current = null;
    section?.scrollIntoView?.({ block: 'start' });
    section?.querySelector('summary')?.focus();
  });

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
  const isOpen = (sectionId: string) => openSections.has(sectionId);
  const toggleSection = (sectionId: string, open: boolean) =>
    setOpenSections((current) => {
      if (current.has(sectionId) === open) return current;
      const next = new Set(current);
      if (open) next.add(sectionId);
      else next.delete(sectionId);
      return next;
    });
  const jumpToSection = (sectionId: string) => {
    pendingJump.current = sectionId;
    // Opening a section that is closed renders again, and the effect above then moves there. A
    // section that is already open needs a render too, so the effect runs.
    setOpenSections((current) => new Set(current).add(sectionId));
  };
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
      <RegionProgress countries={data.detailCountries} onJump={jumpToSection} />
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
      <CollapsibleSection
        id="first-visits"
        title="First visits"
        count={String(data.countries.length)}
        open={isOpen('first-visits')}
        onToggle={(open) => toggleSection('first-visits', open)}
      >
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
      </CollapsibleSection>
      <RegionLists
        countries={data.detailCountries}
        isOpen={isOpen}
        onToggle={toggleSection}
        onSelect={(item) => selectRegion(item.id)}
      />
      {data.cities.length > 0 && (
        <CollapsibleSection
          id="visited-places"
          title="Cities and parks"
          count={String(data.cities.length)}
          open={isOpen('visited-places')}
          onToggle={(open) => toggleSection('visited-places', open)}
        >
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
        </CollapsibleSection>
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
