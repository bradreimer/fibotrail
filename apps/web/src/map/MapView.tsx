import 'maplibre-gl/dist/maplibre-gl.css';

import { Map as MapLibreMap, Marker, setWorkerUrl, type GeoJSONSource } from 'maplibre-gl';
import workerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?url';
import { useEffect, useRef } from 'react';

import regionsUrl from './regions.json?url';

import { loadCountries, type OutlineDetail } from './countryOutlines';
import {
  CITY_LAYER,
  CITY_SOURCE,
  citiesGeoJson,
  type CityPin,
  LAND_SOURCE,
  PARK_LAYER,
  REGION_BORDERS_LAYER,
  REGION_SOURCE,
  REGION_VISITED_LAYER,
  ROUTE_SOURCE,
  TRAIL_LAYER,
  TRAIL_PHOTO_LAYER,
  TRAIL_PHOTO_SOURCE,
  TRAIL_SOURCE,
  photoDotsGeoJson,
  type PhotoDot,
  baseMapStyle,
  boundsOf,
  DETAIL_VISITED_LAYER,
  readMapColors,
  VISITED_LAYER,
  routesGeoJson,
  visitedFilter,
  type Position,
} from './mapStyle';

// The worker is a file of this app. Without this, MapLibre builds one from a blob address, which
// the Content Security Policy doesn't allow.
setWorkerUrl(workerUrl);

export interface MapMarker extends Position {
  id: string;
  /**
   * "home" and "start" are gold dots. "stop" is a coral pin, with a photo count when it has one.
   * The Pin game's "guess" is a coral dot, "past" a dimmed one, and "answer" a meadow dot.
   */
  kind: 'home' | 'start' | 'stop' | 'guess' | 'past' | 'answer';
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
  /** Several routes at once, such as every trip on the life map. Used instead of `route` when given. */
  routes?: readonly (readonly Position[])[];
  /**
   * GPS trails, each a list of points in order. Each list is drawn as its own line, so a gap in a
   * trail is never crossed. The view fits them with the markers and the route.
   */
  trails?: readonly (readonly Position[])[];
  /** Photos placed on the trails by time. They're drawn as dots, not markers, because there can be hundreds. */
  photoDots?: readonly PhotoDot[];
  /** Called with a photo's ID when the person taps its dot. */
  onPhotoDotSelect?: (id: string) => void;
  /** The keys of the countries to fill as visited: the ISO code, or the name when a country has none. */
  visited?: readonly string[];
  /** Called with the key of a visited country when the person taps it. */
  onCountrySelect?: (key: string) => void;
  /**
   * The keys of the countries that also show their regions (Canada and the USA). Giving it makes
   * the map load the region outlines, which the other maps don't need.
   */
  detailCountries?: readonly string[];
  /** The ISO 3166-2 codes of the regions to fill as visited, such as "CA-ON". */
  visitedRegions?: readonly string[];
  /** Called with the code of a visited region when the person taps it. */
  onRegionSelect?: (key: string) => void;
  /** The cities and parks to pin once the map is zoomed in. */
  cities?: readonly CityPin[];
  /** Called with the key of a pin when the person taps it. */
  onCitySelect?: (key: string) => void;
  /** Called when the person drags the marker with this ID. Only that marker can be dragged. */
  draggableId?: string;
  onDrag?: (position: Position) => void;
  onSelect?: (id: string) => void;
  /** False for a small preview that doesn't pan or zoom. */
  interactive?: boolean;
  /** How detailed the country outline is. `fine` is for the trip page (ADR-020). */
  detail?: OutlineDetail;
  /**
   * Called with the point the person taps, anywhere on the map. A press that moves more than
   * `TAP_TOLERANCE_PX` is a pan, not a tap.
   */
  onMapTap?: (position: Position) => void;
  /** False to keep the view where the person left it when the markers change. */
  fit?: boolean;
}

/** How far a press may move and still be a tap, for `onMapTap`. MapLibre's own default is 3 px. */
export const TAP_TOLERANCE_PX = 8;

/** The zoom for a single point: about 2,000 km across on a phone. */
const SINGLE_POINT_ZOOM = 4;

let regions: Promise<GeoJSON.FeatureCollection> | null = null;

