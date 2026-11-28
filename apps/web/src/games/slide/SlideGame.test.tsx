import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { expectNoAccessibilityProblems } from '../../accessibility/accessibility';
import { App } from '../../App';
import { gamesList } from '../../test/games';
import { fakeServer } from '../../test/server';
import { installMatchMedia } from '../../test/window';
import { localDate } from '../today';

const stats = { played: 1, winPercent: 100, currentStreak: 1, bestStreak: 1, tries: [1] };

/** Two moves from solved: 5 slides left, then 6 slides up. */
const puzzle = (result: unknown = null) => ({
  number: 5,
  date: localDate(),
  tripsNeeded: 0,
  photoId: 'photo-1',
  tiles: [1, 2, 3, 4, 0, 5, 7, 8, 6],
  result,
});

const result = {
  solved: true,
  moves: 2,
  share: 'Fibotrail Slide #5 2 moves',
  answer: {
    photoId: 'photo-1',
    tripId: 12,
    tripName: 'Banff, July 2025',
    startDate: '2025-07-03',
    endDate: '2025-07-09',
    place: 'Banff, Alberta',
    immichUrl: 'https://photos.example.com/photos/photo-1',
  },
  stats,
};

function renderGame() {
  return render(
    <MemoryRouter initialEntries={['/games/slide']}>
      <App />
    </MemoryRouter>,
  );
}

const tile = (n: number) => screen.getByRole('button', { name: new RegExp(`^Tile ${n},`) });

/** A board 300 px wide, so a cell is 100 px. */
function layOutBoard() {
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({
    top: 0,
    left: 0,
    width: 300,
    height: 300,
    right: 300,
    bottom: 300,
    x: 0,
    y: 0,
    toJSON: () => ({}),
  });
}

beforeEach(() => installMatchMedia(390));
afterEach(() => vi.restoreAllMocks());

