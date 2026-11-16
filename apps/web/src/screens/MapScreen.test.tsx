import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';

import { expectNoAccessibilityProblems } from '../accessibility/accessibility';
import { App } from '../App';
import type { components } from '../api/schema';
import { fakeServer, status } from '../test/server';
import { installMatchMedia } from '../test/window';

type MapInfo = components['schemas']['MapInfo'];

const map: MapInfo = {
  countries: [
    {
      id: '752',
      name: 'Sweden',
      firstVisit: '2022-10-31',
      trips: [
        { id: 5, name: 'Stockholm, October 2022', startDate: '2022-10-31', endDate: '2022-11-07' },
        { id: 8, name: 'Stockholm, May 2025', startDate: '2025-05-11', endDate: '2025-05-18' },
      ],
    },
    {
      id: '250',
      name: 'France',
      firstVisit: '2026-03-14',
      trips: [
        { id: 9, name: 'Colmar, March 2026', startDate: '2026-03-14', endDate: '2026-03-25' },
      ],
    },
    {
      id: null,
      name: 'Kosovo',
      firstVisit: '2026-04-02',
      trips: [{ id: 10, name: 'Pristina', startDate: '2026-04-02', endDate: '2026-04-03' }],
    },
  ],
  routes: [
    {
      tripId: 5,
      points: [
        { latitude: 59.3, longitude: 18.07 },
        { latitude: 59.9, longitude: 17.6 },
      ],
    },
    { tripId: 9, points: [{ latitude: 48.08, longitude: 7.36 }] },
  ],
  totals: { countries: 3, trips: 4, daysAway: 31, distanceKm: 12345.6 },
};

function renderMap() {
  return render(
    <MemoryRouter initialEntries={['/map']}>
      <App />
    </MemoryRouter>,
  );
}

beforeEach(() => installMatchMedia(390));

describe('the Map screen', () => {
  it('shows the four numbers above the map', async () => {
    fakeServer({ 'GET /api/map': map });
    renderMap();

    const numbers = (
      await screen.findByRole('heading', { name: 'Your travel in numbers' })
    ).closest('section')!;
    expect(
      within(numbers)
        .getAllByRole('listitem')
        .map((tile) => tile.textContent),
    ).toEqual(['3Countries', '4Trips', '31Days away', '12,346 kmDistance']);
  });

  it('gives the map the visited countries by code, or by name when a country has none', async () => {
    fakeServer({ 'GET /api/map': map });
    renderMap();

    const worldMap = await screen.findByRole('application', {
      name: "World map. The countries you've visited are filled in green.",
    });

    expect(JSON.parse(worldMap.dataset.visited!)).toEqual(['752', '250', 'Kosovo']);
  });

  it('lists the first visit to each country, in the order the server gives', async () => {
    fakeServer({ 'GET /api/map': map });
    renderMap();

    const list = (await screen.findByRole('heading', { name: 'First visits' })).closest('section')!;

    expect(
      within(list)
        .getAllByRole('listitem')
        .map((item) => item.textContent),
    ).toEqual(['SwedenOct 31, 2022', 'FranceMar 14, 2026', 'KosovoApr 2, 2026']);
  });

  it('draws no routes until the switch is turned on, and draws them all when it is', async () => {
    fakeServer({ 'GET /api/map': map });
    renderMap();
    const worldMap = await screen.findByRole('application');
    const toggle = screen.getByRole('switch', { name: 'Show routes' });

    expect(toggle).not.toBeChecked();
    expect(JSON.parse(worldMap.dataset.routes!)).toEqual([]);

    await userEvent.click(toggle);
    expect(JSON.parse(screen.getByRole('application').dataset.routes!)).toEqual(
      map.routes.map((route) => route.points),
    );

    await userEvent.click(toggle);
    expect(JSON.parse(screen.getByRole('application').dataset.routes!)).toEqual([]);
  });

  it('turns the routes on from a tap anywhere on the Show routes row', async () => {
    fakeServer({ 'GET /api/map': map });
    renderMap();
    await screen.findByRole('application');

    await userEvent.click(screen.getByText('Show routes'));

    expect(screen.getByRole('switch', { name: 'Show routes' })).toBeChecked();
  });

  it('opens a sheet for a country tapped on the map, with its trips and its first visit', async () => {
    fakeServer({ 'GET /api/map': map });
    renderMap();

    await userEvent.click(await screen.findByRole('button', { name: 'Country on the map: 752' }));

    const sheet = screen.getByRole('dialog', { name: 'Sweden' });
    expect(within(sheet).getByText('2 trips · First visit Oct 31, 2022')).toBeInTheDocument();
    const trips = within(sheet).getAllByRole('link');
    expect(trips.map((link) => link.getAttribute('href'))).toEqual(['/trips/5', '/trips/8']);
    expect(trips[0]).toHaveTextContent('Stockholm, October 2022Oct 31–Nov 7, 2022');
    expect(within(sheet).getByRole('button', { name: 'Close' })).toHaveFocus();
  });

  it('opens the same sheet from the list, closes it with Escape, and gives focus back', async () => {
    fakeServer({ 'GET /api/map': map });
    renderMap();
    const item = await screen.findByRole('button', { name: /^FranceMar 14, 2026$/ });

    await userEvent.click(item);
    const sheet = screen.getByRole('dialog', { name: 'France' });
    expect(within(sheet).getByText('1 trip · First visit Mar 14, 2026')).toBeInTheDocument();
    expectNoAccessibilityProblems();

    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(item).toHaveFocus();
  });

  it('says No places yet and links to the inbox when no trip has been added', async () => {
    fakeServer({
      'GET /api/map': {
        countries: [],
        routes: [],
        totals: { countries: 0, trips: 0, daysAway: 0, distanceKm: 0 },
      },
    });
    renderMap();

    expect(
      await screen.findByText('No places yet. Add a trip, and its countries fill in.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open the inbox' })).toHaveAttribute('href', '/inbox');
    expect(screen.queryByRole('application')).not.toBeInTheDocument();
  });

  it('shows a loading state in the final layout, then the map', async () => {
    fakeServer({ 'GET /api/map': map });
    renderMap();

    expect(screen.getByRole('status')).toHaveTextContent('Loading the map');
    expect(await screen.findByRole('application')).toBeInTheDocument();
  });

  it('explains an error and tries again', async () => {
    const server = fakeServer({ 'GET /api/map': status(500) });
    renderMap();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    server.set('GET /api/map', map);
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await waitFor(() => expect(screen.getByRole('application')).toBeInTheDocument());
  });

  it('has an accessible name on every control', async () => {
    fakeServer({ 'GET /api/map': map });
    renderMap();
    await screen.findByRole('application');

    expectNoAccessibilityProblems();
  });
});
