import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import { fakeServer } from '../../../test/server';
import TravelClock from '.';

const trip = (id: number, name: string) => ({
  id,
  name,
  startDate: '2024-07-12',
  endDate: '2024-07-16',
  days: 5,
});

function months(
  overrides: Record<number, { days: number; trips: ReturnType<typeof trip>[] }> = {},
) {
  return Array.from({ length: 12 }, (_, index) => ({
    month: index + 1,
    days: 0,
    trips: [] as ReturnType<typeof trip>[],
    ...overrides[index + 1],
  }));
}

const busyJuly = months({
  1: { days: 2, trips: [trip(9, 'New Year')] },
  7: { days: 41, trips: [trip(1, 'Rome'), trip(2, 'Oslo')] },
  12: { days: 5, trips: [trip(3, 'Christmas')] },
});

function renderClock() {
  return render(
    <MemoryRouter>
      <TravelClock />
    </MemoryRouter>,
  );
}

describe('the travel clock', () => {
  it('selects the busiest month first, and lists its trips', async () => {
    fakeServer({ 'GET /api/stats/travel-clock': busyJuly });
    renderClock();

    expect(
      await screen.findByRole('heading', { name: 'July · 41 days · 2 trips' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Rome' })).toHaveAttribute('href', '/trips/1');
  });

  it('has a text alternative that lists every month and names the busiest', async () => {
    fakeServer({ 'GET /api/stats/travel-clock': busyJuly });
    renderClock();

    const text = (await screen.findByText(/^Days away by month\./)).textContent!;

    expect(text).toContain('January: 2.');
    expect(text).toContain('July: 41, the busiest.');
    expect(text).toContain('February: 0.');
  });

  it('switches to trips, and changes the text alternative', async () => {
    fakeServer({ 'GET /api/stats/travel-clock': busyJuly });
    renderClock();
    await screen.findByRole('heading', { name: /July/ });

    await userEvent.click(screen.getByRole('button', { name: 'Trips' }));

    expect(screen.getByText(/^Trips by month\./).textContent).toContain('July: 2, the busiest.');
  });

  it('steps Later from December to January, and Earlier from January to December', async () => {
    fakeServer({ 'GET /api/stats/travel-clock': busyJuly });
    const { container } = renderClock();
    await screen.findByRole('heading', { name: /July/ });
    fireEvent.click(container.querySelectorAll('.clock__hit')[11]!);
    expect(screen.getByRole('heading', { name: 'December · 5 days · 1 trip' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Later' }));
    expect(screen.getByRole('heading', { name: 'January · 2 days · 1 trip' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Earlier' }));
    expect(screen.getByRole('heading', { name: /^December/ })).toBeInTheDocument();
  });

  it('draws no bar for a month with no value, and the busiest bar reaches the edge', async () => {
    fakeServer({ 'GET /api/stats/travel-clock': busyJuly });
    const { container } = renderClock();
    await screen.findByRole('heading', { name: /July/ });

    expect(container.querySelectorAll('.clock__bar')).toHaveLength(3);
    expect(container.querySelectorAll('.clock__bar--selected')).toHaveLength(1);
  });

  it('says so with no trips', async () => {
    fakeServer({ 'GET /api/stats/travel-clock': months() });
    renderClock();

    expect(await screen.findByText(/No trips yet/)).toBeInTheDocument();
  });
});
