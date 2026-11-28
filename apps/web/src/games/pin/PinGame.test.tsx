import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { expectNoAccessibilityProblems } from '../../accessibility/accessibility';
import { App } from '../../App';
import { gamesList } from '../../test/games';
import { fakeServer } from '../../test/server';
import { installMatchMedia } from '../../test/window';
import { localDate } from '../today';

vi.mock('./places', () => ({
  loadPlaces: () =>
    Promise.resolve([
      {
        key: '124',
        name: 'Canada',
        center: { latitude: 61, longitude: -98 },
        regions: [
          { key: 'CA-AB', name: 'Alberta', center: { latitude: 55, longitude: -115 }, regions: [] },
        ],
      },
      { key: '250', name: 'France', center: { latitude: 46, longitude: 2 }, regions: [] },
    ]),
}));

const stats = { played: 1, winPercent: 100, currentStreak: 1, bestStreak: 1, tries: [0, 1, 0, 0] };

const puzzle = (result: unknown = null) => ({
  number: 9,
  date: localDate(),
  maxTries: 4,
  tripsNeeded: 0,
  photoId: 'photo-1',
  result,
});

const sydney = { latitude: -33.87, longitude: 151.21 };
const paris = { latitude: 48.86, longitude: 2.35 };
const far = { pin: sydney, band: 'red', direction: 'nw', distanceKm: 16900 };
const close = { pin: paris, band: 'green', direction: null, distanceKm: 10 };

const result = (marks = [far, close], solved = true) => ({
  solved,
  tries: marks.length,
  maxTries: 4,
  marks,
  share: 'Fibotrail Pin #9 2/4\n🟥↖️\n🟩',
  answer: {
    photoId: 'photo-1',
    latitude: 48.85,
    longitude: 2.35,
    tripId: 12,
    tripName: 'Paris, September 2027',
    startDate: '2027-09-01',
    endDate: '2027-09-05',
    place: 'Paris, Île-de-France',
    immichUrl: 'https://photos.example.com/photos/photo-1' as string | null,
  },
  stats,
});

function renderGame() {
  return render(
    <MemoryRouter initialEntries={['/games/pin']}>
      <App />
    </MemoryRouter>,
  );
}

const map = () => screen.findByRole('application', { name: /World map/ });

beforeEach(() => installMatchMedia(390));

