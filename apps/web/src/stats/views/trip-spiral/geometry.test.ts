import { describe, expect, it } from 'vitest';

import {
  buildSpiral,
  dotRadius,
  FIRST_SHARE,
  isLabeledYear,
  LAST_SHARE,
  pointAt,
  tallestSpiral,
  tripShares,
} from './geometry';

const distance = (a: { x: number; y: number }, b: { x: number; y: number }) =>
  Math.hypot(a.x - b.x, a.y - b.y);

describe('the Fibonacci spiral', () => {
  it('is smooth: each arc starts where the last one ended', () => {
    const { arcs } = buildSpiral();

    arcs.slice(1).forEach((arc, index) => {
      expect(distance(arc.start, arcs[index]!.end)).toBeLessThan(1e-9);
    });
  });

  it('has the length of its quarter circles', () => {
    const { length } = buildSpiral();

    expect(length).toBeCloseTo((Math.PI / 2) * (1 + 1 + 2 + 3 + 5 + 8 + 13 + 21 + 34 + 55), 6);
  });

  it('keeps the same shape when turned, and chooses the tallest turn', () => {
    expect(buildSpiral(1).length).toBeCloseTo(buildSpiral(0).length, 6);
    const tall = tallestSpiral();
    const width = tall.bounds.maxX - tall.bounds.minX;
    const height = tall.bounds.maxY - tall.bounds.minY;
    expect(height).toBeGreaterThanOrEqual(width);
  });

  it('starts at the center and ends at the end of the last arc', () => {
    const spiral = buildSpiral();

    expect(distance(pointAt(spiral, 0), { x: 0, y: 0 })).toBeLessThan(1e-9);
    expect(distance(pointAt(spiral, 1), spiral.arcs[9]!.end)).toBeLessThan(1e-6);
  });
});

describe('placing trips', () => {
  it('puts one trip at the start and none for no trips', () => {
    expect(tripShares(1)).toEqual([FIRST_SHARE]);
    expect(tripShares(0)).toEqual([]);
  });

  it('spreads trips from 7 to 98.5 percent at equal distances along the spiral', () => {
    const shares = tripShares(24);
    expect(shares[0]).toBeCloseTo(FIRST_SHARE);
    expect(shares[23]).toBeCloseTo(LAST_SHARE);

    const spiral = buildSpiral();
    const steps = shares.slice(1).map((share, index) => share - shares[index]!);
    steps.forEach((step) => expect(step).toBeCloseTo(steps[0]!, 9));
    // The 24 dots follow the arcs outward: each is farther from the center than the one before.
    const radii = shares.map((share) => distance(pointAt(spiral, share), { x: 0, y: 0 }));
    radii.slice(1).forEach((radius, index) => expect(radius).toBeGreaterThan(radii[index]! - 1e-9));
  });
});

describe('dots and labels', () => {
  it('makes a 10-day dot larger than a 4-day dot', () => {
    expect(dotRadius(4)).toBeCloseTo(6.3, 5);
    expect(dotRadius(10)).toBeCloseTo(7.93, 2);
  });

  it('labels 2012, 2016, 2020, 2024, and every fourth year after', () => {
    expect([2011, 2012, 2013, 2016, 2020, 2024, 2028].map(isLabeledYear)).toEqual([
      false,
      true,
      false,
      true,
      true,
      true,
      true,
    ]);
  });
});
