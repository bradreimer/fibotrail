// The server matches stops against its own copy of the country outlines (ADR-011). This test keeps
// that copy in step with the world-atlas package that the web app draws from. If it fails, run
// "npm run outlines" in apps/web and commit the result.

import { readFileSync } from 'node:fs';
import path from 'node:path';
import { gunzipSync } from 'node:zlib';

import { feature } from 'topojson-client';
import { describe, expect, it } from 'vitest';
import countries from 'world-atlas/countries-50m.json';

interface Outlines {
  source: string;
  countries: { id: string | null; name: string; polygons: number[][][][] }[];
}

const file = path.resolve(
  import.meta.dirname,
  '../../../server/src/Fibotrail.Places/Data/countries.json.gz',
);

describe("the server's country outlines", () => {
  const outlines = JSON.parse(gunzipSync(readFileSync(file)).toString('utf8')) as Outlines;
  const drawn = feature(
    countries as unknown as Parameters<typeof feature>[0],
    (countries as unknown as { objects: { countries: never } }).objects.countries,
  ) as unknown as { features: { id?: string; properties: { name: string } }[] };

  it('hold the same countries as the map the web app draws', () => {
    expect(outlines.countries).toHaveLength(drawn.features.length);
    expect(outlines.countries.map((item) => [item.id, item.name])).toEqual(
      drawn.features.map((item) => [item.id ?? null, item.properties.name]),
    );
  });

  it('name their source, and every country has a polygon', () => {
    expect(outlines.source).toMatch(/^world-atlas@\d/);
    expect(outlines.countries.every((item) => item.polygons.length > 0)).toBe(true);
  });
});
