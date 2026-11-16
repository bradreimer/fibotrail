// Writes the region outlines of the detail countries (ADR-012).
// Intention: the Map shows which provinces, territories, and states you've visited in the few
// countries you travel in most, and the server and the web app draw and match against one source,
// Natural Earth's states and provinces at 1:50 million.
// Method: read the Natural Earth admin-1 file, keep the countries listed in detail-countries.json,
// round each position to 3 decimals (about 100 m, far finer than the data), and write two copies:
// a gzipped resource of Fibotrail.Places for the server, and a GeoJSON file for the web app.
// Run it with "npm run outlines" after the list or the Natural Earth version changes, and commit
// the result. The source is downloaded from a pinned Natural Earth release. To use a copy you
// already have, pass its path: "npm run outlines -- path/to/ne_50m_admin_1_states_provinces.geojson".

import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { gzipSync } from 'node:zlib';

import { feature } from 'topojson-client';

import { cutPolygons } from '../src/map/antimeridian.mjs';

const NATURAL_EARTH_VERSION = 'v5.1.2';
const SOURCE_URL = `https://raw.githubusercontent.com/nvkelso/natural-earth-vector/${NATURAL_EARTH_VERSION}/geojson/ne_50m_admin_1_states_provinces.geojson`;

const require = createRequire(import.meta.url);
const topology = require('world-atlas/countries-50m.json');
const detailCodes = JSON.parse(readFileSync(path.join(import.meta.dirname, 'detail-countries.json'), 'utf8'));

const round = (value) => Math.round(value * 1000) / 1000;

async function readSource() {
  const file = process.argv[2];
  if (file) return JSON.parse(readFileSync(file, 'utf8'));
  const response = await fetch(SOURCE_URL);
  if (!response.ok) throw new Error(`Could not download ${SOURCE_URL}: ${response.status}`);
  return response.json();
}

/** A GeoJSON Polygon or MultiPolygon as a list of polygons, each a list of rings (outer ring first). */
function polygonsOf(geometry) {
  const polygons = geometry.type === 'Polygon' ? [geometry.coordinates] : geometry.coordinates;
  return cutPolygons(polygons).map((rings) =>
    rings.map((ring) => ring.map(([lon, lat]) => [round(lon), round(lat)])),
  );
}

const source = await readSource();
const worldCountries = feature(topology, topology.objects.countries).features;

// The detail countries, tied to the same ISO numeric code the country outlines carry.
const names = new Map();
for (const item of source.features) names.set(item.properties.adm0_a3, item.properties.admin);
const countries = detailCodes.map((code) => {
  const admin = names.get(code);
  const world = worldCountries.find((item) => item.properties.name === admin);
  if (!admin || !world?.id) throw new Error(`No country outline matches the detail country ${code}`);
  return { code, id: world.id, name: world.properties.name };
});

const regions = source.features
  .filter((item) => detailCodes.includes(item.properties.adm0_a3))
  .map((item) => ({
    id: item.properties.iso_3166_2,
    name: item.properties.name,
    country: countries.find((country) => country.code === item.properties.adm0_a3).id,
    polygons: polygonsOf(item.geometry),
  }))
  .sort((a, b) => a.id.localeCompare(b.id));

if (regions.some((region) => !region.id)) throw new Error('A region has no ISO 3166-2 code');
if (new Set(regions.map((region) => region.id)).size !== regions.length) {
  throw new Error('Two regions share an ISO 3166-2 code');
}

const label = `natural-earth@${NATURAL_EARTH_VERSION}`;

const serverFile = path.resolve(
  import.meta.dirname,
  '../../server/src/Fibotrail.Places/Data/regions.json.gz',
);
mkdirSync(path.dirname(serverFile), { recursive: true });
writeFileSync(
  serverFile,
  gzipSync(JSON.stringify({ source: label, countries, regions }), { level: 9 }),
);

// The web copy is GeoJSON, so the map can draw it as it is. The key is the ISO 3166-2 code.
const webFile = path.resolve(import.meta.dirname, '../src/map/regions.json');
writeFileSync(
  webFile,
  JSON.stringify({
    type: 'FeatureCollection',
    source: label,
    features: regions.map((region) => ({
      type: 'Feature',
      properties: { key: region.id, name: region.name, country: region.country },
      geometry: {
        type: 'MultiPolygon',
        coordinates: region.polygons,
      },
    })),
  }),
);

for (const file of [serverFile, webFile]) {
  console.log(`Wrote ${path.relative(process.cwd(), file)}`);
}
console.log(`${regions.length} regions in ${countries.length} countries, from ${label}`);
