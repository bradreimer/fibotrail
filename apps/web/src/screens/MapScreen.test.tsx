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

const canada = {
  id: '124',
  name: 'Canada',
  regionNoun: 'provinces and territories',
  totalRegions: 13,
  regions: [
    {
      id: 'CA-ON',
      name: 'Ontario',
      firstVisit: '2023-07-01',
      trips: [
        { id: 12, name: 'Toronto, July 2023', startDate: '2023-07-01', endDate: '2023-07-06' },
      ],
    },
    {
      id: 'CA-AB',
      name: 'Alberta',
      firstVisit: '2024-08-10',
      trips: [
        { id: 13, name: 'Banff, August 2024', startDate: '2024-08-10', endDate: '2024-08-17' },
        { id: 14, name: 'Banff, August 2025', startDate: '2025-08-09', endDate: '2025-08-16' },
      ],
    },
  ],
};

const usa = {
  id: '840',
  name: 'United States of America',
  regionNoun: 'states',
  totalRegions: 51,
  regions: [],
};

const map: MapInfo = {
  detailCountries: [],
  cities: [],
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

  it('shows no region line, list, or region data when no visited country has regions', async () => {
    fakeServer({ 'GET /api/map': map });
    renderMap();
    const worldMap = await screen.findByRole('application');

    expect(worldMap.dataset.detailCountries).toBeUndefined();
    expect(worldMap.dataset.visitedRegions).toBeUndefined();
    expect(
      screen.queryByRole('heading', { name: "Regions you've visited" }),
    ).not.toBeInTheDocument();
  });

  describe('for the countries that show their regions', () => {
    const detail: MapInfo = { ...map, detailCountries: [canada, usa] };

    it('says how many provinces, territories, and states you have visited, under the numbers', async () => {
      fakeServer({ 'GET /api/map': detail });
      renderMap();

      const lines = (
        await screen.findByRole('heading', { name: "Regions you've visited" })
      ).closest('section')!;

      expect(
        within(lines)
          .getAllByRole('listitem')
          .map((item) => item.textContent),
      ).toEqual([
        'Canada2 of 13 provinces and territories',
        'United States of America0 of 51 states',
      ]);
    });

    it('gives the map the detail countries and the visited regions by code', async () => {
      fakeServer({ 'GET /api/map': detail });
      renderMap();

      const worldMap = await screen.findByRole('application', {
        name: /^World map\. .* Zoom in to a country with regions/,
      });

      expect(JSON.parse(worldMap.dataset.detailCountries!)).toEqual(['124', '840']);
      expect(JSON.parse(worldMap.dataset.visitedRegions!)).toEqual(['CA-ON', 'CA-AB']);
    });

    it('lists the visited regions of each country with the first visit, in the order the server gives', async () => {
      fakeServer({ 'GET /api/map': detail });
      renderMap();

      const canadaList = (
        await screen.findByRole('heading', { name: 'Canada: 2 of 13 provinces and territories' })
      ).closest('section')!;
      expect(
        within(canadaList)
          .getAllByRole('listitem')
          .map((item) => item.textContent),
      ).toEqual(['OntarioJul 1, 2023', 'AlbertaAug 10, 2024']);
      expect(
        screen.getByRole('heading', { name: 'United States of America: 0 of 51 states' }),
      ).toBeInTheDocument();
      expect(screen.getByText('No states yet.')).toBeInTheDocument();
    });

    it('opens a sheet for a region tapped on the map, with its trips and its first visit', async () => {
      fakeServer({ 'GET /api/map': detail });
      renderMap();

      await userEvent.click(
        await screen.findByRole('button', { name: 'Region on the map: CA-AB' }),
      );

      const sheet = screen.getByRole('dialog', { name: 'Alberta' });
      expect(within(sheet).getByText('2 trips · First visit Aug 10, 2024')).toBeInTheDocument();
      expect(
        within(sheet)
          .getAllByRole('link')
          .map((link) => link.getAttribute('href')),
      ).toEqual(['/trips/13', '/trips/14']);
      expect(within(sheet).getByRole('button', { name: 'Close' })).toHaveFocus();
    });

    it('opens the same sheet from the list, so a small region needs no precise tap', async () => {
      fakeServer({ 'GET /api/map': detail });
      renderMap();
      const item = await screen.findByRole('button', { name: /^OntarioJul 1, 2023$/ });

      await userEvent.click(item);
      const sheet = screen.getByRole('dialog', { name: 'Ontario' });
      expect(within(sheet).getByText('1 trip · First visit Jul 1, 2023')).toBeInTheDocument();
      expectNoAccessibilityProblems();

      await userEvent.keyboard('{Escape}');
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(item).toHaveFocus();
    });

    it('shows one sheet at a time', async () => {
      fakeServer({ 'GET /api/map': detail });
      renderMap();
      await userEvent.click(await screen.findByRole('button', { name: /^OntarioJul 1, 2023$/ }));
      await userEvent.keyboard('{Escape}');

      await userEvent.click(screen.getByRole('button', { name: 'Country on the map: 752' }));

      expect(screen.getAllByRole('dialog')).toHaveLength(1);
      expect(screen.getByRole('dialog', { name: 'Sweden' })).toBeInTheDocument();
    });

    it('has an accessible name on every control', async () => {
      fakeServer({ 'GET /api/map': detail });
      renderMap();
      await screen.findByRole('application');

      expectNoAccessibilityProblems();
    });
  });

  it('says No places yet and links to the inbox when no trip has been added', async () => {
    fakeServer({
      'GET /api/map': {
        countries: [],
        detailCountries: [],
        cities: [],
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

  describe('cities and parks', () => {
    const withCities: MapInfo = {
      ...map,
      cities: [
        {
          name: 'Stockholm',
          latitude: 59.33,
          longitude: 18.07,
          isPark: false,
          firstVisit: '2022-10-31',
          trips: [
            {
              id: 5,
              name: 'Stockholm, October 2022',
              startDate: '2022-10-31',
              endDate: '2022-11-07',
            },
          ],
        },
        {
          name: 'Magic Kingdom',
          latitude: 28.42,
          longitude: -81.58,
          isPark: true,
          firstVisit: '2024-03-10',
          trips: [
            { id: 7, name: 'Orlando, March 2024', startDate: '2024-03-10', endDate: '2024-03-16' },
          ],
        },
      ],
    };

    it('gives the map a pin for each city and park, and lists them with the first visit', async () => {
      fakeServer({ 'GET /api/map': withCities });
      renderMap();

      const worldMap = await screen.findByRole('application');
      expect(JSON.parse(worldMap.dataset.cities!)).toEqual([
        { key: '0', latitude: 59.33, longitude: 18.07, isPark: false },
        { key: '1', latitude: 28.42, longitude: -81.58, isPark: true },
      ]);
      const list = screen.getByRole('heading', { name: 'Cities and parks' }).closest('section')!;
      expect(
        within(list)
          .getAllByRole('listitem')
          .map((item) => item.textContent),
      ).toEqual(['StockholmOct 31, 2022', 'Magic Kingdom (theme park)Park · Mar 10, 2024']);
    });

    it('opens a sheet with the trips from a pin on the map or a row in the list', async () => {
      fakeServer({ 'GET /api/map': withCities });
      renderMap();

      await userEvent.click(await screen.findByRole('button', { name: 'Pin on the map: 1' }));
      const sheet = screen.getByRole('dialog', { name: 'Magic Kingdom' });
      expect(within(sheet).getByRole('link', { name: /Orlando, March 2024/ })).toHaveAttribute(
        'href',
        '/trips/7',
      );
      await userEvent.click(within(sheet).getByRole('button', { name: 'Close' }));

      await userEvent.click(screen.getByRole('button', { name: /^StockholmOct 31, 2022$/ }));
      expect(screen.getByRole('dialog', { name: 'Stockholm' })).toBeInTheDocument();
    });

    it('shows no list when no city has been visited', async () => {
      fakeServer({ 'GET /api/map': map });
      renderMap();
      await screen.findByRole('application');

      expect(screen.queryByRole('heading', { name: 'Cities and parks' })).not.toBeInTheDocument();
    });
  });
});
