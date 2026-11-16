import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { fakeServer } from '../../../test/server';
import TripLength from '.';

describe('Trip length', () => {
  it('reads each bin with its count', async () => {
    fakeServer({
      'GET /api/stats/trip-length': [
        { label: '2 days', minDays: 0, maxDays: 2, count: 1 },
        { label: '3 days', minDays: 3, maxDays: 3, count: 4 },
        { label: '4 to 5 days', minDays: 4, maxDays: 5, count: 0 },
        { label: '6 to 8 days', minDays: 6, maxDays: 8, count: 2 },
        { label: '9 or more days', minDays: 9, maxDays: null, count: 0 },
      ],
    });
    render(<TripLength />);

    const list = await screen.findByRole('list', { name: 'Trips by length' });

    expect(Array.from(list.children).map((row) => row.textContent)).toEqual([
      '2 days1 trip',
      '3 days4 trips',
      '4 to 5 days0 trips',
      '6 to 8 days2 trips',
      '9 or more days0 trips',
    ]);
  });

  it('says so with no trips', async () => {
    fakeServer({
      'GET /api/stats/trip-length': [{ label: '2 days', minDays: 0, maxDays: 2, count: 0 }],
    });
    render(<TripLength />);

    expect(await screen.findByText(/No trips yet/)).toBeInTheDocument();
  });
});