describe('the Pin game', () => {
  it('shows the photo and a map with no trip name, and plays a pin at a time', async () => {
    const server = fakeServer({
      'GET /api/games/pin/puzzle': puzzle(),
      'POST /api/games/pin/check': (init?: RequestInit) => {
        const body = JSON.parse(String(init?.body)) as { pin: { latitude: number } };
        return body.pin.latitude < 0 ? far : close;
      },
      'POST /api/games/pin/results': result(),
    });
    const { container } = renderGame();
    await screen.findByRole('heading', { name: 'Pin #9' });

    expect(screen.queryByText(/Paris/)).not.toBeInTheDocument();
    expect(screen.getByRole('img', { name: "Today's puzzle. Where was this?" })).toHaveAttribute(
      'src',
      '/api/photos/photo-1/preview',
    );
    expect(screen.getByText(/4 tries left/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Place a pin to check' })).toBeDisabled();
    expect(await map()).toHaveAttribute('data-fit', 'false');
    await expectNoAccessibilityProblems(container);

    // A miss: the mark shows the band, the arrow, and the distance, and the pin stays dimmed.
    fireEvent.click(screen.getByRole('button', { name: 'Tap the map in Sydney' }));
    expect(within(await map()).getByRole('button', { name: 'Your pin' })).toHaveAttribute(
      'data-kind',
      'guess',
    );
    fireEvent.click(screen.getByRole('button', { name: 'Check' }));
    const tries = await screen.findByRole('list', { name: 'Your tries' });
    expect(within(tries).getByRole('listitem')).toHaveAccessibleName(
      'Try 1: Far, 16,900 kilometers, go northwest',
    );
    expect(within(tries).getByText('Far · 16,900 km')).toBeInTheDocument();
    expect(screen.getByText(/3 tries left/)).toBeInTheDocument();
    expect(within(await map()).getByRole('button', { name: /^Try 1: Far/ })).toHaveAttribute(
      'data-kind',
      'past',
    );

    // A hit ends the game, and the server gets every pin.
    fireEvent.click(screen.getByRole('button', { name: 'Tap the map near Paris' }));
    fireEvent.click(screen.getByRole('button', { name: 'Check' }));
    expect(await screen.findByText('Solved in 2 tries.')).toBeInTheDocument();
    const post = server.requests.find((request) => request.path === '/api/games/pin/results');
    expect(post?.body).toEqual({ date: localDate(), pins: [sydney, paris] });
  });

  it('ends with the answer on the map, a line to it, and the trip', async () => {
    fakeServer({
      'GET /api/games/pin/puzzle': puzzle(result()),
      'GET /api/games': gamesList(['timeline', 'slide', 'pin']),
    });
    const { container } = renderGame();

    expect(await screen.findByText('Solved in 2 tries.')).toBeInTheDocument();
    expect(await screen.findByRole('link', { name: 'Next game: Fetch →' })).toHaveAttribute(
      'href',
      '/games/fetch',
    );
    const shown = await map();
    expect(within(shown).getByRole('button', { name: /^Where you took it/ })).toHaveAttribute(
      'data-position',
      '48.85,2.35',
    );
    expect(JSON.parse(shown.getAttribute('data-route')!)).toEqual([
      paris,
      { latitude: 48.85, longitude: 2.35 },
    ]);
    expect(shown).toHaveAttribute('data-fit', 'true');
    expect(screen.getByRole('link', { name: 'Paris, September 2027' })).toHaveAttribute(
      'href',
      '/trips/12',
    );
    expect(screen.getByText(/Paris, Île-de-France/)).toBeInTheDocument();
    const immich = screen.getByRole('link', {
      name: 'Open the photo from Paris, September 2027 in Immich',
    });
    expect(immich).toHaveAttribute('href', 'https://photos.example.com/photos/photo-1');
    expect(immich).toHaveAttribute('target', '_blank');
    expect(screen.getByText('Tap the photo to open it in Immich.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Share' })).toBeInTheDocument();
    await expectNoAccessibilityProblems(container);
  });

  it('shows the photo without an Immich link when there is no Immich address', async () => {
    const shown = result();
    fakeServer({
      'GET /api/games/pin/puzzle': puzzle({
        ...shown,
        answer: { ...shown.answer, immichUrl: null },
      }),
    });
    renderGame();

    const figure = (await screen.findByRole('figure')) as HTMLElement;
    expect(within(figure).getByRole('img')).toHaveAttribute('src', '/api/photos/photo-1/preview');
    expect(within(figure).queryByRole('link', { name: /in Immich/ })).not.toBeInTheDocument();
    expect(screen.queryByText('Tap the photo to open it in Immich.')).not.toBeInTheDocument();
  });

  it('says how far off the last pin was when the tries run out', async () => {
    fakeServer({
      'GET /api/games/pin/puzzle': puzzle(
        result([far, far, far, { ...far, distanceKm: 310, band: 'yellow' }], false),
      ),
    });
    renderGame();

    expect(await screen.findByText('So close. It was 310 km away.')).toBeInTheDocument();
  });

  it('places a pin from a list of countries and regions', async () => {
    fakeServer({ 'GET /api/games/pin/puzzle': puzzle(), 'POST /api/games/pin/check': close });
    renderGame();
    await screen.findByRole('heading', { name: 'Pin #9' });

    const summary = screen.getByText('Pick a place from a list');
    fireEvent.click(summary);
    const details = summary.closest('details')!;
    details.open = true;
    fireEvent(details, new Event('toggle'));
    fireEvent.change(await screen.findByLabelText('Country'), { target: { value: '124' } });
    expect(within(await map()).getByRole('button', { name: 'Your pin' })).toHaveAttribute(
      'data-position',
      '61,-98',
    );
    fireEvent.change(screen.getByLabelText('Region'), { target: { value: 'CA-AB' } });
    await waitFor(() =>
      expect(
        within(screen.getByRole('application')).getByRole('button', { name: 'Your pin' }),
      ).toHaveAttribute('data-position', '55,-115'),
    );
    expect(screen.getByRole('status', { name: '' })).toHaveTextContent('Pin placed in Alberta.');
  });

  it('asks for a trip with located photos when there is none', async () => {
    fakeServer({
      'GET /api/games/pin/puzzle': { ...puzzle(), tripsNeeded: 1, photoId: null },
    });
    renderGame();

    expect(
      await screen.findByText(/Pin needs a trip with photos that have a location/),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open the inbox' })).toHaveAttribute('href', '/inbox');
  });
});
