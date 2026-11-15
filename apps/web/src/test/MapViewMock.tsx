import type { MapViewProps } from '../map/MapView';

/**
 * Stands in for the MapLibre map in tests. jsdom has no WebGL, so the real map can't draw. This
 * shows what the map was asked to show, and a button that moves the draggable pin.
 */
export default function MapViewMock({
  label,
  markers,
  route = [],
  draggableId,
  onDrag,
  onSelect,
}: MapViewProps) {
  return (
    <div role="application" aria-label={label} data-route={JSON.stringify(route)}>
      {markers.map((marker) => (
        <button
          key={marker.id}
          type="button"
          data-kind={marker.kind}
          data-position={`${marker.latitude},${marker.longitude}`}
          onClick={() => onSelect?.(marker.id)}
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
