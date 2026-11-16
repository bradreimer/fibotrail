import { createRequire } from 'node:module';

import { feature } from 'topojson-client';
import { describe, expect, it } from 'vitest';

import { cutGeometry, cutPolygon, cutPolygons } from './antimeridian.mjs';

/** The signed area of a closed ring, in square degrees. */
function area(ring: number[][]): number {
  let sum = 0;
  for (let i = 0; i < ring.length - 1; i++) {
    sum += ring[i]![0]! * ring[i + 1]![1]! - ring[i + 1]![0]! * ring[i]![1]!;
  }
  return Math.abs(sum / 2);
}

function jumps(ring: number[][]): boolean {
  return ring.some((point, i) => i > 0 && Math.abs(point[0]! - ring[i - 1]![0]!) > 180);
}

describe('cutting a polygon at the antimeridian', () => {
  const square = [
    [
      [10, 0],
      [20, 0],
      [20, 10],
      [10, 10],
      [10, 0],
    ],
  ];

  it('returns a polygon that never crosses 180 exactly as it was', () => {
    expect(cutPolygon(square)).toEqual([square]);
    expect(cutPolygon(square)[0]).toBe(square);
  });

  it('cuts a ring that jumps across 180 into one piece on each side, with the same total area', () => {
    // 4 degrees wide: from 178 East across 180 to 178 West. As data, its edges jump from 178 to -178.
    const crossing = [
      [
        [178, 0],
        [-178, 0],
        [-178, 10],
        [178, 10],
        [178, 0],
      ],
    ];

    const pieces = cutPolygon(crossing);

    expect(pieces).toHaveLength(2);
    expect(pieces.map((rings) => rings[0]!.map((point) => point[0]!))).toEqual([
      [178, 180, 180, 178, 178],
      [-180, -178, -178, -180, -180],
    ]);
    expect(pieces.every((rings) => rings.every((ring) => !jumps(ring)))).toBe(true);
    expect(pieces.reduce((sum, rings) => sum + area(rings[0]!), 0)).toBeCloseTo(40);
    expect(pieces.map((rings) => area(rings[0]!))).toEqual([20, 20]);
  });

  it('leaves a ring that winds around a pole as it is, because its long edge is right on a flat map', () => {
    const antarctica = [
      [
        [-180, -80],
        [-90, -80],
        [0, -80],
        [90, -80],
        [180, -80],
        [180, -90],
        [-180, -90],
        [-180, -80],
      ],
    ];

    expect(cutPolygon(antarctica)).toEqual([antarctica]);
  });

  it('keeps a hole with the piece that holds it', () => {
    const withHole = [
      [
        [170, 0],
        [-170, 0],
        [-170, 20],
        [170, 20],
        [170, 0],
      ],
      // A hole at 172 to 176, wholly east of 180 in the unwrapped ring is not here: it is west of it.
      [
        [172, 5],
        [176, 5],
        [176, 15],
        [172, 15],
        [172, 5],
      ],
    ];

    const pieces = cutPolygons([withHole]);

    expect(pieces).toHaveLength(2);
    expect(pieces[0]).toHaveLength(2);
    expect(pieces[1]).toHaveLength(1);
  });

  it('gives a GeoJSON Polygon when one piece is left and a MultiPolygon when there are several', () => {
    expect(cutGeometry({ type: 'Polygon', coordinates: square }).type).toBe('Polygon');
    const crossing = {
      type: 'Polygon' as const,
      coordinates: [
        [
          [178, 0],
          [-178, 0],
          [-178, 10],
          [178, 10],
          [178, 0],
        ],
      ],
    };

    expect(cutGeometry(crossing).type).toBe('MultiPolygon');
  });
});

describe('the Natural Earth country outlines, cut', () => {
  const topology = createRequire(import.meta.url)('world-atlas/countries-50m.json');
  const features = (
    feature(topology, topology.objects.countries) as unknown as {
      features: {
        properties: { name: string };
        geometry: GeoJSON.Polygon | GeoJSON.MultiPolygon;
      }[];
    }
  ).features;
  const cut = new Map(features.map((item) => [item.properties.name, cutGeometry(item.geometry)]));
  const polygonsOf = (name: string) => {
    const geometry = cut.get(name)!;
    return geometry.type === 'Polygon' ? [geometry.coordinates] : geometry.coordinates;
  };

  it('have no ring that jumps across the antimeridian, except the rings of Antarctica around the pole', () => {
    const jumping = [...cut.keys()].filter((name) =>
      polygonsOf(name).some((rings) => rings.some(jumps)),
    );

    expect(jumping).toEqual(['Antarctica']);
  });

  it('keep every longitude between -180 and 180', () => {
    for (const name of cut.keys()) {
      for (const rings of polygonsOf(name)) {
        for (const point of rings.flat()) {
          expect(point[0], name).toBeGreaterThanOrEqual(-180);
          expect(point[0], name).toBeLessThanOrEqual(180);
        }
      }
    }
  });

  it('give Russia a piece on each side of the antimeridian, and Fiji too', () => {
    for (const name of ['Russia', 'Fiji']) {
      const longitudes = polygonsOf(name).flatMap((rings) => rings[0]!.map((point) => point[0]!));
      expect(Math.max(...longitudes), name).toBeGreaterThan(179.5);
      expect(Math.min(...longitudes), name).toBeLessThan(-169);
    }
  });
});
