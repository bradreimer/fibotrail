import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';

import { expectNoAccessibilityProblems } from '../accessibility/accessibility';
import { App } from '../App';
import { CollectionBodySkeleton } from '../insights/CollectionBody';
import { fakeServer, status } from '../test/server';
import { installMatchMedia } from '../test/window';

const totals = {
  trips: 12,
  countries: 9,
  outlineCountries: 240,
  continents: 3,
  cities: 21,
  daysAway: 84,
  distanceKm: 40250.4,
};

const summary = {
  id: 'universal-parks',
  name: 'Universal parks',
  description: 'Made up',
  visited: 1,
  total: 2,
  totalDays: 6,
  trips: 2,
};

const trip = (id: number, name: string, startDate: string) => ({
  id,
  name,
  startDate,
  endDate: startDate,
  days: 1,
  place: 'Orlando',
  coverPhotoId: `p${id}`,
});

const library = [
  trip(9, 'Orlando, June 2025', '2025-06-01'),
  trip(5, 'Lisbon', '2024-09-01'),
  trip(4, 'Orlando, March 2024', '2024-03-10'),
];

const detail = {
  summary,
  resorts: [
    {
      name: 'Universal Orlando Resort',
      visited: 1,
      total: 2,
      days: 6,
      trips: 2,
      tripList: [
        {
          tripId: 9,
          name: 'Orlando, June 2025',
          parks: ['Universal Studios Florida', 'Epic Universe'],
        },
        { tripId: 4, name: 'Orlando, March 2024', parks: ['Universal Studios Florida'] },
      ],
    },
    { name: 'Universal Studios Hollywood', visited: 0, total: 1, days: 0, trips: 0, tripList: [] },
  ],
  attribution: 'Park boundaries © OpenStreetMap contributors',
  places: [
    {
      id: 'usf',
      name: 'Universal Studios Florida',
      group: 'Universal Orlando Resort',
      status: 'open',
      days: 6,
      trips: 2,
      firstVisit: '2024-03-10',
      lastVisit: '2025-06-03',
      tripList: [
        { tripId: 4, name: 'Orlando, March 2024', days: 3 },
        { tripId: 9, name: 'Orlando, June 2025', days: 3 },
      ],
    },
    {
      id: 'ioa',
      name: "Universal's Islands of Adventure",
      group: 'Universal Orlando Resort',
      status: 'open',
      days: 0,
      trips: 0,
      firstVisit: null,
      lastVisit: null,
      tripList: [],
    },
    {
      id: 'epic',
      name: 'Epic Universe',
      group: 'Universal Orlando Resort',
      status: 'planned',
      days: 0,
      trips: 0,
      firstVisit: null,
      lastVisit: null,
      tripList: [],
    },
  ],
};

/** Reports the address the router settled on. */
function Spy({ onPath }: { onPath: (path: string) => void }) {
  onPath(useLocation().pathname);
  return null;
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  );
}

