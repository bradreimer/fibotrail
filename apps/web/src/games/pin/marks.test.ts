import { describe, expect, it } from 'vitest';

import { describeMark, formatKm } from './marks';

const pin = { latitude: 0, longitude: 0 };

describe('the Pin marks', () => {
  it('read as words, so a band is never color alone', () => {
    expect(describeMark({ pin, band: 'yellow', direction: 'e', distanceKm: 310 })).toBe(
      'Close, 310 kilometers, go east',
    );
    expect(describeMark({ pin, band: 'red', direction: 'nw', distanceKm: 2400 })).toBe(
      'Far, 2,400 kilometers, go northwest',
    );
    expect(describeMark({ pin, band: 'green', direction: null, distanceKm: 20 })).toBe(
      'Solved, 20 kilometers or less',
    );
  });

  it('show distances with a thousands comma', () => {
    expect(formatKm(2400)).toBe('2,400 km');
    expect(formatKm(60)).toBe('60 km');
  });
});