/** Loads the region outlines once, for the maps that show regions. Part of the app, like the countries. */
function loadRegions(): Promise<GeoJSON.FeatureCollection> {
  regions ??= fetch(regionsUrl).then(
    (response) => response.json() as Promise<GeoJSON.FeatureCollection>,
  );
  return regions;
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
  interactive = true,
  detail = 'coarse',
  onMapTap,
  fit = true,
}: MapViewProps) {
  const container = useRef<HTMLDivElement>(null);
  const map = useRef<MapLibreMap | null>(null);
  const shown = useRef<Marker[]>([]);
  const handlers = useRef({
    onDrag,
    onSelect,
    onCountrySelect,
    onRegionSelect,
    onCitySelect,
    onMapTap,
    onPhotoDotSelect,
  });
  const wantsRegions = detailCountries !== undefined;
  const wantsTaps = onMapTap !== undefined;
  useEffect(() => {
    handlers.current = {
      onDrag,
      onSelect,
      onCountrySelect,
      onRegionSelect,
      onCitySelect,
      onMapTap,
      onPhotoDotSelect,
    };
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
      ...(wantsTaps ? { clickTolerance: TAP_TOLERANCE_PX } : {}),
    });
    created.touchZoomRotate.disableRotation();
    map.current = created;

    created.on('load', () => {
      void loadCountries(detail).then((land) => {
        (created.getSource(LAND_SOURCE) as GeoJSONSource | undefined)?.setData(land);
      });
      if (wantsRegions) {
        void loadRegions().then((land) => {
          (created.getSource(REGION_SOURCE) as GeoJSONSource | undefined)?.setData(land);
        });
      }
    });

    // A tap anywhere reports its point, for a map you place a pin on.
    if (interactive && wantsTaps) {
      created.on('click', (event) => {
        handlers.current.onMapTap?.({ latitude: event.lngLat.lat, longitude: event.lngLat.lng });
      });
    }

    // A visited country opens its sheet. The pointer shows that it can be tapped.
    if (interactive) {
      // A detail country is filled whole only below the zoom where its regions draw, so a tap on
      // a zoomed-in detail country reaches the region layer, and an unvisited region does nothing.
      for (const layer of [VISITED_LAYER, DETAIL_VISITED_LAYER]) {
        created.on('click', layer, (event) => {
          const key = event.features?.[0]?.properties?.key as string | undefined;
          if (key) handlers.current.onCountrySelect?.(key);
        });
      }
      created.on('click', REGION_VISITED_LAYER, (event) => {
        const key = event.features?.[0]?.properties?.key as string | undefined;
        if (key) handlers.current.onRegionSelect?.(key);
      });
      created.on('click', TRAIL_PHOTO_LAYER, (event) => {
        const id = event.features?.[0]?.properties?.id as string | undefined;
        if (id) handlers.current.onPhotoDotSelect?.(id);
      });
      for (const layer of [CITY_LAYER, PARK_LAYER]) {
        created.on('click', layer, (event) => {
          const key = event.features?.[0]?.properties?.key as string | undefined;
          if (key) handlers.current.onCitySelect?.(key);
        });
      }
      for (const layer of [
        VISITED_LAYER,
        DETAIL_VISITED_LAYER,
        REGION_VISITED_LAYER,
        CITY_LAYER,
        PARK_LAYER,
        TRAIL_PHOTO_LAYER,
      ]) {
        created.on('mouseenter', layer, () => {
          created.getCanvas().style.cursor = 'pointer';
        });
        created.on('mouseleave', layer, () => {
          created.getCanvas().style.cursor = '';
        });
      }
    }

    // The map follows the system light and dark setting without a reload. Only the colors
    // change, so the outlines, the route, and the markers stay where they are.
    const recolor = () => {
      const colors = readMapColors(element, scheme.matches);
      created.setPaintProperty('water', 'background-color', colors.water);
      created.setPaintProperty('land', 'fill-color', colors.land);
      created.setPaintProperty(VISITED_LAYER, 'fill-color', colors.visited);
      created.setPaintProperty(DETAIL_VISITED_LAYER, 'fill-color', colors.visited);
      created.setPaintProperty(REGION_VISITED_LAYER, 'fill-color', colors.visited);
      created.setPaintProperty('borders', 'line-color', colors.border);
      created.setPaintProperty(REGION_BORDERS_LAYER, 'line-color', colors.border);
      created.setPaintProperty('route-outline', 'line-color', colors.routeOutline);
      created.setPaintProperty('route', 'line-color', colors.route);
      created.setPaintProperty(TRAIL_LAYER, 'line-color', colors.trail);
      created.setPaintProperty(TRAIL_PHOTO_LAYER, 'circle-stroke-color', colors.photo);
      created.setPaintProperty(TRAIL_PHOTO_LAYER, 'circle-color', [
        'case',
        ['get', 'estimated'],
        colors.routeOutline,
        colors.photo,
      ]);
      created.setPaintProperty(CITY_LAYER, 'circle-color', colors.route);
      created.setPaintProperty(CITY_LAYER, 'circle-stroke-color', colors.routeOutline);
      created.setPaintProperty(PARK_LAYER, 'circle-color', colors.visited);
      created.setPaintProperty(PARK_LAYER, 'circle-stroke-color', colors.routeOutline);
    };
    scheme.addEventListener('change', recolor);

    return () => {
      scheme.removeEventListener('change', recolor);
      created.remove();
      map.current = null;
    };
  }, [interactive, wantsRegions, wantsTaps, detail]);

  // Fill the visited countries. A detail country fills whole only until its regions are drawn.
  useEffect(() => {
    const current = map.current;
    if (!current) return;
    const detailSet = new Set(detailCountries ?? []);
    const all = visited ?? [];
    const apply = () => {
      current.setFilter(VISITED_LAYER, visitedFilter(all.filter((key) => !detailSet.has(key))));
      current.setFilter(
        DETAIL_VISITED_LAYER,
        visitedFilter(all.filter((key) => detailSet.has(key))),
      );
    };
    if (current.isStyleLoaded()) apply();
    else current.once('load', apply);
  }, [visited, detailCountries]);

  // Fill the visited regions.
  useEffect(() => {
    const current = map.current;
    if (!current) return;
    const apply = () =>
      current.setFilter(REGION_VISITED_LAYER, visitedFilter(visitedRegions ?? []));
    if (current.isStyleLoaded()) apply();
    else current.once('load', apply);
  }, [visitedRegions]);

  // Pin the cities and parks. They only draw once the map is zoomed in.
  useEffect(() => {
    const current = map.current;
    if (!current) return;
    const apply = () =>
      (current.getSource(CITY_SOURCE) as GeoJSONSource | undefined)?.setData(
        citiesGeoJson(cities ?? []),
      );
    if (current.isStyleLoaded()) apply();
    else current.once('load', apply);
  }, [cities]);

  // Draw the photo dots. They don't move the view: they lie on the trails, which already do.
  useEffect(() => {
    const current = map.current;
    if (!current) return;
    const apply = () =>
      (current.getSource(TRAIL_PHOTO_SOURCE) as GeoJSONSource | undefined)?.setData(
        photoDotsGeoJson(photoDots ?? []),
      );
    if (current.isStyleLoaded()) apply();
    else current.once('load', apply);
  }, [photoDots]);

  // Draw the route and the markers, and move the view to hold them.
  useEffect(() => {
    const current = map.current;
    if (!current) return;

    const drawRoute = () => {
      (current.getSource(ROUTE_SOURCE) as GeoJSONSource | undefined)?.setData(
        routesGeoJson(routes ?? [route]),
      );
      (current.getSource(TRAIL_SOURCE) as GeoJSONSource | undefined)?.setData(
        routesGeoJson(trails ?? []),
      );
    };
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

    const bounds = fit ? boundsOf([...markers, ...route, ...(trails ?? []).flat()]) : null;
    if (bounds) {
      const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      // The outlines are drawn for a world map. One point is shown from far enough away that the
      // coast around it is recognizable, and several points may zoom in until they spread out.
      const maxZoom = bounds[0] === bounds[2] && bounds[1] === bounds[3] ? SINGLE_POINT_ZOOM : 11;
      current.fitBounds(bounds, { padding: 48, maxZoom, animate: !reduceMotion, duration: 377 });
    }
  }, [markers, route, routes, trails, draggableId, onSelect, fit]);

  return <div ref={container} className="map" role="application" aria-label={label} />;
}
