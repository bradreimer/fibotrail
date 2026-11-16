// Writes the country outlines that the server uses to find the country of a stop.
// Intention: the web app and the server draw and match against one source, the Natural Earth
// countries in the world-atlas package (ADR-009, ADR-011). Method: read the 1:50 million
// outlines, turn them into plain polygons, round each position to 3 decimals (about 100 m, far
// finer than the data), and write them gzipped as an embedded resource of Fibotrail.Places.
// Run it with "npm run outlines" after the world-atlas package changes, and commit the result.

import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { gzipSync } from 'node:zlib';

import { feature } from 'topojson-client';

const require = createRequire(import.meta.url);
const topology = require('world-atlas/countries-50m.json');
const { version } = require('world-atlas/package.json');

const round = (value) => Math.round(value * 1000) / 1000;

/** A GeoJSON Polygon or MultiPolygon as a list of polygons, each a list of rings (outer ring first). */
function polygonsOf(geometry) {
  const polygons = geometry.type === 'Polygon' ? [geometry.coordinates] : geometry.coordinates;
  return polygons.map((rings) =>
    rings.map((ring) => ring.map(([lon, lat]) => [round(lon), round(lat)])),
  );
}

const countries = feature(topology, topology.objects.countries).features.map((item) => ({
  // The ISO 3166-1 numeric code, as the web app's map features carry it. A few territories have none.
  id: item.id ?? null,
  name: item.properties.name,
  polygons: polygonsOf(item.geometry),
}));

const output = path.resolve(
  import.meta.dirname,
  '../../server/src/Fibotrail.Places/Data/countries.json.gz',
);
mkdirSync(path.dirname(output), { recursive: true });
writeFileSync(
  output,
  gzipSync(JSON.stringify({ source: `world-atlas@${version}`, countries }), { level: 9 }),
);
console.log(
  `Wrote ${countries.length} countries from world-atlas@${version} to ${path.relative(process.cwd(), output)}`,
);
