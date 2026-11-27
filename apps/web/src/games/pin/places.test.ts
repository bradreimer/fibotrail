import { describe, expect, it } from 'vitest';

import { centerOf, placesFrom } from './places';

const square = (west: number, south: number, size: number): GeoJSON.Position[] => [
  [west, south],
  [west + size, south],
  [west + size, south + size],
  [west, south + size],
  [west, south],
];

const feature = (properties: Record<string, string>, geometry: GeoJSON.Geometry) => ({
  type: 'Feature' as const,
  properties,
  geometry,
});

describe('the Pin place list', () => {
  it('puts the pin at the center of the largest piece of land', () => {
    expect(
      centerOf({ type: 'MultiPolygon', coordinates: [[square(0, 0, 1)], [square(10, 20, 4)]] }),
    ).toEqual({ latitude: 22, longitude: 12 });
    expect(centerOf(null)).toBeNull();
  });

  it('lists countries by name, each with its regions by name', () => {
    const countries: GeoJSON.FeatureCollection = {
      type: 'FeatureCollection',
      features: [
        feature(
          { key: '250', name: 'France' },
          { type: 'Polygon', coordinates: [square(0, 44, 4)] },
        ),
        feature(
          { key: '124', name: 'Canada' },
          { type: 'Polygon', coordinates: [square(-120, 50, 40)] },
        ),
      ],
    };
    const regions: GeoJSON.FeatureCollection = {
      type: 'FeatureCollection',
      features: [
        feature(
          { key: 'CA-ON', name: 'Ontario', country: '124' },
          { type: 'Polygon', coordinates: [square(-90, 44, 10)] },
        ),
        feature(
          { key: 'CA-AB', name: 'Alberta', country: '124' },
          { type: 'Polygon', coordinates: [square(-120, 49, 10)] },
        ),
      ],
    };

    const places = placesFrom(countries, regions);

    expect(places.map((place) => place.name)).toEqual(['Canada', 'France']);
    expect(places[0]!.regions.map((region) => region.name)).toEqual(['Alberta', 'Ontario']);
    expect(places[0]!.regions[0]!.center).toEqual({ latitude: 54, longitude: -115 });
    expect(places[1]!.regions).toEqual([]);
  });
});
