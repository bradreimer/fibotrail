import { afterEach, describe, expect, it, vi } from 'vitest';

import { COUNTRY_OUTLINE_URLS, forgetLoadedCountries, loadCountries } from './countryOutlines';

/** A one-country outline in TopoJSON, the format world-atlas ships. */
const topology = {
  type: 'Topology',
  arcs: [
    [
      [0, 0],
      [1, 0],
      [1, 1],
      [0, 0],
    ],
  ],
  objects: {
    countries: {
      type: 'GeometryCollection',
      geometries: [{ type: 'Polygon', arcs: [[0]], id: '036', properties: { name: 'Australia' } }],
    },
  },
};

function fakeFetch() {
  const fetch = vi.fn<(url: string) => Promise<Response>>(() =>
    Promise.resolve(new Response(JSON.stringify(topology))),
  );
  vi.stubGlobal('fetch', fetch);
  return fetch;
}

afterEach(() => {
  forgetLoadedCountries();
  vi.unstubAllGlobals();
});

describe('the country outlines', () => {
  it('are Natural Earth at 1:50 million for coarse and 1:10 million for fine', () => {
    expect(COUNTRY_OUTLINE_URLS.coarse).toMatch(/countries-50m\.json/);
    expect(COUNTRY_OUTLINE_URLS.fine).toMatch(/countries-10m\.json/);
  });

  it('are files of the app, not addresses on another host', () => {
    for (const url of Object.values(COUNTRY_OUTLINE_URLS)) {
      expect(url).not.toMatch(/^[a-z]+:\/\//i);
    }
  });

  it('fetch only the outline a map asks for, once', async () => {
    const fetch = fakeFetch();

    await loadCountries('coarse');
    await loadCountries('coarse');
    expect(fetch.mock.calls.map(([url]) => url)).toEqual([COUNTRY_OUTLINE_URLS.coarse]);

    await loadCountries('fine');
    expect(fetch.mock.calls.map(([url]) => url)).toEqual([
      COUNTRY_OUTLINE_URLS.coarse,
      COUNTRY_OUTLINE_URLS.fine,
    ]);
  });

  it('key each country by its ISO code, as the server does', async () => {
    fakeFetch();
    const land = await loadCountries('fine');
    expect(land.features.map((item) => item.properties?.key)).toEqual(['036']);
  });

  it('try again after a failed load', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.reject(new Error('offline'))),
    );
    await expect(loadCountries('fine')).rejects.toThrow('offline');

    const fetch = fakeFetch();
    await loadCountries('fine');
    expect(fetch).toHaveBeenCalledTimes(1);
  });
});
