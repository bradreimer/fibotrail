import { describe, expect, it } from 'vitest';

import type { TripSummary } from './TripCard';
import { normalize, searchTrips } from './search';

const trip = (id: number, overrides: Partial<TripSummary>): TripSummary => ({
  id,
  name: `Trip ${id}`,
  startDate: '2027-03-15',
  endDate: '2027-03-24',
  days: 10,
  place: null,
  coverPhotoId: `c${id}`,
  keywords: [],
  ...overrides,
});

const trips = [
  trip(1, {
    name: 'Disney with the kids',
    place: 'Orlando',
    keywords: ['Magic Kingdom', 'Disney Parks'],
  }),
  trip(2, {
    name: 'Paris',
    startDate: '2025-09-01',
    endDate: '2025-09-06',
    keywords: ['France', 'Île-de-France'],
  }),
  trip(3, { name: 'New Year', startDate: '2026-12-28', endDate: '2027-01-03' }),
];

const ids = (query: string) => searchTrips(trips, query).map((found) => found.id);

describe('searchTrips', () => {
  it('returns every trip, in order, for an empty or blank search', () => {
    expect(ids('')).toEqual([1, 2, 3]);
    expect(ids('   ')).toEqual([1, 2, 3]);
  });

  it('matches the name, the place, and the keywords, ignoring case', () => {
    expect(ids('KIDS')).toEqual([1]);
    expect(ids('orlando')).toEqual([1]);
    expect(ids('magic kingdom')).toEqual([1]);
  });

  it('matches every year a trip touches', () => {
    expect(ids('2025')).toEqual([2]);
    expect(ids('2026')).toEqual([3]);
    expect(ids('2027')).toEqual([1, 3]);
  });

  it('needs every word to match', () => {
    expect(ids('disney 2027')).toEqual([1]);
    expect(ids('disney 2025')).toEqual([]);
  });

  it('ignores accents', () => {
    expect(normalize('Île')).toBe('ile');
    expect(ids('ile-de')).toEqual([2]);
  });
});
