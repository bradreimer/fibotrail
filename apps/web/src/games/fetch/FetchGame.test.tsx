import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { expectNoAccessibilityProblems } from '../../accessibility/accessibility';
import { App } from '../../App';
import { gamesList } from '../../test/games';
import { fakeServer } from '../../test/server';
import { installMatchMedia } from '../../test/window';
import { localDate } from '../today';

const UNIT = 100;

const course = {
  physics: {
    gravity: 50,
    flap: -750,
    speed: 200,
    worldWidth: 400 * UNIT,
    worldHeight: 700 * UNIT,
    ground: 620 * UNIT,
    dogX: 100 * UNIT,
    dogSize: 64 * UNIT,
    body: { left: 6 * UNIT, top: 20 * UNIT, right: 58 * UNIT, bottom: 44 * UNIT },
    startY: 318 * UNIT,
    gateWidth: 70 * UNIT,
    gapHeight: 250 * UNIT,
    treatRadius: 24 * UNIT,
  },
  gates: Array.from({ length: 8 }, (_, index) => ({
    x: (440 + index * 240) * UNIT,
    gapTop: 175 * UNIT,
    gapBottom: 425 * UNIT,
    day: index + 1,
  })),
  treats: Array.from({ length: 8 }, (_, index) => ({
    x: (595 + index * 240) * UNIT,
    y: 300 * UNIT,
    photoId: `photo-${index}`,
  })),
  finishX: 2360 * UNIT,
};

const stats = { played: 1, winPercent: 100, currentStreak: 1, bestStreak: 1, tries: [0, 1, 0] };

const puzzle = (result: unknown = null) => ({
  number: 9,
  date: localDate(),
  maxTries: 3,
  tripsNeeded: 0,
  course,
  result,
});

const ground = { end: 'ground', tick: 50, gatesPassed: 0, caught: [], finished: false };
const finish = { end: 'finish', tick: 1127, gatesPassed: 8, caught: [0, 2], finished: true };

const result = (runs: unknown[] = [ground, finish], solved = true) => ({
  solved,
  tries: runs.length,
  maxTries: 3,
  runs,
  treats: 2,
  caughtPhotoIds: ['photo-0', 'photo-2'],
  share: 'Fibotrail Fetch #9 2/3 🦴 2/8\n💥\n🦴⬛🦴⬛⬛⬛⬛⬛🏁',
  trip: {
    tripId: 12,
    tripName: 'Paris, September 2027',
    startDate: '2027-09-01',
    endDate: '2027-09-05',
    place: 'Paris, Île-de-France',
  },
  stats,
});

function renderGame() {
  return render(
    <MemoryRouter initialEntries={['/games/fetch']}>
      <App />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  installMatchMedia(390);
  // jsdom has no 2D canvas, and each animation frame here is a whole second, so a run ends fast.
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null);
  let now = 0;
  vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) =>
    setTimeout(() => callback((now += 1000)), 0),
  );
  vi.stubGlobal('cancelAnimationFrame', (id: number) => clearTimeout(id));
  vi.spyOn(performance, 'now').mockReturnValue(0);
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('the Fetch game', () => {
  it('asks for a trip with located photos when there is none', async () => {
    fakeServer({
      'GET /api/games/fetch/puzzle': { ...puzzle(), tripsNeeded: 1, course: null },
    });
    renderGame();

    expect(await screen.findByText(/Fetch needs a trip with photos/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open the inbox' })).toHaveAttribute('href', '/inbox');
  });

  it('flies three runs, has the server grade each, and ends on the trip', async () => {
    const server = fakeServer({
      'GET /api/games/fetch/puzzle': puzzle(),
      'POST /api/games/fetch/check': ground,
      'POST /api/games/fetch/results': result([ground, ground, ground], false),
    });
    const { container } = renderGame();

    const stage = await screen.findByRole('button', { name: 'Flap' });
    expect(screen.getByText('Tap to start')).toBeInTheDocument();
    expect(screen.queryByText(/Paris/)).not.toBeInTheDocument();
    await expectNoAccessibilityProblems(container);

    for (let run = 1; run <= 3; run++) {
      fireEvent.pointerDown(stage);
      if (run < 3) {
        expect(
          await screen.findByText(/Hit the ground, with 0 of 8 treats. Tap to fly again./),
        ).toBeInTheDocument();
        expect(screen.getByRole('list', { name: 'Your runs' }).children).toHaveLength(run);
      }
    }

    expect(await screen.findByText(/No finish today/)).toBeInTheDocument();
    const checks = server.requests.filter((request) => request.path === '/api/games/fetch/check');
    expect(checks).toHaveLength(3);
    // Nothing but the start flap, so no taps: the browser sends taps, never a score.
    expect(checks[0]!.body).toEqual({ date: localDate(), taps: [] });
    const sent = server.requests.find((request) => request.path === '/api/games/fetch/results');
    expect(sent?.body).toEqual({ date: localDate(), runs: [[], [], []] });
  });

  it('flaps from the keyboard', async () => {
    const server = fakeServer({
      'GET /api/games/fetch/puzzle': puzzle(),
      'POST /api/games/fetch/check': ground,
    });
    renderGame();

    const stage = await screen.findByRole('button', { name: 'Flap' });
    fireEvent.keyDown(stage, { key: ' ' });

    await waitFor(() =>
      expect(server.requests.some((request) => request.path === '/api/games/fetch/check')).toBe(
        true,
      ),
    );
  });

  it('shows a played day: the runs, the caught photos, the trip, and the stats', async () => {
    fakeServer({
      'GET /api/games/fetch/puzzle': puzzle(result()),
      'GET /api/games': gamesList(['timeline', 'slide', 'fetch']),
    });
    const { container } = renderGame();

    expect(await screen.findByText('Finished on run 2, with 2 of 8 treats.')).toBeInTheDocument();
    // Pin comes before Fetch on the Games screen, so it's next though Stamps is unplayed too.
    expect(await screen.findByRole('link', { name: 'Next game: Pin →' })).toHaveAttribute(
      'href',
      '/games/pin',
    );
    const runs = screen.getByRole('list', { name: 'Your runs' });
    expect(within(runs).getByLabelText('Run 2: Finished, with 2 of 8 treats.')).toBeInTheDocument();
    expect(
      within(screen.getByRole('list', { name: 'The photos you caught' })).getAllByRole('img'),
    ).toHaveLength(2);
    expect(screen.getByRole('link', { name: 'Paris, September 2027' })).toHaveAttribute(
      'href',
      '/trips/12',
    );
    expect(screen.getByRole('button', { name: 'Share' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Flap' })).not.toBeInTheDocument();
    await expectNoAccessibilityProblems(container);
  });
});
