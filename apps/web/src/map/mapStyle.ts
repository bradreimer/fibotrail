import type { StyleSpecification } from 'maplibre-gl';

/** The colors of the base map, read from the design tokens. */
export interface MapColors {
  water: string;
  land: string;
  border: string;
  route: string;
  routeOutline: string;
  /** A GPS trail. It differs from the photo route, which is Trail Teal. */
  trail: string;
  /** A photo placed on a GPS trail by its time. */
  photo: string;
  /** The fill of a country you've visited. */
  visited: string;
}

/** Reads the map colors from the tokens, so the map follows light and dark mode. */
export function readMapColors(element: Element, dark: boolean): MapColors {
  const style = getComputedStyle(element);
  const token = (name: string) => style.getPropertyValue(name).trim();
  // Light: pale Mist water and Paper land. Dark: Deep Ocean water and Night land.
  return {
    water: dark ? token('--fibo-ocean') : token('--fibo-mist'),
    land: token('--fibo-paper'),
    border: dark ? token('--fibo-mist') : token('--fibo-map-border'),
    route: token('--fibo-teal'),
    routeOutline: token('--fibo-paper'),
    trail: dark ? token('--fibo-gold') : token('--fibo-ocean'),
    photo: token('--fibo-coral'),
    visited: token('--fibo-meadow'),
  };
}

export const LAND_SOURCE = 'land';
export const ROUTE_SOURCE = 'route';
export const TRAIL_SOURCE = 'trails';
export const TRAIL_LAYER = 'trail';
export const TRAIL_PHOTO_SOURCE = 'trail-photos';
export const TRAIL_PHOTO_LAYER = 'trail-photos';
export const REGION_SOURCE = 'regions';
export const VISITED_LAYER = 'visited';
/** The visited detail countries, filled whole only until the regions are drawn. */
export const DETAIL_VISITED_LAYER = 'visited-detail';
export const REGION_VISITED_LAYER = 'region-visited';
export const REGION_BORDERS_LAYER = 'region-borders';
export const CITY_SOURCE = 'cities';
export const CITY_LAYER = 'city-pins';
export const PARK_LAYER = 'park-pins';

/**
 * From this zoom up, a pin marks each city and theme park you've been to. A pin is about a
 * neighborhood wide at this zoom, so merged cities stay one pin. See ADR-015.
 */
export const CITY_MIN_ZOOM = 4.5;

/**
 * From this zoom up, the regions of a detail country are drawn: a Canada or USA fills by province,
 * territory, or state. Below it, the country fills as a whole. Canada is about 700 px wide at this zoom, wider than a phone.
 */
export const REGION_MIN_ZOOM = 2.5;

/**
 * Matches the countries whose key is in the list. A country's key is its ISO code, or its name
 * when it has none, so the screen and the map agree on which outline is which. An empty list
 * matches nothing.
 */
export function visitedFilter(keys: readonly string[]): ExpressionFilter {
  return ['in', ['get', 'key'], ['literal', keys.length > 0 ? [...keys] : ['']]];
}

type ExpressionFilter = ['in', ['get', string], ['literal', string[]]];

/**
 * The quiet base map: country outlines from Natural Earth, and nothing else. It has no labels,
 * so it needs no font files, and no tiles, so the browser asks no other host for anything. See
 * ADR-009.
 */
