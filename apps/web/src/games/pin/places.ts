import regionsUrl from '../../map/regions.json?url';

import { loadCountries } from '../../map/countryOutlines';
import type { Position } from '../../map/mapStyle';

/** A place you can pick from a list instead of tapping the map. */
export interface PickPlace {
  key: string;
  name: string;
  /** Where the pin goes: the center of the place's largest piece of land. */
  center: Position;
  /** The regions of a country that has them on the map: Canada and the USA. */
  regions: PickPlace[];
}

type Ring = GeoJSON.Position[];

/** The rings of the outer edges of a polygon or multipolygon. */
function outerRings(geometry: GeoJSON.Geometry | null): Ring[] {
  if (!geometry) return [];
  if (geometry.type === 'Polygon') return geometry.coordinates[0] ? [geometry.coordinates[0]] : [];
  if (geometry.type === 'MultiPolygon')
    return geometry.coordinates.flatMap((polygon) => (polygon[0] ? [polygon[0]] : []));
  return [];
}

/**
 * The center of the bounding box of the largest piece, by bounding-box area. The largest piece
 * keeps an island or an overseas territory from pulling the pin into the sea.
 */
export function centerOf(geometry: GeoJSON.Geometry | null): Position | null {
  let best: { area: number; center: Position } | null = null;
  for (const ring of outerRings(geometry)) {
    const longitudes = ring.map((point) => point[0]!);
    const latitudes = ring.map((point) => point[1]!);
    if (longitudes.length === 0) continue;
    const [west, east] = [Math.min(...longitudes), Math.max(...longitudes)];
    const [south, north] = [Math.min(...latitudes), Math.max(...latitudes)];
    const area = (east - west) * (north - south);
    if (!best || area > best.area)
      best = { area, center: { latitude: (south + north) / 2, longitude: (west + east) / 2 } };
  }
  return best?.center ?? null;
}

const byName = (a: PickPlace, b: PickPlace) => a.name.localeCompare(b.name, 'en');

/**
 * The places to pick from: every country by name, each with its regions when the map has them.
 * A region's `country` is the key of its country.
 */
export function placesFrom(
  countries: GeoJSON.FeatureCollection,
  regions: GeoJSON.FeatureCollection,
): PickPlace[] {
  const regionsOf = new Map<string, PickPlace[]>();
  for (const region of regions.features) {
    const center = centerOf(region.geometry);
    const country = region.properties?.country as string | undefined;
    if (!center || !country) continue;
    const list = regionsOf.get(country) ?? [];
    list.push({
      key: region.properties?.key as string,
      name: region.properties?.name as string,
      center,
      regions: [],
    });
    regionsOf.set(country, list);
  }
  const places: PickPlace[] = [];
  for (const country of countries.features) {
    const center = centerOf(country.geometry);
    const name = country.properties?.name as string | undefined;
    const key = country.properties?.key as string | undefined;
    if (!center || !name || !key) continue;
    places.push({ key, name, center, regions: (regionsOf.get(key) ?? []).sort(byName) });
  }
  return places.sort(byName);
}

let loaded: Promise<PickPlace[]> | null = null;

/** Loads the places once, from the outlines that are part of the app. No other host is asked. */
export function loadPlaces(): Promise<PickPlace[]> {
  loaded ??= Promise.all([
    loadCountries('coarse'),
    fetch(regionsUrl).then((response) => response.json() as Promise<GeoJSON.FeatureCollection>),
  ]).then(([countries, regions]) => placesFrom(countries, regions));
  // A failed load is forgotten, so opening the list again tries again.
  loaded.catch(() => (loaded = null));
  return loaded;
}
