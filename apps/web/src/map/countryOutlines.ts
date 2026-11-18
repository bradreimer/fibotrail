import { feature } from 'topojson-client';
import type { Topology } from 'topojson-specification';
import coarseUrl from 'world-atlas/countries-50m.json?url';
import fineUrl from 'world-atlas/countries-10m.json?url';

import { cutGeometry } from './antimeridian.mjs';

/**
 * How detailed the country outline under a map is (ADR-009, ADR-020).
 * - `coarse`: Natural Earth at 1:50 million, 230 KB gzipped. For maps of the whole world.
 * - `fine`: Natural Earth at 1:10 million, 921 KB gzipped. For the trip page, where you zoom in to
 *   one region and need its islands and coasts.
 */
export type OutlineDetail = 'coarse' | 'fine';

/** The address of each outline. Both files are part of the app, so no other host is asked. */
export const COUNTRY_OUTLINE_URLS: Record<OutlineDetail, string> = {
  coarse: coarseUrl,
  fine: fineUrl,
};

const loaded = new Map<OutlineDetail, Promise<GeoJSON.FeatureCollection>>();

/** Loads one outline the first time a map asks for it, and then keeps it for every later map. */
export function loadCountries(detail: OutlineDetail): Promise<GeoJSON.FeatureCollection> {
  let land = loaded.get(detail);
  if (!land) {
    land = fetch(COUNTRY_OUTLINE_URLS[detail])
      .then((response) => response.json() as Promise<Topology>)
      .then((topology) => {
        const countries = feature(
          topology,
          topology.objects.countries!,
        ) as unknown as GeoJSON.FeatureCollection;
        // The key lets the visited layer pick outlines by the same ID the server uses.
        for (const item of countries.features) {
          item.properties = { ...item.properties, key: String(item.id ?? item.properties?.name) };
          // Russia and Fiji cross the antimeridian. Uncut, their outlines cover the sea across the map.
          item.geometry = cutGeometry(item.geometry as GeoJSON.Polygon | GeoJSON.MultiPolygon);
        }
        return countries;
      });
    // A failed load is forgotten, so the next map tries again.
    land.catch(() => loaded.delete(detail));
    loaded.set(detail, land);
  }
  return land;
}

/** Forgets the loaded outlines. For tests only. */
export function forgetLoadedCountries(): void {
  loaded.clear();
}
