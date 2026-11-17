import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';

import { expectNoAccessibilityProblems } from '../accessibility/accessibility';
import { App } from '../App';
import { fakeServer, status } from '../test/server';
import { installMatchMedia } from '../test/window';

const summary = {
  trips: 12,
  countries: 5,
  outlineCountries: 236,
  continents: 2,
  cities: 9,
  daysAway: 80,
  distanceKm: 51234.5,
  continentCards: [
    { id: 'europe', name: 'Europe', visited: 4, total: 49 },
    { id: 'north-america', name: 'North America', visited: 1, total: 38 },
    { id: 'africa', name: 'Africa', visited: 0, total: 56 },
  ],
};

const trip = {
  id: 5,
  name: 'Stockholm, May 2025',
  startDate: '2025-05-11',
  endDate: '2025-05-18',
  days: 8,
};

const stockholm = {
  id: '752',
  name: 'Sweden',
  continentId: 'europe',
  continentName: 'Europe',
  firstVisit: '2019-07-01',
  trips: 3,
  days: 19,
  hasUnpositionedDays: true,
  tripList: [trip],
};

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  );
}

const views = {
  'GET /api/stats/records': {
    longestTrip: null,
    farthestTrip: null,
    mostCountriesTrip: null,
    mostVisitedCountry: null,
    longestGap: null,
  },
};

/** The back link is the first thing on the page, and its arrow is hidden from a screen reader. */
function expectBackLink(container: HTMLElement, name: string, href: string) {
  const page = container.querySelector('.stats-screen') as HTMLElement;
  const link = within(page).getByRole('link', { name });
  expect(link).toHaveAttribute('href', href);
  expect(link).toHaveClass('back-link');
  expect(link.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
  expect(page.firstElementChild).toBe(link);
}

beforeEach(() => installMatchMedia(390));

describe('the Stats screen', () => {
  it('shows the summary tiles, with the share of the outlines for countries', async () => {
    fakeServer({ 'GET /api/stats/summary': summary, ...views });
    renderAt('/stats');

    const numbers = (
      await screen.findByRole('heading', { name: 'Your travel in numbers' })
    ).closest('section')!;

    expect(
      within(numbers)
        .getAllByRole('listitem')
        .map((tile) => tile.textContent),
    ).toEqual([
      '12Trips',
      '5Countries5 / 236 · 2.1%',
      '9Cities',
      '2 / 7Continents',
      '80Days away',
      '51,235 kmDistance',
    ]);
  });

  it('lists the continents with their visited counts, and opens one', async () => {
    fakeServer({
      'GET /api/stats/summary': summary,
      'GET /api/stats/continents/europe': {
        id: 'europe',
        name: 'Europe',
        visited: 1,
        total: 49,
        countries: [{ id: '752', name: 'Sweden', trips: 3, days: 19 }],
      },
      ...views,
    });
    renderAt('/stats');

    const cards = await screen.findAllByRole('link', { name: /Europe|North America|Africa/ });
    expect(cards.map((card) => card.textContent)).toEqual([
      '4Europe4 / 49',
      '1North America1 / 38',
      '0Africa0 / 56',
    ]);

    await userEvent.click(cards[0]!);

    expect(await screen.findByRole('heading', { name: 'Europe' })).toBeInTheDocument();
    expect(screen.getByText('1 of 49 countries visited')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sweden' }).closest('li')!.textContent).toContain(
      '3 trips · 19 days',
    );
  });

  it('opens a country with its trips and days, and says that days count only located photos', async () => {
    fakeServer({ 'GET /api/stats/countries/752': stockholm, ...views });
    renderAt('/stats/countries/752');

    expect(await screen.findByRole('heading', { name: 'Sweden' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Europe' })).toHaveAttribute(
      'href',
      '/stats/continents/europe',
    );
    expect(screen.getByText('Days count only photos with a location.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Stockholm, May 2025' })).toHaveAttribute(
      'href',
      '/trips/5',
    );
  });

  it('says what is missing for a continent or a country with no visits', async () => {
    fakeServer({ 'GET /api/stats/countries/999': status(404, {}) });
    renderAt('/stats/countries/999');

    expect(
      await screen.findByText('No added trip has a stop in this country.'),
    ).toBeInTheDocument();
  });

  it('shows an empty state with no trips', async () => {
    fakeServer({
      'GET /api/stats/summary': { ...summary, trips: 0, countries: 0, continents: 0, cities: 0 },
    });
    renderAt('/stats');

    expect(
      await screen.findByText('No stats yet. Add a trip, and the numbers fill in.'),
    ).toBeInTheDocument();
  });

  it('shows an error with a way to try again', async () => {
    fakeServer({ 'GET /api/stats/summary': status(500, {}) });
    renderAt('/stats');

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('shows a back arrow on a continent and a country', async () => {
    fakeServer({
      'GET /api/stats/continents/europe': {
        id: 'europe',
        name: 'Europe',
        visited: 1,
        total: 49,
        countries: [{ id: '752', name: 'Sweden', trips: 3, days: 19 }],
      },
      'GET /api/stats/countries/752': stockholm,
      ...views,
    });

    const continent = renderAt('/stats/continents/europe');
    await screen.findByRole('heading', { name: 'Europe' });
    expectBackLink(continent.container, 'Stats', '/stats');
    continent.unmount();

    const country = renderAt('/stats/countries/752');
    await screen.findByRole('heading', { name: 'Sweden' });
    expectBackLink(country.container, 'Europe', '/stats/continents/europe');
  });

  it('has no accessibility problems', async () => {
    fakeServer({ 'GET /api/stats/summary': summary, ...views });
    const { container } = renderAt('/stats');
    await screen.findByRole('heading', { name: 'Your travel in numbers' });

    await expectNoAccessibilityProblems(container);
  });
});
