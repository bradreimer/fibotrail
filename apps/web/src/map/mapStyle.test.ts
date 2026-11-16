import { featureFilter, validateStyleMin } from '@maplibre/maplibre-gl-style-spec';
import { describe, expect, it } from 'vitest';

import { baseMapStyle, boundsOf, routeGeoJson, routesGeoJson, visitedFilter } from './mapStyle';

const colors = {
  water: '#dde6e4',
  land: '#f7f3ea',
  border: '#c3cfcc',
  route: '#146560',
  routeOutline: '#f7f3ea',
  visited: '#7bae7f',
};

describe('the base map style', () => {
  const style = baseMapStyle(colors);

  it('asks no other host for anything: no tiles, no fonts, and no images', () => {
    expect(style.glyphs).toBeUndefined();
    expect(style.sprite).toBeUndefined();
    for (const source of Object.values(style.sources)) {
      expect(source.type).toBe('geojson');
    }
    expect(JSON.stringify(style)).not.toMatch(/https?:\/\//);
  });

  it('has no text layer, because it has no font files', () => {
    expect(style.layers.map((layer) => layer.type)).not.toContain('symbol');
  });

  it('draws the route as a 4 px line over an outline, above the land', () => {
    const ids = style.layers.map((layer) => layer.id);
    expect(ids).toEqual(['water', 'land', 'visited', 'borders', 'route-outline', 'route']);
    const route = style.layers.find((layer) => layer.id === 'route')!;
    expect(route.paint).toMatchObject({ 'line-color': '#146560', 'line-width': 4 });
  });
});

describe('routeGeoJson', () => {
  it('connects the points in order, as longitude then latitude', () => {
    const line = routeGeoJson([
      { latitude: 28.4, longitude: -81.55 },
      { latitude: 28.5, longitude: -81.4 },
    ]);
    expect(line.features[0]!.geometry).toEqual({
      type: 'LineString',
      coordinates: [
        [-81.55, 28.4],
        [-81.4, 28.5],
      ],
    });
  });

  it('draws nothing for one stop', () => {
    expect(routeGeoJson([{ latitude: 1, longitude: 2 }]).features).toEqual([]);
  });
});

describe('boundsOf', () => {
  it('is the box that holds every point', () => {
    expect(
      boundsOf([
        { latitude: 28.4, longitude: -81.55 },
        { latitude: 49.25, longitude: -123.1 },
      ]),
    ).toEqual([-123.1, 28.4, -81.55, 49.25]);
    expect(boundsOf([])).toBeNull();
  });
});

describe('the visited countries', () => {
  it('are a Meadow fill above the land and below the borders, matching nothing until told', () => {
    const style = baseMapStyle(colors);
    const visited = style.layers.find((layer) => layer.id === 'visited')!;

    expect(visited.type).toBe('fill');
    expect(visited.paint).toMatchObject({ 'fill-color': '#7bae7f' });
    expect(visited).toMatchObject({ filter: visitedFilter([]) });
    expect(visitedFilter([])).toEqual(['in', ['get', 'key'], ['literal', ['']]]);
  });

  it('are picked by key, which is the ISO code or the name', () => {
    expect(visitedFilter(['752', 'Kosovo'])).toEqual([
      'in',
      ['get', 'key'],
      ['literal', ['752', 'Kosovo']],
    ]);
  });
});

describe('routesGeoJson', () => {
  it('draws one line for each route with at least two points', () => {
    const lines = routesGeoJson([
      [
        { latitude: 1, longitude: 2 },
        { latitude: 3, longitude: 4 },
      ],
      [{ latitude: 5, longitude: 6 }],
      [
        { latitude: 7, longitude: 8 },
        { latitude: 9, longitude: 10 },
      ],
    ]);

    expect(lines.features.map((line) => line.geometry)).toEqual([
      {
        type: 'LineString',
        coordinates: [
          [2, 1],
          [4, 3],
        ],
      },
      {
        type: 'LineString',
        coordinates: [
          [8, 7],
          [10, 9],
        ],
      },
    ]);
  });

  it('draws nothing for no routes', () => {
    expect(routesGeoJson([]).features).toEqual([]);
  });
});

describe("MapLibre's own checks of the style", () => {
  const feature = (key: string) => ({
    type: 'Feature' as const,
    properties: { key },
    geometry: { type: 'Point' as const, coordinates: [0, 0] },
  });

  it('accept the whole base map style with no error', () => {
    expect(validateStyleMin(baseMapStyle(colors))).toEqual([]);
  });

  it('pick exactly the visited countries, including one that has no code', () => {
    const { filter } = featureFilter(visitedFilter(['752', 'Kosovo']), 'layers[0].filter');

    expect(
      ['752', 'Kosovo', '250'].map((key) => filter({ zoom: 0 }, feature(key) as never)),
    ).toEqual([true, true, false]);
  });

  it('pick nothing when no country is visited', () => {
    const { filter } = featureFilter(visitedFilter([]), 'layers[0].filter');

    expect(filter({ zoom: 0 }, feature('752') as never)).toBe(false);
  });
});
