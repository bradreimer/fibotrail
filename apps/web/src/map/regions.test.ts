// The server matches stops against its own copy of the region outlines (ADR-012), and the web app
// draws this copy. This test keeps the two in step. If it fails, run "npm run outlines" in
// apps/web and commit the result.

import { readFileSync } from 'node:fs';
import path from 'node:path';
import { gunzipSync } from 'node:zlib';

import { describe, expect, it } from 'vitest';

import regions from './regions.json';

interface ServerRegions {
  source: string;
  countries: { code: string; id: string; name: string }[];
  regions: { id: string; name: string; country: string; polygons: number[][][][] }[];
}

const file = path.resolve(
  import.meta.dirname,
  '../../../server/src/Fibotrail.Places/Data/regions.json.gz',
);

describe("the server's region outlines", () => {
  const server = JSON.parse(gunzipSync(readFileSync(file)).toString('utf8')) as ServerRegions;

  it('hold the same regions as the map the web app draws', () => {
    expect(regions.features.map((item) => [item.properties.key, item.properties.name])).toEqual(
      server.regions.map((item) => [item.id, item.name]),
    );
    expect(regions.features.map((item) => item.properties.country)).toEqual(
      server.regions.map((item) => item.country),
    );
    expect(regions.features.map((item) => item.geometry.coordinates)).toEqual(
      server.regions.map((item) => item.polygons),
    );
  });

  it('name their source, and Canada has 13 regions and the USA has 51', () => {
    expect(server.source).toMatch(/^natural-earth@v\d/);
    expect(regions.source).toBe(server.source);
    const count = (country: string) =>
      regions.features.filter((item) => item.properties.country === country).length;
    expect([count('124'), count('840')]).toEqual([13, 51]);
  });
});
