import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import { fakeServer } from '../../../test/server';
import TripsPerYear from '.';

const trip = (id: number, name: string, start: string, end: string) => ({
  id,
  name,
  startDate: start,
  endDate: end,
  days: 3,
});

const years = [
  { year: 2022, count: 1, trips: [trip(1, 'Oslo', '2022-02-01', '2022-02-03')] },
  { year: 2023, count: 0, trips: [] },
  {
    year: 2024,
    count: 2,
    trips: [
      trip(2, 'Rome', '2024-04-01', '2024-04-03'),
      trip(3, 'Paris', '2024-06-01', '2024-06-03'),
    ],
  },
];

describe('Trips per year', () => {
  it('has a text alternative for each bar, and lists a year when its bar is tapped', async () => {
    fakeServer({ 'GET /api/stats/trips-per-year': years });
    render(
      <MemoryRouter>
        <TripsPerYear />
      </MemoryRouter>,
    );

    const bars = await screen.findAllByRole('button');
    expect(bars.map((bar) => bar.getAttribute('aria-label'))).toEqual([
      '2022: 1 trip',
      '2023: 0 trips',
      '2024: 2 trips',
    ]);
    expect(bars[1]).toBeDisabled();

    await userEvent.click(bars[2]!);

    expect(screen.getByRole('link', { name: 'Rome' })).toHaveAttribute('href', '/trips/2');
    expect(screen.getByRole('link', { name: 'Paris' })).toBeInTheDocument();
  });

  it('says so with no trips', async () => {
    fakeServer({ 'GET /api/stats/trips-per-year': [] });
    render(
      <MemoryRouter>
        <TripsPerYear />
      </MemoryRouter>,
    );

    expect(await screen.findByText(/No trips yet/)).toBeInTheDocument();
  });
});
