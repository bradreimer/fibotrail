import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';

import { expectNoAccessibilityProblems } from '../accessibility/accessibility';
import { App } from '../App';
import { fakeServer, status } from '../test/server';
import { installMatchMedia } from '../test/window';

const summary = {
  id: 'universal-parks',
  name: 'Universal parks',
  description: 'Made up',
  visited: 1,
  total: 2,
  totalDays: 6,
  trips: 2,
};

const detail = {
  summary,
  resorts: [
    { name: 'Universal Orlando Resort', visited: 1, total: 2, days: 6, trips: 2 },
    { name: 'Universal Studios Hollywood', visited: 0, total: 1, days: 0, trips: 0 },
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
      tripList: [],
    },
  ],
};

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  );
}

beforeEach(() => installMatchMedia(390));

describe('the Collections screens', () => {
  it('leads each collection card with its trips', async () => {
    fakeServer({ 'GET /api/collections': [summary] });
    renderAt('/collections');

    const card = await screen.findByRole('link', { name: /Universal parks/ });

    expect(card.textContent).toContain('2 trips');
    expect(card.textContent).not.toContain('days');
    expect(card).toHaveAttribute('href', '/collections/universal-parks');
  });

  it('says so when there are no collection files', async () => {
    fakeServer({ 'GET /api/collections': [] });
    renderAt('/collections');

    expect(
      await screen.findByText(
        'No collections yet. A collection is a data file in the collections folder.',
      ),
    ).toBeInTheDocument();
  });

  it('lists the resorts of a collection with their trips, most trips first', async () => {
    fakeServer({ 'GET /api/collections/universal-parks': detail });
    renderAt('/collections/universal-parks');

    const resort = await screen.findByRole('link', { name: /Universal Orlando Resort/ });
    expect(resort.textContent).toContain('2 trips');
    expect(resort).toHaveAttribute(
      'href',
      '/collections/universal-parks/resorts/Universal%20Orlando%20Resort',
    );
    expect(screen.getByRole('link', { name: /Hollywood/ }).textContent).toContain(
      '0 trips · Not visited yet',
    );
    expect(screen.getByText('A trip to two resorts counts once for each.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Stamp book' })).toHaveAttribute(
      'href',
      '/collections/universal-parks/stamps',
    );
  });

  it('lists the parks of a resort, and opens a visited park into its trips', async () => {
    fakeServer({ 'GET /api/collections/universal-parks': detail });
    renderAt('/collections/universal-parks/resorts/Universal%20Orlando%20Resort');

    await userEvent.click(
      await screen.findByRole('button', { name: 'Universal Studios Florida, 2 trips, 6 days' }),
    );

    expect(screen.getByRole('dialog', { name: 'Universal Studios Florida' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(
      screen.getByRole('group', { name: "Universal's Islands of Adventure, not visited yet" }),
    ).toBeInTheDocument();
    expect(screen.queryByText('Epic Universe')).not.toBeInTheDocument();
  });

  it('says so for a resort that is not in the collection', async () => {
    fakeServer({ 'GET /api/collections/universal-parks': detail });
    renderAt('/collections/universal-parks/resorts/Nowhere');

    expect(await screen.findByText("That resort isn't in this collection.")).toBeInTheDocument();
  });

  it('shows each stamp as one label, and tells the three states apart by words', async () => {
    fakeServer({ 'GET /api/collections/universal-parks': detail });
    renderAt('/collections/universal-parks/stamps');

    expect(
      await screen.findByRole('button', { name: 'Universal Studios Florida, 6 days, 2 trips' }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('group', { name: "Universal's Islands of Adventure, not visited yet" }),
    ).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'Epic Universe, coming' })).toBeInTheDocument();
    expect(screen.getByText('Park boundaries © OpenStreetMap contributors')).toBeInTheDocument();
  });

  it('opens a visited stamp into a sheet with its trips, and closes it', async () => {
    fakeServer({ 'GET /api/collections/universal-parks': detail });
    renderAt('/collections/universal-parks/stamps');

    await userEvent.click(
      await screen.findByRole('button', { name: 'Universal Studios Florida, 6 days, 2 trips' }),
    );

    const sheet = screen.getByRole('dialog', { name: 'Universal Studios Florida' });
    expect(within(sheet).getByText(/First visit Mar 10, 2024/)).toBeInTheDocument();
    expect(within(sheet).getByRole('link', { name: /Orlando, June 2025/ })).toHaveAttribute(
      'href',
      '/trips/9',
    );
    await userEvent.click(within(sheet).getByRole('button', { name: 'Close' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('invites you to add a trip when nothing is visited', async () => {
    fakeServer({
      'GET /api/collections/universal-parks': {
        ...detail,
        summary: { ...summary, visited: 0, totalDays: 0, trips: 0 },
      },
    });
    renderAt('/collections/universal-parks');

    expect(await screen.findByText('Add a trip to start your stamp book.')).toBeInTheDocument();
  });

  it('says so for a collection that does not exist', async () => {
    fakeServer({ 'GET /api/collections/nope': status(404, {}) });
    renderAt('/collections/nope');

    expect(await screen.findByText("That collection doesn't exist.")).toBeInTheDocument();
  });

  it('has no accessibility problems', async () => {
    fakeServer({ 'GET /api/collections/universal-parks': detail });
    for (const path of [
      '/collections/universal-parks',
      '/collections/universal-parks/resorts/Universal%20Orlando%20Resort',
      '/collections/universal-parks/stamps',
    ]) {
      const { container, unmount } = renderAt(path);
      await screen.findByRole('heading', { level: 2 });

      await expectNoAccessibilityProblems(container);
      unmount();
    }
  });
});
