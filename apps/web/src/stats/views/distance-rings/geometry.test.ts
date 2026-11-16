import { describe, expect, it } from 'vitest';

import { BANDS, compassWord, dotRadius, inBand, polar, radiusShare } from './geometry';

describe('the distance scale', () => {
  it('puts 1,000 km at half the radius and the outer edge at the whole radius', () => {
    expect(radiusShare(1000)).toBeCloseTo(0.5, 2);
    expect(radiusShare(20000)).toBe(1);
    expect(radiusShare(99999)).toBe(1);
  });

  it('keeps everything under 50 km on the home dot', () => {
    expect(radiusShare(0)).toBe(0);
    expect(radiusShare(50)).toBe(0);
  });

  it('puts 100 km inside 500 km inside 5,000 km', () => {
    expect(radiusShare(100)).toBeLessThan(radiusShare(500));
    expect(radiusShare(500)).toBeLessThan(radiusShare(5000));
  });
});

describe('polar', () => {
  it('puts north straight up and east to the right', () => {
    expect(polar(0, 10)).toEqual({ x: 0, y: -10 });
    const east = polar(90, 10);
    expect(east.x).toBeCloseTo(10);
    expect(east.y).toBeCloseTo(0);
  });
});

describe('compassWord', () => {
  it.each([
    [0, 'north'],
    [44, 'northeast'],
    [90, 'east'],
    [200, 'south'],
    [225, 'southwest'],
    [300, 'northwest'],
    [359, 'north'],
  ])('reads %i degrees as %s', (bearing, word) => {
    expect(compassWord(bearing)).toBe(word);
  });
});

describe('dotRadius', () => {
  it('grows with the square root of the days', () => {
    expect(dotRadius(0)).toBe(3.5);
    expect(dotRadius(4)).toBeCloseTo(6.3);
  });
});

describe('the bands', () => {
  it('place every distance in exactly one band', () => {
    for (const km of [0, 499.9, 500, 999, 1000, 4999, 5000, 9999, 10000, 19999]) {
      expect(BANDS.filter((band) => inBand(km, band))).toHaveLength(1);
    }
  });
});
