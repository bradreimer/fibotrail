import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';

import { App } from '../App';
import { fakeServer } from '../test/server';
import { installMatchMedia, resizeTo } from '../test/window';
import { tripLine, type TripSummary } from '../trips/TripCard';

const trips: TripSummary[] = [
  {
    id: 3,
    name: 'Paris',
    startDate: '2027-09-01',
    endDate: '2027-09-06',
    days: 6,
    place: 'Paris',
    coverPhotoId: 'c3',
  },
  {
    id: 2,
    name: 'Okanagan weekend',
    startDate: '2027-07-03',
    endDate: '2027-07-06',
    days: 4,
    place: 'Kelowna',
    coverPhotoId: 'c2',
  },
  {
    id: 1,
    name: 'Disney with the kids',
    startDate: '2027-03-15',
    endDate: '2027-03-24',
    days: 10,
    place: 'Orlando',
    coverPhotoId: 'c1',
  },
];

function renderTrips() {
  return render(
    <MemoryRouter initialEntries={['/trips']}>
      <App />
    </MemoryRouter>,
  );
}

beforeEach(() => installMatchMedia(390));

describe('the trip library', () => {
  it('shows a card for each trip, in the order the server gives, newest first', async () => {
    fakeServer({ 'GET /api/trips': trips });
    renderTrips();
    const list = await screen.findByRole('list', { name: 'Your trips' });
    const cards = within(list).getAllByRole('link');
    expect(cards.map((card) => card.querySelector('.trip-card__name')!.textContent)).toEqual([
      'Paris',
      'Okanagan weekend',
      'Disney with the kids',
    ]);
  });

  it('builds each card from the cover, the name, and one line of facts, and links to the trip page', async () => {
    fakeServer({ 'GET /api/trips': trips });
    renderTrips();
    const card = await screen.findByRole('link', { name: /Disney with the kids/ });

    expect(card).toHaveAttribute('href', '/trips/1');
    expect(within(card).getByText('Mar 15–24, 2027 · 10 days · Orlando')).toHaveClass(
      'trip-card__line',
    );
    const cover = within(card).getByRole('img');
    expect(cover).toHaveAttribute('src', '/api/photos/c1/thumbnail');
    expect(cover).toHaveAttribute('alt', 'Disney with the kids, Mar 15–24, 2027');
    expect(cover).toHaveAttribute('loading', 'lazy');
  });

  it('is read by a screen reader as the trip name, dates, and destination', async () => {
    fakeServer({ 'GET /api/trips': trips });
    renderTrips();
    expect(
      await screen.findByRole('link', {
        name: 'Disney with the kids, Mar 15–24, 2027 · 10 days · Orlando',
      }),
    ).toBeInTheDocument();
  });

  it('shows the spiral mark on a Mist background when a cover can’t load', async () => {
    fakeServer({ 'GET /api/trips': [trips[0]] });
    renderTrips();
    const card = await screen.findByRole('link', { name: /Paris/ });
    fireEvent.error(within(card).getByRole('img'));
    expect(within(card).queryByRole('img')).not.toBeInTheDocument();
    expect(card.querySelector('.trip-card__mark')).not.toBeNull();
    expect(within(card).getByText('Paris', { selector: '.trip-card__name' })).toBeInTheDocument();
  });

  it.each([
    [599, 'tabs'],
    [600, 'rail'],
    [1024, 'sidebar'],
  ])('at %i px the grid sits in the "%s" layout, which sets its columns', async (width, mode) => {
    installMatchMedia(width);
    fakeServer({ 'GET /api/trips': trips });
    const { container } = renderTrips();
    await screen.findByRole('list', { name: 'Your trips' });
    expect(container.querySelector('.app')).toHaveAttribute('data-nav', mode);
    expect(container.querySelector('.app .trip-grid')).not.toBeNull();
  });

  it('keeps its cards when the width changes', async () => {
    fakeServer({ 'GET /api/trips': trips });
    renderTrips();
    await screen.findByRole('list', { name: 'Your trips' });
    resizeTo(1100);
    expect(screen.getAllByRole('link', { name: /days/ })).toHaveLength(3);
  });
});

describe('the library states', () => {
  it('shows skeleton cards in the grid while trips load', async () => {
    let release: (value: Response) => void = () => undefined;
    fakeServer({ 'GET /api/trips': trips });
    const original = globalThis.fetch;
    globalThis.fetch = ((input: RequestInfo | URL, init?: RequestInit) =>
      String(input) === '/api/trips'
        ? new Promise<Response>((resolve) => (release = resolve))
        : original(input, init)) as typeof fetch;
    const { container } = renderTrips();

    const loading = await screen.findByRole('status', { name: '' });
    expect(loading).toHaveClass('trip-grid');
    expect(container.querySelectorAll('.trip-grid .skeleton--card')).toHaveLength(6);
    await act(async () => release(new Response(JSON.stringify(trips))));
    expect(await screen.findByRole('list', { name: 'Your trips' })).toBeInTheDocument();
  });

  it('with no trips and suggestions waiting, names the count and links to the inbox', async () => {
    fakeServer({ 'GET /api/trips': [], 'GET /api/inbox/count': { count: 2 } });
    renderTrips();
    expect(
      await screen.findByText('No trips yet. You have 2 possible trips to review.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open the inbox' })).toHaveAttribute('href', '/inbox');
  });

  it('with nothing waiting and a scan running, says it is still scanning', async () => {
    fakeServer({
      'GET /api/trips': [],
      'GET /api/scan': {
        state: 'running',
        done: 10,
        total: 100,
        message: null,
        finishedAt: null,
        photoCount: 10,
      },
    });
    renderTrips();
    expect(
      await screen.findByText('No trips yet. Fibotrail is still scanning your library.'),
    ).toBeInTheDocument();
  });

  it('with nothing waiting and no scan running, offers Scan now', async () => {
    const server = fakeServer({
      'GET /api/trips': [],
      'POST /api/scan': {
        state: 'running',
        done: 0,
        total: null,
        message: null,
        finishedAt: null,
        photoCount: 0,
      },
    });
    renderTrips();
    const button = await screen.findByRole('button', { name: 'Scan now' });
    await waitFor(() => expect(button).toBeEnabled());
    await userEvent.click(button);
    expect(server.sent('POST', '/api/scan')).toHaveLength(1);
  });

  it('explains an error and tries again', async () => {
    const server = fakeServer({ 'GET /api/trips': { __status: 500 } });
    renderTrips();
    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong');
    server.set('GET /api/trips', trips);
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByRole('list', { name: 'Your trips' })).toBeInTheDocument();
  });
});

describe('tripLine', () => {
  it('leaves out a missing destination', () => {
    expect(tripLine({ ...trips[0]!, place: null, days: 1, endDate: '2027-09-01' })).toBe(
      'Sep 1, 2027 · 1 day',
    );
  });
});

it('a card opens its trip page', async () => {
  fakeServer({ 'GET /api/trips': trips });
  function Where() {
    return <p>at {useLocation().pathname}</p>;
  }
  render(
    <MemoryRouter initialEntries={['/trips']}>
      <Routes>
        <Route path="/trips/:id" element={<Where />} />
        <Route path="*" element={<App />} />
      </Routes>
    </MemoryRouter>,
  );
  await userEvent.click(await screen.findByRole('link', { name: /Okanagan weekend/ }));
  expect(screen.getByText('at /trips/2')).toBeInTheDocument();
});
