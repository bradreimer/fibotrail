import type { MapViewProps } from '../map/MapView';

/**
 * Stands in for the MapLibre map in tests. jsdom has no WebGL, so the real map can't draw. This
 * shows what the map was asked to show, and a button that moves the draggable pin.
 */
export default function MapViewMock({
  label,
  markers,
  route = [],
  routes,
  visited,
  onCountrySelect,
  detailCountries,
  visitedRegions,
  onRegionSelect,
  cities,
  onCitySelect,
  draggableId,
  onDrag,
  onSelect,
}: MapViewProps) {
  return (
    <div
      role="application"
      aria-label={label}
      data-route={JSON.stringify(route)}
      data-routes={routes ? JSON.stringify(routes) : undefined}
      data-visited={visited ? JSON.stringify(visited) : undefined}
      data-detail-countries={detailCountries ? JSON.stringify(detailCountries) : undefined}
      data-visited-regions={visitedRegions ? JSON.stringify(visitedRegions) : undefined}
      data-cities={cities ? JSON.stringify(cities) : undefined}
    >
      {onCountrySelect &&
        visited?.map((key) => (
          <button key={key} type="button" onClick={() => onCountrySelect(key)}>
            {`Country on the map: ${key}`}
          </button>
        ))}
      {onRegionSelect &&
        visitedRegions?.map((key) => (
          <button key={key} type="button" onClick={() => onRegionSelect(key)}>
            {`Region on the map: ${key}`}
          </button>
        ))}
      {onCitySelect &&
        cities?.map((pin) => (
          <button key={pin.key} type="button" onClick={() => onCitySelect(pin.key)}>
            {`Pin on the map: ${pin.key}`}
          </button>
        ))}
      {markers.map((marker) => (
        <button
          key={marker.id}
          type="button"
          data-kind={marker.kind}
          data-position={`${marker.latitude},${marker.longitude}`}
          onClick={() => marker.kind === 'stop' && onSelect?.(marker.id)}
        >
          {marker.label}
        </button>
      ))}
      {draggableId && (
        <button type="button" onClick={() => onDrag?.({ latitude: -33.87, longitude: 151.21 })}>
          Drag the pin to Sydney
        </button>
      )}
    </div>
  );
}
