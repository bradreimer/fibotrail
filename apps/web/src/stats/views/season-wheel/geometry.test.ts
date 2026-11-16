import { describe, expect, it } from 'vitest';

import { labelPoint, monthRange, shade, slicePath } from './geometry';

describe('the season wheel geometry', () => {
  it('puts January at the top and July at the bottom', () => {
    expect(labelPoint(1).x).toBeGreaterThan(100);
    expect(labelPoint(1).y).toBeLessThan(50);
    expect(labelPoint(7).y).toBeGreaterThan(150);
  });

  it('writes a season across the new year in words', () => {
    expect(monthRange([12, 1, 2])).toBe('Dec–Feb');
    expect(monthRange([6, 7, 8])).toBe('Jun–Aug');
  });

  it('shades a month from nothing to the busiest', () => {
    expect(shade(0, 7)).toBe(0);
    expect(shade(7, 7)).toBe(1);
    expect(shade(1, 100)).toBe(0.25);
  });

  it('draws a closed slice for each month', () => {
    for (let month = 1; month <= 12; month += 1) {
      expect(slicePath(month).endsWith('Z')).toBe(true);
    }
  });
});