/** The back link is the first thing on the page, and its arrow is hidden from a screen reader. */
function expectBackLink(container: HTMLElement, name: string, href: string) {
  const page = container.querySelector('.insights-page') as HTMLElement;
  const link = within(page).getByRole('link', { name });
  expect(link).toHaveAttribute('href', href);
  expect(link).toHaveClass('back-link');
  expect(link.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
  expect(page.firstElementChild).toBe(link);
}

beforeEach(() => installMatchMedia(390));

describe('the Insights screen', () => {
  it('shows the six totals above the collections', async () => {
    fakeServer({ 'GET /api/insights/summary': totals, 'GET /api/collections': [summary] });
    const { container } = renderAt('/insights');

    const numbers = await screen.findByRole('region', { name: 'Your travel in numbers' });
    expect(
      within(numbers)
        .getAllByRole('listitem')
        .map((tile) => tile.querySelector('.stat__label')?.textContent),
    ).toEqual(['Trips', 'Countries', 'Cities', 'Continents', 'Days away', 'Distance']);
    expect(numbers.textContent).toContain('9 / 240 · 3.8%');
    expect(numbers.textContent).toContain('3 / 7');
    const collections = screen.getByRole('region', { name: 'Collections' });
    expect(
      within(collections).getByRole('heading', { name: 'Universal parks' }),
    ).toBeInTheDocument();
    expect(
      numbers.compareDocumentPosition(collections) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(screen.getByRole('heading', { level: 1, name: 'Insights' })).toBeInTheDocument();
    await expectNoAccessibilityProblems(container);
  });

  it('shows zeros and points to the inbox with no added trip', async () => {
    fakeServer({
      'GET /api/insights/summary': {
        ...totals,
        trips: 0,
        countries: 0,
        continents: 0,
        cities: 0,
        daysAway: 0,
        distanceKm: 0,
      },
      'GET /api/collections': [{ ...summary, trips: 0, visited: 0, totalDays: 0 }],
    });
    renderAt('/insights');

    expect(
      await screen.findByText(/No trips yet\. Add a trip, and the numbers fill in\./),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open the inbox' })).toHaveAttribute('href', '/inbox');
    expect(screen.getByRole('heading', { name: 'Universal parks' })).toBeInTheDocument();
  });

  it('shows Try again when the totals fail to load', async () => {
    fakeServer({ 'GET /api/insights/summary': status(500), 'GET /api/collections': [summary] });
    renderAt('/insights');

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it.each([
    ['/stats', '/insights'],
    ['/stats/countries/752', '/insights'],
    ['/collections', '/insights'],
    ['/collections/universal-parks/stamps', '/insights/universal-parks/stamps'],
  ])('sends the old address %s to %s', async (from, to) => {
    fakeServer({
      'GET /api/insights/summary': totals,
      'GET /api/collections': [summary],
      'GET /api/collections/universal-parks': detail,
    });
    let pathname = '';
    render(
      <MemoryRouter initialEntries={[from]}>
        <App />
        <Routes>
          <Route path="*" element={<Spy onPath={(path) => (pathname = path)} />} />
        </Routes>
      </MemoryRouter>,
    );

    await screen.findByRole('heading', { level: 1, name: 'Insights' });
    expect(pathname).toBe(to);
  });
});

describe('the collection screens', () => {
  it('lists each collection closed, with its trips and no days', async () => {
    fakeServer({ 'GET /api/insights/summary': totals, 'GET /api/collections': [summary] });
    renderAt('/insights');

    const row = (await screen.findByRole('heading', { name: 'Universal parks' })).closest(
      'details',
    )!;
    expect(row).not.toHaveAttribute('open');
    expect(row.textContent).toContain('2 trips');
    expect(row.textContent).not.toContain('days');
    expect(screen.queryByText('Universal Orlando Resort')).not.toBeInTheDocument();
  });

  it('opens a collection in place to show its totals, resorts, and stamp book', async () => {
    fakeServer({
      'GET /api/insights/summary': totals,
      'GET /api/collections': [summary],
      'GET /api/collections/universal-parks': detail,
    });
    const { container } = renderAt('/insights');

    await userEvent.click(await screen.findByText('Universal parks'));

    expect(await screen.findByText('1 park · 6 days · 2 trips')).toBeInTheDocument();
    expect(screen.queryByText(/\/ 2 parks/)).not.toBeInTheDocument();
    const card = screen.getByRole('link', { name: /Universal Orlando Resort/ });
    expect(card.textContent).toContain('2 trips');
    expect(screen.queryByText('Universal Studios Florida')).not.toBeInTheDocument();
    expect(screen.queryByText('Universal Studios Florida')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Stamp book' })).toHaveAttribute(
      'href',
      '/insights/universal-parks/stamps',
    );
    await expectNoAccessibilityProblems(container);
  });

  it('loads an open collection as a summary line, three resort rows, and a button, not one block', () => {
    const { container } = render(<CollectionBodySkeleton />);

    expect(screen.getByRole('status')).toHaveTextContent('Loading the collection');
    expect(container.querySelectorAll('.skeleton--text')).toHaveLength(1);
    expect(container.querySelectorAll('.skeleton--row')).toHaveLength(3);
    expect(container.querySelectorAll('.skeleton--button')).toHaveLength(1);
    expect(container.querySelector('.skeleton--card')).toBeNull();
  });

  it('shows Try again when an open collection fails to load', async () => {
    fakeServer({
      'GET /api/insights/summary': totals,
      'GET /api/collections': [summary],
      'GET /api/collections/universal-parks': status(500),
    });
    renderAt('/insights');

    await userEvent.click(await screen.findByText('Universal parks'));

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('says so when there are no collection files', async () => {
    fakeServer({ 'GET /api/insights/summary': totals, 'GET /api/collections': [] });
    renderAt('/insights');

    expect(
      await screen.findByText(
        'No collections yet. A collection is a data file in the collections folder.',
      ),
    ).toBeInTheDocument();
  });

  it('lists the resorts you have been to with their trips, and leaves out the rest', async () => {
    fakeServer({ 'GET /api/collections/universal-parks': detail });
    renderAt('/insights/universal-parks');

    const resort = await screen.findByRole('link', { name: /Universal Orlando Resort/ });
    expect(resort.textContent).toContain('2 trips');
    expect(resort).toHaveAttribute(
      'href',
      '/insights/universal-parks/resorts/Universal%20Orlando%20Resort',
    );
    expect(screen.queryByRole('link', { name: /Hollywood/ })).not.toBeInTheDocument();
    expect(screen.queryByText(/Not visited yet/)).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Stamp book' })).toHaveAttribute(
      'href',
      '/insights/universal-parks/stamps',
    );
  });

  it('opens a resort to every trip there, newest first, with a badge for each park visited', async () => {
    fakeServer({
      'GET /api/collections/universal-parks': detail,
      'GET /api/trips': library,
    });
    renderAt('/insights/universal-parks/resorts/Universal%20Orlando%20Resort');

    const june = await screen.findByRole('link', { name: /Orlando, June 2025/ });
    expect(june).toHaveAttribute('href', '/trips/9');
    expect(screen.queryByText('Lisbon')).not.toBeInTheDocument();
    const links = screen.getAllByRole('link', { name: /^Orlando, / });
    expect(links.map((link) => link.getAttribute('href'))).toEqual(['/trips/9', '/trips/4']);
    const both = within(screen.getByRole('list', { name: 'Parks on Orlando, June 2025' }));
    expect(both.getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      '✓ Universal Studios Florida',
      '✓ Epic Universe',
    ]);
    const one = within(screen.getByRole('list', { name: 'Parks on Orlando, March 2024' }));
    expect(one.getAllByRole('listitem')).toHaveLength(1);
    expect(screen.queryByText("Universal's Islands of Adventure")).not.toBeInTheDocument();
  });

  it('says so for a resort that is not in the collection', async () => {
    fakeServer({ 'GET /api/collections/universal-parks': detail, 'GET /api/trips': library });
    renderAt('/insights/universal-parks/resorts/Nowhere');

    expect(await screen.findByText("That resort isn't in this collection.")).toBeInTheDocument();
  });

  it('shows only the parks you have been to, each with its days, trips, and last visit', async () => {
    fakeServer({ 'GET /api/collections/universal-parks': detail });
    const { container } = renderAt('/insights/universal-parks/stamps');

    const stamp = await screen.findByRole('button', {
      name: 'Universal Studios Florida, 6 days, 2 trips, Last visit Jun 3, 2025',
    });
    expect(stamp.textContent).toContain('Last visit Jun 3, 2025');
    expect(screen.getByText('6 days across 1 park')).toBeInTheDocument();
    expect(screen.queryByText("Universal's Islands of Adventure")).not.toBeInTheDocument();
    expect(screen.queryByText('Epic Universe')).not.toBeInTheDocument();
    expect(screen.queryByText(/Not visited yet|Coming|\/ 2/)).not.toBeInTheDocument();
    expect(container.querySelector('.ring')).toBeNull();
    expect(screen.getByText('Park boundaries © OpenStreetMap contributors')).toBeInTheDocument();
  });

  it('opens a stamp into a sheet with its first and last visit and its trips, and closes it', async () => {
    fakeServer({ 'GET /api/collections/universal-parks': detail });
    renderAt('/insights/universal-parks/stamps');

    await userEvent.click(
      await screen.findByRole('button', { name: /^Universal Studios Florida/ }),
    );

    const sheet = screen.getByRole('dialog', { name: 'Universal Studios Florida' });
    expect(within(sheet).getByText(/First visit Mar 10, 2024/)).toBeInTheDocument();
    expect(within(sheet).getByText(/Last visit Jun 3, 2025/)).toBeInTheDocument();
    expect(within(sheet).getByRole('link', { name: /Orlando, June 2025/ })).toHaveAttribute(
      'href',
      '/trips/9',
    );
    await userEvent.click(within(sheet).getByRole('button', { name: 'Close' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('shows an empty stamp book with no count of parks left', async () => {
    const usf = detail.places[0]!;
    fakeServer({
      'GET /api/collections/universal-parks': {
        ...detail,
        summary: { ...summary, visited: 0, totalDays: 0, trips: 0 },
        places: [{ ...usf, days: 0, trips: 0, firstVisit: null, lastVisit: null, tripList: [] }],
      },
    });
    renderAt('/insights/universal-parks/stamps');

    expect(await screen.findByText('Add a trip to start your stamp book.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Universal Studios Florida/ })).toBeNull();
    expect(screen.queryByText(/across/)).not.toBeInTheDocument();
  });

  it('invites you to add a trip when nothing is visited', async () => {
    fakeServer({
      'GET /api/collections/universal-parks': {
        ...detail,
        summary: { ...summary, visited: 0, totalDays: 0, trips: 0 },
      },
    });
    renderAt('/insights/universal-parks');

    expect(await screen.findByText('Add a trip to start your stamp book.')).toBeInTheDocument();
  });

  it('says so for a collection that does not exist', async () => {
    fakeServer({ 'GET /api/collections/nope': status(404, {}) });
    renderAt('/insights/nope');

    expect(await screen.findByText("That collection doesn't exist.")).toBeInTheDocument();
  });

  it('shows a back arrow on a collection, a resort, and the stamp book', async () => {
    fakeServer({ 'GET /api/collections/universal-parks': detail, 'GET /api/trips': library });

    const collection = renderAt('/insights/universal-parks');
    await screen.findByRole('heading', { name: 'Universal parks' });
    expectBackLink(collection.container, 'Insights', '/insights');
    collection.unmount();

    const resort = renderAt('/insights/universal-parks/resorts/Universal%20Orlando%20Resort');
    await screen.findByRole('heading', { name: 'Universal Orlando Resort' });
    expectBackLink(resort.container, 'Universal parks', '/insights/universal-parks');
    resort.unmount();

    const stamps = renderAt('/insights/universal-parks/stamps');
    await screen.findByRole('heading', { name: 'Stamp book' });
    expectBackLink(stamps.container, 'Universal parks', '/insights/universal-parks');
  });

  it('has no accessibility problems', async () => {
    fakeServer({ 'GET /api/collections/universal-parks': detail, 'GET /api/trips': library });
    for (const path of [
      '/insights/universal-parks',
      '/insights/universal-parks/resorts/Universal%20Orlando%20Resort',
      '/insights/universal-parks/stamps',
    ]) {
      const { container, unmount } = renderAt(path);
      await screen.findByRole('heading', { level: 2 });

      await expectNoAccessibilityProblems(container);
      unmount();
    }
  });
});
