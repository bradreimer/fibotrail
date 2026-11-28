import type { MapViewProps } from '../map/MapView';

/**
 * Stands in for the MapLibre map in tests. jsdom has no WebGL, so the real map can't draw. This
 * shows what the map was asked to show, a button that moves the draggable pin, and buttons that
 * tap the map near Paris and in Sydney.
 */
export default function MapViewMock({
  label,
  markers,
  route = [],
  routes,
  trails,
  photoDots,
  onPhotoDotSelect,
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
  detail = 'coarse',
  onMapTap,
  fit = true,
}: MapViewProps) {
  return (
    <div
      role="application"
      aria-label={label}
      data-detail={detail}
      data-fit={String(fit)}
      data-route={JSON.stringify(route)}
      data-routes={routes ? JSON.stringify(routes) : undefined}
      data-trails={trails ? JSON.stringify(trails) : undefined}
      data-photo-dots={photoDots ? JSON.stringify(photoDots) : undefined}
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
      {photoDots?.map((dot) => (
        <button
          key={`dot-${dot.id}`}
          type="button"
          data-kind={dot.estimated ? 'estimated-photo' : 'trail-photo'}
          onClick={() => onPhotoDotSelect?.(dot.id)}
        >
          {dot.label}
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
      {onMapTap && (
        <>
          <button type="button" onClick={() => onMapTap({ latitude: 48.86, longitude: 2.35 })}>
            Tap the map near Paris
          </button>
          <button type="button" onClick={() => onMapTap({ latitude: -33.87, longitude: 151.21 })}>
            Tap the map in Sydney
          </button>
        </>
      )}
      {draggableId && (
        <button type="button" onClick={() => onDrag?.({ latitude: -33.87, longitude: 151.21 })}>
          Drag the pin to Sydney
        </button>
      )}
    </div>
  );
}