export function baseMapStyle(colors: MapColors): StyleSpecification {
  return {
    version: 8,
    sources: {
      [LAND_SOURCE]: { type: 'geojson', data: { type: 'FeatureCollection', features: [] } },
      [ROUTE_SOURCE]: { type: 'geojson', data: { type: 'FeatureCollection', features: [] } },
      [TRAIL_SOURCE]: { type: 'geojson', data: { type: 'FeatureCollection', features: [] } },
      [TRAIL_PHOTO_SOURCE]: {
        type: 'geojson',
        data: { type: 'FeatureCollection', features: [] },
      },
      [REGION_SOURCE]: { type: 'geojson', data: { type: 'FeatureCollection', features: [] } },
      [CITY_SOURCE]: { type: 'geojson', data: { type: 'FeatureCollection', features: [] } },
    },
    layers: [
      { id: 'water', type: 'background', paint: { 'background-color': colors.water } },
      { id: 'land', type: 'fill', source: LAND_SOURCE, paint: { 'fill-color': colors.land } },
      {
        id: VISITED_LAYER,
        type: 'fill',
        source: LAND_SOURCE,
        filter: visitedFilter([]),
        paint: { 'fill-color': colors.visited },
      },
      {
        // A layer's zoom range is exact. A zoom in a data expression is read per tile, in whole zooms.
        id: DETAIL_VISITED_LAYER,
        type: 'fill',
        source: LAND_SOURCE,
        maxzoom: REGION_MIN_ZOOM,
        filter: visitedFilter([]),
        paint: { 'fill-color': colors.visited },
      },
      {
        id: REGION_VISITED_LAYER,
        type: 'fill',
        source: REGION_SOURCE,
        minzoom: REGION_MIN_ZOOM,
        filter: visitedFilter([]),
        paint: { 'fill-color': colors.visited },
      },
      {
        id: 'borders',
        type: 'line',
        source: LAND_SOURCE,
        paint: { 'line-color': colors.border, 'line-width': 1 },
      },
      {
        id: REGION_BORDERS_LAYER,
        type: 'line',
        source: REGION_SOURCE,
        minzoom: REGION_MIN_ZOOM,
        paint: { 'line-color': colors.border, 'line-width': 0.75 },
      },
      {
        id: CITY_LAYER,
        type: 'circle',
        source: CITY_SOURCE,
        minzoom: CITY_MIN_ZOOM,
        filter: ['!=', ['get', 'isPark'], true],
        paint: {
          'circle-radius': 6,
          'circle-color': colors.route,
          'circle-stroke-color': colors.routeOutline,
          'circle-stroke-width': 2,
        },
      },
      {
        id: PARK_LAYER,
        type: 'circle',
        source: CITY_SOURCE,
        minzoom: CITY_MIN_ZOOM,
        filter: ['==', ['get', 'isPark'], true],
        paint: {
          'circle-radius': 6,
          'circle-color': colors.visited,
          'circle-stroke-color': colors.routeOutline,
          'circle-stroke-width': 2,
        },
      },
      // A GPS trail lies under the photo route and the pins.
      {
        id: TRAIL_LAYER,
        type: 'line',
        source: TRAIL_SOURCE,
        layout: { 'line-cap': 'round', 'line-join': 'round' },
        paint: { 'line-color': colors.trail, 'line-width': 3 },
      },
      {
        id: 'route-outline',
        type: 'line',
        source: ROUTE_SOURCE,
        layout: { 'line-cap': 'round', 'line-join': 'round' },
        paint: { 'line-color': colors.routeOutline, 'line-width': 8 },
      },
      {
        id: 'route',
        type: 'line',
        source: ROUTE_SOURCE,
        layout: { 'line-cap': 'round', 'line-join': 'round' },
        paint: { 'line-color': colors.route, 'line-width': 4 },
      },
      // Photos placed on a GPS trail by time. A photo with no position of its own is hollow.
      {
        id: TRAIL_PHOTO_LAYER,
        type: 'circle',
        source: TRAIL_PHOTO_SOURCE,
        paint: {
          'circle-radius': 5,
          'circle-color': ['case', ['get', 'estimated'], colors.routeOutline, colors.photo],
          'circle-stroke-color': colors.photo,
          'circle-stroke-width': 2,
        },
      },
    ],
  };
}

/** A photo placed on a GPS trail by its time. `estimated` is true when the trail gives its only position. */
export interface PhotoDot extends Position {
  id: string;
  estimated: boolean;
  /** What a screen reader and the test map say for the dot. */
  label: string;
}

/** The photo dots as GeoJSON, with the ID and whether the position is estimated in each feature. */
export function photoDotsGeoJson(dots: readonly PhotoDot[]): GeoJSON.FeatureCollection {
  return {
    type: 'FeatureCollection',
    features: dots.map((dot) => ({
      type: 'Feature',
      properties: { id: dot.id, estimated: dot.estimated },
      geometry: { type: 'Point', coordinates: [dot.longitude, dot.latitude] },
    })),
  };
}

/** One place on the zoomed-in map. `key` ties a pin to its row in the list. */
export interface CityPin extends Position {
  key: string;
  isPark: boolean;
}

/** The pins as GeoJSON, with the key and the kind in each feature's properties. */
export function citiesGeoJson(cities: readonly CityPin[]): GeoJSON.FeatureCollection {
  return {
    type: 'FeatureCollection',
    features: cities.map((city) => ({
      type: 'Feature',
      properties: { key: city.key, isPark: city.isPark },
      geometry: { type: 'Point', coordinates: [city.longitude, city.latitude] },
    })),
  };
}

/** A line through each list of points, in order, as GeoJSON. Fewer than two points give no line. */
export function routesGeoJson(routes: readonly (readonly Position[])[]): GeoJSON.FeatureCollection {
  return {
    type: 'FeatureCollection',
    features: routes
      .filter((points) => points.length >= 2)
      .map((points) => ({
        type: 'Feature' as const,
        properties: {},
        geometry: {
          type: 'LineString' as const,
          coordinates: points.map((point) => [point.longitude, point.latitude]),
        },
      })),
  };
}

/** A line through the points, in order, as GeoJSON. Fewer than two points give no line. */
export function routeGeoJson(points: readonly Position[]): GeoJSON.FeatureCollection {
  return routesGeoJson([points]);
}

export interface Position {
  latitude: number;
  longitude: number;
}

/** The box that holds every point, as west, south, east, north. Null with no points. */
export function boundsOf(points: readonly Position[]): [number, number, number, number] | null {
  if (points.length === 0) return null;
  const latitudes = points.map((point) => point.latitude);
  const longitudes = points.map((point) => point.longitude);
  return [
    Math.min(...longitudes),
    Math.min(...latitudes),
    Math.max(...longitudes),
    Math.max(...latitudes),
  ];
}
