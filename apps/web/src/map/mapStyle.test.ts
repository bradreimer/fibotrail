import { describe, expect, it } from 'vitest';

import { baseMapStyle, boundsOf, routeGeoJson } from './mapStyle';

const colors = {
  water: '#dde6e4',
  land: '#f7f3ea',
  border: '#c3cfcc',
  route: '#146560',
  routeOutline: '#f7f3ea',
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
    expect(ids).toEqual(['water', 'land', 'borders', 'route-outline', 'route']);
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
