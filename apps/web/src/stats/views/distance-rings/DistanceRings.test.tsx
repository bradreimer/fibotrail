import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import { fakeServer } from '../../../test/server';
import DistanceRings from '.';

const ring = (id: number, name: string, distanceKm: number, bearingDegrees: number, days = 10) => ({
  trip: { id, name, startDate: '2023-01-01', endDate: '2023-01-10', days },
  distanceKm,
  bearingDegrees,
});

const trips = [
  ring(1, 'Seattle, May 2020', 200, 270, 3),
  ring(2, 'Rome, June 2021', 3000, 60, 7),
  ring(3, 'Sydney, January 2023', 15620, 220, 10),
];

function renderRings() {
  return render(
    <MemoryRouter>
      <DistanceRings />
    </MemoryRouter>,
  );
}

describe('the distance rings', () => {
  it('selects the farthest trip first, and says its distance and direction in words', async () => {
    fakeServer({ 'GET /api/stats/distance-rings': trips });
    renderRings();

    const card = await screen.findByLabelText('Selected trip');

    expect(card.textContent).toContain('Sydney, January 2023 · 15,620 km southwest · 10 days');
    expect(screen.getByRole('link', { name: 'Sydney, January 2023' })).toHaveAttribute(
      'href',
      '/trips/3',
    );
  });

  it('has a text alternative with the bands and the farthest trip', async () => {
    fakeServer({ 'GET /api/stats/distance-rings': trips });
    renderRings();

    const text = (await screen.findByText(/trips by distance from home\./)).textContent!;

    expect(text).toContain('3 trips by distance from home.');
    expect(text).toContain('Under 500 km: 1.');
    expect(text).toContain('1,000 to 5,000 km: 1.');
    expect(text).toContain('Over 10,000 km: 1.');
    expect(text).toContain('Farthest: Sydney, January 2023 · 15,620 km southwest · 10 days.');
  });

  it('steps nearer and farther, and stops at each end', async () => {
    fakeServer({ 'GET /api/stats/distance-rings': trips });
    renderRings();
    await screen.findByLabelText('Selected trip');
    expect(screen.getByRole('button', { name: 'Farther' })).toBeDisabled();

    await userEvent.click(screen.getByRole('button', { name: 'Nearer' }));
    expect(screen.getByLabelText('Selected trip').textContent).toContain('Rome, June 2021');
    await userEvent.click(screen.getByRole('button', { name: 'Nearer' }));
    expect(screen.getByLabelText('Selected trip').textContent).toContain('Seattle');
    expect(screen.getByRole('button', { name: 'Nearer' })).toBeDisabled();
  });

  it('selects a dot when tapped, and fades the dots outside a tapped band', async () => {
    fakeServer({ 'GET /api/stats/distance-rings': trips });
    const { container } = renderRings();
    await screen.findByLabelText('Selected trip');

    fireEvent.click(container.querySelectorAll('.rings__hit')[0]!);
    expect(screen.getByLabelText('Selected trip').textContent).toContain('Seattle');

    const band = screen.getByRole('button', { name: 'Under 500 km · 1 trip' });
    await userEvent.click(band);
    expect(container.querySelectorAll('.rings__dot--faded')).toHaveLength(2);
    await userEvent.click(band);
    expect(container.querySelectorAll('.rings__dot--faded')).toHaveLength(0);
  });

  it('counts every trip in exactly one band', async () => {
    fakeServer({ 'GET /api/stats/distance-rings': trips });
    renderRings();

    const bands = await screen.findByRole('list', { name: 'Distance bands' });

    const total = Array.from(bands.querySelectorAll('button')).reduce(
      (sum, button) => sum + Number(/· (\d+) trip/.exec(button.textContent!)![1]),
      0,
    );
    expect(total).toBe(trips.length);
  });

  it('says so when there is nothing to place', async () => {
    fakeServer({ 'GET /api/stats/distance-rings': [] });
    renderRings();

    expect(await screen.findByText(/No distances yet/)).toBeInTheDocument();
  });
});
