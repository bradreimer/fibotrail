import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import { fakeServer } from '../../../test/server';
import TripSpiral from '.';

const spiralTrip = (
  id: number,
  name: string,
  year: number,
  days: number,
  continent: [string, string] | null,
) => ({
  trip: { id, name, startDate: `${year}-05-01`, endDate: `${year}-05-0${days}`, days },
  continentId: continent?.[0] ?? null,
  continentName: continent?.[1] ?? null,
});

const europe: [string, string] = ['europe', 'Europe'];
const america: [string, string] = ['north-america', 'North America'];

const trips = [
  spiralTrip(1, 'Oslo, May 2012', 2012, 4, europe),
  spiralTrip(2, 'Orlando, May 2018', 2018, 9, america),
  spiralTrip(3, 'Anaheim, May 2026', 2026, 4, america),
];

function renderSpiral() {
  return render(
    <MemoryRouter>
      <TripSpiral />
    </MemoryRouter>,
  );
}

describe('the trip spiral', () => {
  it('selects the newest trip first, and its card reads Trip 3 of 3', async () => {
    fakeServer({ 'GET /api/stats/trip-spiral': trips });
    renderSpiral();

    const card = await screen.findByLabelText('Selected trip');

    expect(card.textContent).toContain('Trip 3 of 3');
    expect(card.textContent).toContain('North America');
    expect(screen.getByRole('link', { name: 'Anaheim, May 2026' })).toHaveAttribute(
      'href',
      '/trips/3',
    );
  });

  it('draws one dot for each trip, and labels the first trip of 2012 and 2020 only when they have trips', async () => {
    fakeServer({ 'GET /api/stats/trip-spiral': trips });
    const { container } = renderSpiral();
    await screen.findByLabelText('Selected trip');

    expect(container.querySelectorAll('.spiral__mark')).toHaveLength(3);
    expect(
      Array.from(container.querySelectorAll('.spiral__year')).map((n) => n.textContent),
    ).toEqual(['2012']);
  });

  it('has a text alternative with the range of years and the selected trip', async () => {
    fakeServer({ 'GET /api/stats/trip-spiral': trips });
    renderSpiral();

    expect(
      await screen.findByText(
        '3 trips from 2012 to 2026, placed along a spiral from the oldest at the center to the newest at the edge. Selected: Anaheim, May 2026, 4 days.',
      ),
    ).toBeInTheDocument();
  });

  it('steps Earlier and Later, and does nothing past either end', async () => {
    fakeServer({ 'GET /api/stats/trip-spiral': trips });
    renderSpiral();
    await screen.findByLabelText('Selected trip');
    expect(screen.getByRole('button', { name: 'Later' })).toBeDisabled();

    await userEvent.click(screen.getByRole('button', { name: 'Earlier' }));
    expect(screen.getByLabelText('Selected trip').textContent).toContain('Trip 2 of 3');
    await userEvent.click(screen.getByRole('button', { name: 'Earlier' }));
    expect(screen.getByRole('button', { name: 'Earlier' })).toBeDisabled();
    expect(screen.getByLabelText('Selected trip').textContent).toContain('Oslo');
  });

  it('selects a tapped dot', async () => {
    fakeServer({ 'GET /api/stats/trip-spiral': trips });
    const { container } = renderSpiral();
    await screen.findByLabelText('Selected trip');

    fireEvent.click(container.querySelectorAll('.spiral__hit')[1]!);

    expect(screen.getByLabelText('Selected trip').textContent).toContain('Trip 2 of 3');
  });

  it('fades the dots of other continents when a continent is tapped, and restores them with All', async () => {
    fakeServer({ 'GET /api/stats/trip-spiral': trips });
    const { container } = renderSpiral();
    await screen.findByLabelText('Selected trip');

    await userEvent.click(screen.getByRole('button', { name: 'Europe' }));
    expect(container.querySelectorAll('.spiral__dot--faded')).toHaveLength(2);

    await userEvent.click(screen.getByRole('button', { name: 'All' }));
    expect(container.querySelectorAll('.spiral__dot--faded')).toHaveLength(0);
  });

  it('lists only the continents that have trips', async () => {
    fakeServer({ 'GET /api/stats/trip-spiral': trips });
    renderSpiral();

    const chips = await screen.findByRole('list', { name: 'Continents' });

    expect(Array.from(chips.querySelectorAll('button')).map((chip) => chip.textContent)).toEqual([
      'All',
      'Europe',
      'North America',
    ]);
  });

  it('says so with no trips', async () => {
    fakeServer({ 'GET /api/stats/trip-spiral': [] });
    renderSpiral();

    expect(await screen.findByText(/No trips yet/)).toBeInTheDocument();
  });
});
