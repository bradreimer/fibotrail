import 'maplibre-gl/dist/maplibre-gl.css';

import { Map as MapLibreMap, Marker, setWorkerUrl, type GeoJSONSource } from 'maplibre-gl';
import workerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?url';
import { useEffect, useRef } from 'react';
import { feature } from 'topojson-client';
import type { Topology } from 'topojson-specification';
import countriesUrl from 'world-atlas/countries-50m.json?url';

import {
  LAND_SOURCE,
  ROUTE_SOURCE,
  baseMapStyle,
  boundsOf,
  readMapColors,
  routeGeoJson,
  type Position,
} from './mapStyle';

// The worker is a file of this app. Without this, MapLibre builds one from a blob address, which
// the Content Security Policy doesn't allow.
setWorkerUrl(workerUrl);

export interface MapMarker extends Position {
  id: string;
  /** "home" and "start" are gold dots. "stop" is a coral pin, with a photo count when it has one. */
  kind: 'home' | 'start' | 'stop';
  /** What a screen reader says for the marker. */
  label: string;
  count?: number;
}

export interface MapViewProps {
  /** What the map shows, for screen readers. */
  label: string;
  markers: readonly MapMarker[];
  /** The points of the route, in order. */
  route?: readonly Position[];
  /** Called when the person drags the marker with this ID. Only that marker can be dragged. */
  draggableId?: string;
  onDrag?: (position: Position) => void;
  onSelect?: (id: string) => void;
  /** False for a small preview that doesn't pan or zoom. */
  interactive?: boolean;
}

/** The zoom for a single point: about 2,000 km across on a phone. */
const SINGLE_POINT_ZOOM = 4;

let countries: Promise<GeoJSON.FeatureCollection> | null = null;

/** Loads the country outlines once. The file is part of the app, so no other host is asked. */
function loadCountries(): Promise<GeoJSON.FeatureCollection> {
  countries ??= fetch(countriesUrl)
    .then((response) => response.json() as Promise<Topology>)
    .then(
      (topology) =>
        feature(topology, topology.objects.countries!) as unknown as GeoJSON.FeatureCollection,
    );
  return countries;
}

function markerElement(marker: MapMarker, onSelect?: (id: string) => void): HTMLElement {
  // Only a stop opens its photos. Home and the start of a route are marks, not controls.
  if (marker.kind !== 'stop') onSelect = undefined;
  const element = document.createElement(onSelect ? 'button' : 'div');
  element.className = `map-marker map-marker--${marker.kind}`;
  element.setAttribute('aria-label', marker.label);
  if (onSelect) {
    element.setAttribute('type', 'button');
    element.addEventListener('click', (event) => {
      event.stopPropagation();
      onSelect(marker.id);
    });
  } else {
    element.setAttribute('role', 'img');
  }
  if (marker.count !== undefined && marker.count > 1) element.textContent = String(marker.count);
  return element;
}

/**
 * A map drawn by MapLibre GL JS on the bundled base map. It's loaded only when a screen shows a
 * map, because MapLibre is the largest part of the app.
 */
export default function MapView({
  label,
  markers,
  route = [],
  draggableId,
  onDrag,
  onSelect,
  interactive = true,
}: MapViewProps) {
  const container = useRef<HTMLDivElement>(null);
  const map = useRef<MapLibreMap | null>(null);
  const shown = useRef<Marker[]>([]);
  const handlers = useRef({ onDrag, onSelect });
  useEffect(() => {
    handlers.current = { onDrag, onSelect };
  });

  // Create the map once.
  useEffect(() => {
    const element = container.current!;
    const scheme = window.matchMedia('(prefers-color-scheme: dark)');
    const created = new MapLibreMap({
      container: element,
      style: baseMapStyle(readMapColors(element, scheme.matches)),
      center: [0, 20],
      zoom: 1,
      maxZoom: 12,
      interactive,
      attributionControl: false,
      dragRotate: false,
      pitchWithRotate: false,
      touchPitch: false,
    });
    created.touchZoomRotate.disableRotation();
    map.current = created;

    created.on('load', () => {
      void loadCountries().then((land) => {
        (created.getSource(LAND_SOURCE) as GeoJSONSource | undefined)?.setData(land);
      });
    });

    // The map follows the system light and dark setting without a reload. Only the colors
    // change, so the outlines, the route, and the markers stay where they are.
    const recolor = () => {
      const colors = readMapColors(element, scheme.matches);
      created.setPaintProperty('water', 'background-color', colors.water);
      created.setPaintProperty('land', 'fill-color', colors.land);
      created.setPaintProperty('borders', 'line-color', colors.border);
      created.setPaintProperty('route-outline', 'line-color', colors.routeOutline);
      created.setPaintProperty('route', 'line-color', colors.route);
    };
    scheme.addEventListener('change', recolor);

    return () => {
      scheme.removeEventListener('change', recolor);
      created.remove();
      map.current = null;
    };
  }, [interactive]);

  // Draw the route and the markers, and move the view to hold them.
  useEffect(() => {
    const current = map.current;
    if (!current) return;

    const drawRoute = () =>
      (current.getSource(ROUTE_SOURCE) as GeoJSONSource | undefined)?.setData(routeGeoJson(route));
    if (current.isStyleLoaded()) drawRoute();
    else current.once('load', drawRoute);

    shown.current.forEach((marker) => marker.remove());
    shown.current = markers.map((marker) => {
      const draggable = marker.id === draggableId;
      const added = new Marker({
        // A pin stands above its place, so the gold start dot under it stays visible.
        anchor: marker.kind === 'stop' ? 'bottom' : 'center',
        element: markerElement(
          marker,
          onSelect ? (id) => handlers.current.onSelect?.(id) : undefined,
        ),
        draggable,
      })
        .setLngLat([marker.longitude, marker.latitude])
        .addTo(current);
      if (draggable) {
        added.on('dragend', () => {
          const { lat, lng } = added.getLngLat();
          handlers.current.onDrag?.({ latitude: lat, longitude: lng });
        });
      }
      return added;
    });

    const bounds = boundsOf([...markers, ...route]);
    if (bounds) {
      const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      // The outlines are drawn for a world map. One point is shown from far enough away that the
      // coast around it is recognizable, and several points may zoom in until they spread out.
      const maxZoom = bounds[0] === bounds[2] && bounds[1] === bounds[3] ? SINGLE_POINT_ZOOM : 11;
      current.fitBounds(bounds, { padding: 48, maxZoom, animate: !reduceMotion, duration: 377 });
    }
  }, [markers, route, draggableId, onSelect]);

  return <div ref={container} className="map" role="application" aria-label={label} />;
}