describe('the Slide game', () => {
  it('shows the photo in tiles with no trip name, and solves by tap and drag', async () => {
    const server = fakeServer({
      'GET /api/games/slide/puzzle': puzzle(),
      'POST /api/games/slide/results': result,
    });
    const { container } = renderGame();
    await screen.findByRole('heading', { name: 'Slide #5' });
    layOutBoard();

    expect(screen.queryByText(/Banff/)).not.toBeInTheDocument();
    expect(tile(5)).toHaveAccessibleName('Tile 5, row 2, column 3');
    expect(tile(1)).toHaveAttribute('aria-disabled', 'true');
    await expectNoAccessibilityProblems(container);

    // A tap: down and up in place.
    fireEvent.pointerDown(tile(5), {
      pointerId: 1,
      pointerType: 'touch',
      clientX: 250,
      clientY: 150,
    });
    fireEvent.pointerUp(tile(5), {
      pointerId: 1,
      pointerType: 'touch',
      clientX: 251,
      clientY: 150,
    });
    expect(tile(5)).toHaveAccessibleName('Tile 5, row 2, column 2');
    expect(screen.getByText('1 move')).toBeInTheDocument();

    // A drag: tile 6 follows the finger up, then snaps into place.
    fireEvent.pointerDown(tile(6), {
      pointerId: 2,
      pointerType: 'touch',
      clientX: 250,
      clientY: 250,
    });
    fireEvent.pointerMove(tile(6), {
      pointerId: 2,
      pointerType: 'touch',
      clientX: 252,
      clientY: 200,
    });
    expect(tile(6)).toHaveClass('slide-tile--dragging');
    expect(tile(6).style.getPropertyValue('--slide-dy')).toBe('-50px');
    fireEvent.pointerMove(tile(6), {
      pointerId: 2,
      pointerType: 'touch',
      clientX: 252,
      clientY: 170,
    });
    fireEvent.pointerUp(tile(6), {
      pointerId: 2,
      pointerType: 'touch',
      clientX: 252,
      clientY: 170,
    });

    expect(document.querySelector('.slide-board--solved')).toBeInTheDocument();
    expect(
      await screen.findByText('Solved in 2 moves.', {}, { timeout: 3000 }),
    ).toBeInTheDocument();
    const post = server.requests.find((request) => request.method === 'POST');
    expect(post?.body).toEqual({ date: localDate(), moves: [5, 6] });
  });

  it('springs a tile back when the drag is short and pulled back', async () => {
    fakeServer({ 'GET /api/games/slide/puzzle': puzzle() });
    renderGame();
    await screen.findByRole('heading', { name: 'Slide #5' });
    layOutBoard();

    fireEvent.pointerDown(tile(5), {
      pointerId: 1,
      pointerType: 'mouse',
      button: 0,
      clientX: 250,
      clientY: 150,
    });
    fireEvent.pointerMove(tile(5), {
      pointerId: 1,
      pointerType: 'mouse',
      clientX: 220,
      clientY: 150,
    });
    fireEvent.pointerMove(tile(5), {
      pointerId: 1,
      pointerType: 'mouse',
      clientX: 240,
      clientY: 150,
    });
    fireEvent.pointerUp(tile(5), {
      pointerId: 1,
      pointerType: 'mouse',
      clientX: 240,
      clientY: 150,
    });

    expect(tile(5)).toHaveAccessibleName('Tile 5, row 2, column 3');
    expect(tile(5)).not.toHaveClass('slide-tile--dragging');
    expect(screen.getByText('0 moves')).toBeInTheDocument();
  });

  it('plays with the keyboard: Enter on a tile, and the arrow keys', async () => {
    fakeServer({
      'GET /api/games/slide/puzzle': puzzle(),
      'POST /api/games/slide/results': result,
    });
    renderGame();
    await screen.findByRole('heading', { name: 'Slide #5' });

    fireEvent.click(tile(5), { detail: 0 });
    expect(tile(5)).toHaveAccessibleName('Tile 5, row 2, column 2');
    // A tile that isn't in line with the empty cell doesn't move.
    fireEvent.click(tile(1), { detail: 0 });
    expect(tile(1)).toHaveAccessibleName('Tile 1, row 1, column 1');
    fireEvent.keyDown(tile(5), { key: 'ArrowUp' });

    expect(
      await screen.findByText('Solved in 2 moves.', {}, { timeout: 3000 }),
    ).toBeInTheDocument();
  });

  it('shows the whole photo with the trip name, dates, and place when played', async () => {
    fakeServer({
      'GET /api/games/slide/puzzle': puzzle(result),
      'GET /api/games': gamesList(['timeline', 'slide', 'pin', 'fetch', 'stamps']),
    });
    const { container } = renderGame();

    const figure = (await screen.findByRole('figure')) as HTMLElement;
    expect(await screen.findByRole('link', { name: 'Back to Games' })).toHaveAttribute(
      'href',
      '/games',
    );
    expect(within(figure).getByRole('img')).toHaveAttribute('src', '/api/photos/photo-1/preview');
    expect(within(figure).getByRole('link', { name: 'Banff, July 2025' })).toHaveAttribute(
      'href',
      '/trips/12',
    );
    expect(within(figure).getByText('Jul 3–9, 2025 · Banff, Alberta')).toBeInTheDocument();
    const immich = within(figure).getByRole('link', {
      name: 'Open the photo from Banff, July 2025 in Immich',
    });
    expect(immich).toHaveAttribute('href', 'https://photos.example.com/photos/photo-1');
    expect(immich).toHaveAttribute('target', '_blank');
    expect(screen.getByText('Tap the photo to open it in Immich.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Share' })).toBeInTheDocument();
    expect(screen.queryByText('Tries to win')).not.toBeInTheDocument();
    await expectNoAccessibilityProblems(container);
  });

  it('shows the photo without an Immich link when there is no Immich address', async () => {
    fakeServer({
      'GET /api/games/slide/puzzle': puzzle({
        ...result,
        answer: { ...result.answer, immichUrl: null },
      }),
    });
    renderGame();

    const figure = (await screen.findByRole('figure')) as HTMLElement;
    expect(within(figure).getByRole('img')).toHaveAttribute('src', '/api/photos/photo-1/preview');
    expect(within(figure).queryByRole('link', { name: /in Immich/ })).not.toBeInTheDocument();
    expect(screen.queryByText('Tap the photo to open it in Immich.')).not.toBeInTheDocument();
  });

  it('says a trip with photos is needed', async () => {
    fakeServer({
      'GET /api/games/slide/puzzle': { ...puzzle(), tripsNeeded: 1, photoId: null, tiles: [] },
    });
    renderGame();

    expect(await screen.findByText(/Slide needs a trip with photos/)).toBeInTheDocument();
  });
});
