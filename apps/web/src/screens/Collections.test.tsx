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
};

const detail = {
  summary,
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
  it('lists each collection with its progress and days', async () => {
    fakeServer({ 'GET /api/collections': [summary] });
    renderAt('/collections');

    const card = await screen.findByRole('link', { name: /Universal parks/ });

    expect(card.textContent).toContain('1 / 2 · 6 days across 1 park');
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

  it('shows each stamp as one label, and tells the three states apart by words', async () => {
    fakeServer({ 'GET /api/collections/universal-parks': detail });
    renderAt('/collections/universal-parks');

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
    renderAt('/collections/universal-parks');

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
        summary: { ...summary, visited: 0, totalDays: 0 },
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
    const { container } = renderAt('/collections/universal-parks');
    await screen.findByRole('heading', { name: 'Universal parks' });

    await expectNoAccessibilityProblems(container);
  });
});
