import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { expectNoAccessibilityProblems } from '../../accessibility/accessibility';
import { App } from '../../App';
import { fakeServer } from '../../test/server';
import { installMatchMedia } from '../../test/window';
import { localDate } from '../today';
import { saveGame } from './board';

const stats = {
  played: 1,
  winPercent: 100,
  currentStreak: 1,
  bestStreak: 1,
  tries: [1],
  times: [{ label: '5 × 5', days: 1, best: 161, average: 161 }],
};

/** A 5 by 5 board whose stamps go in cells 0, 7, 14, 16, and 23. */
const ROWS = ['ABBBB', 'ABBCC', 'ADDCC', 'ADDEE', 'AAEEE'];
const SOLUTION = [0, 7, 14, 16, 23];

const puzzle = (result: unknown = null) => ({
  number: 9,
  date: localDate(),
  tripsNeeded: 0,
  size: 5,
  label: 'Easy',
  weekday: 'Fri',
  rule: 'Place one stamp in each row, column, and trip. No two stamps touch, even at a corner.',
  sizeNote: '5 × 5 today, because you have 5 trips.',
  regions: ROWS,
  photos: Array.from({ length: 25 }, (_, cell) => `photo-${cell}`),
  result,
});

const passport = SOLUTION.map((cell, index) => ({
  cell,
  region: 'ABCDE'[index],
  photoId: `photo-${cell}`,
  tripId: 10 + index,
  tripName: `Trip ${index + 1}`,
  startDate: `202${index}-04-0${index + 1}`,
  endDate: `202${index}-04-1${index}`,
  immichUrl: `https://photos.example.com/photos/photo-${cell}`,
}));

const result = {
  solved: true,
  size: 5,
  seconds: 161,
  hints: 0,
  share: 'Fibotrail Stamps #9 Fri 5×5 2:41 💡0',
  passport,
  stats,
};

function renderGame() {
  return render(
    <MemoryRouter initialEntries={['/games/stamps']}>
      <App />
    </MemoryRouter>,
  );
}

const cell = (row: number, column: number) =>
  screen.getByRole('button', { name: new RegExp(`^Row ${row}, column ${column},`) });

/** A board 500 px wide, so a cell is 100 px. */
function layOutBoard() {
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({
    top: 0,
    left: 0,
    width: 500,
    height: 500,
    right: 500,
    bottom: 500,
    x: 0,
    y: 0,
    toJSON: () => ({}),
  });
}

/** A tap at the middle of a cell, as a finger would. */
function tap(row: number, column: number, id = 1) {
  const at = {
    pointerId: id,
    pointerType: 'touch',
    clientX: column * 100 - 50,
    clientY: row * 100 - 50,
  };
  const board = screen.getByRole('group', { name: /^Board/ });
  fireEvent.pointerDown(board, at);
  fireEvent.pointerUp(board, at);
}

beforeEach(() => installMatchMedia(390));
afterEach(() => vi.restoreAllMocks());

describe('the Stamps game', () => {
  it('shows the board with no trip names, and taps move a cell through ✕ and stamp', async () => {
    fakeServer({ 'GET /api/games/stamps/puzzle': puzzle() });
    const { container } = renderGame();
    await screen.findByRole('heading', { name: 'Stamps #9' });
    layOutBoard();

    expect(screen.getByText('Fri · 5 × 5 · Easy')).toBeInTheDocument();
    expect(screen.getByText('5 × 5 today, because you have 5 trips.')).toBeInTheDocument();
    expect(screen.queryByText(/Trip 1/)).not.toBeInTheDocument();
    expect(cell(1, 1)).toHaveAccessibleName('Row 1, column 1, region A, empty');
    await expectNoAccessibilityProblems(container);

    tap(1, 1);
    expect(cell(1, 1)).toHaveAccessibleName('Row 1, column 1, region A, crossed out');
    tap(1, 1);
    expect(cell(1, 1)).toHaveAccessibleName('Row 1, column 1, region A, stamp');
    // Auto-✕ marks the cells the stamp rules out.
    expect(cell(1, 2)).toHaveAccessibleName('Row 1, column 2, region B, ruled out');
    expect(cell(2, 2)).toHaveAccessibleName('Row 2, column 2, region B, ruled out');
    expect(cell(3, 3)).toHaveAccessibleName('Row 3, column 3, region D, empty');

    // With the switch off, the faint marks go.
    fireEvent.click(screen.getByRole('switch', { name: 'Mark ruled-out cells' }));
    expect(cell(1, 2)).toHaveAccessibleName('Row 1, column 2, region B, empty');

    tap(1, 1);
    expect(cell(1, 1)).toHaveAccessibleName('Row 1, column 1, region A, empty');
  });

  it('shows stamps that break a rule, and undoes the last change', async () => {
    fakeServer({ 'GET /api/games/stamps/puzzle': puzzle() });
    renderGame();
    await screen.findByRole('heading', { name: 'Stamps #9' });
    layOutBoard();

    tap(1, 1);
    tap(1, 1);
    tap(2, 2);
    tap(2, 2);
    expect(cell(1, 1)).toHaveAccessibleName('Row 1, column 1, region A, stamp, breaks a rule');
    expect(cell(2, 2)).toHaveAccessibleName('Row 2, column 2, region B, stamp, breaks a rule');

    fireEvent.click(screen.getByRole('button', { name: 'Undo' }));
    expect(cell(2, 2)).toHaveAccessibleName('Row 2, column 2, region B, crossed out');
    expect(cell(1, 1)).toHaveAccessibleName('Row 1, column 1, region A, stamp');
  });

  it('marks ✕ along a drag, and clears ✕ along a drag that starts on one', async () => {
    fakeServer({ 'GET /api/games/stamps/puzzle': puzzle() });
    renderGame();
    await screen.findByRole('heading', { name: 'Stamps #9' });
    layOutBoard();
    const board = screen.getByRole('group', { name: /^Board/ });

    fireEvent.pointerDown(board, { pointerId: 1, clientX: 50, clientY: 250 });
    fireEvent.pointerMove(board, { pointerId: 1, clientX: 150, clientY: 250 });
    fireEvent.pointerMove(board, { pointerId: 1, clientX: 350, clientY: 250 });
    fireEvent.pointerUp(board, { pointerId: 1, clientX: 350, clientY: 250 });
    expect(cell(3, 1)).toHaveAccessibleName(/crossed out$/);
    expect(cell(3, 2)).toHaveAccessibleName(/crossed out$/);
    expect(cell(3, 4)).toHaveAccessibleName(/crossed out$/);
    expect(cell(3, 5)).toHaveAccessibleName(/empty$/);

    fireEvent.pointerDown(board, { pointerId: 2, clientX: 50, clientY: 250 });
    fireEvent.pointerMove(board, { pointerId: 2, clientX: 150, clientY: 250 });
    fireEvent.pointerUp(board, { pointerId: 2, clientX: 150, clientY: 250 });
    expect(cell(3, 1)).toHaveAccessibleName(/empty$/);
    expect(cell(3, 2)).toHaveAccessibleName(/empty$/);
    expect(cell(3, 4)).toHaveAccessibleName(/crossed out$/);

    // One undo takes back the whole drag.
    fireEvent.click(screen.getByRole('button', { name: 'Undo' }));
    expect(cell(3, 1)).toHaveAccessibleName(/crossed out$/);
  });

  it('moves focus with the arrow keys and marks with the keyboard', async () => {
    fakeServer({ 'GET /api/games/stamps/puzzle': puzzle() });
    renderGame();
    await screen.findByRole('heading', { name: 'Stamps #9' });
    const user = userEvent.setup();

    cell(1, 1).focus();
    await user.keyboard('{ArrowRight}{ArrowDown}');
    expect(cell(2, 2)).toHaveFocus();
    await user.keyboard('{Enter}');
    expect(cell(2, 2)).toHaveAccessibleName(/crossed out$/);
  });

  it('asks for a hint, marks the cell, and counts it', async () => {
    const server = fakeServer({
      'GET /api/games/stamps/puzzle': puzzle(),
      'POST /api/games/stamps/hint': {
        cell: 0,
        stamp: true,
        wrong: false,
        reason: 'Only one open cell is left in this row.',
      },
    });
    renderGame();
    await screen.findByRole('heading', { name: 'Stamps #9' });
    layOutBoard();

    tap(2, 2);
    fireEvent.click(screen.getByRole('button', { name: 'Hint' }));

    expect(
      await screen.findByText('Stamp here: Only one open cell is left in this row.'),
    ).toBeInTheDocument();
    expect(cell(1, 1)).toHaveAccessibleName('Row 1, column 1, region A, empty, hint');
    expect(screen.getByText('1 hint')).toBeInTheDocument();
    expect(server.sent('POST', '/api/games/stamps/hint')[0]?.body).toEqual({
      date: localDate(),
      stamps: [],
      crosses: [6],
    });
  });

  it('sends the stamps, time, and hints when solved, then shows the passport page', async () => {
    const server = fakeServer({
      'GET /api/games/stamps/puzzle': puzzle(),
      'POST /api/games/stamps/results': result,
    });
    const { container } = renderGame();
    await screen.findByRole('heading', { name: 'Stamps #9' });
    layOutBoard();

    for (const stamp of SOLUTION) {
      const row = Math.floor(stamp / 5) + 1;
      const column = (stamp % 5) + 1;
      tap(row, column);
      tap(row, column);
    }

    expect(await screen.findByText(/Solved in 2:41/, {}, { timeout: 3000 })).toBeInTheDocument();
    const post = server.sent('POST', '/api/games/stamps/results')[0];
    expect(post?.body).toMatchObject({ date: localDate(), stamps: SOLUTION, hints: 0 });
    expect((post?.body as { seconds: number }).seconds).toBeGreaterThanOrEqual(1);

    const page = screen.getByRole('region', { name: 'Passport' });
    const stamps = within(page).getAllByRole('listitem');
    expect(stamps).toHaveLength(5);
    expect(within(stamps[0] as HTMLElement).getByRole('link', { name: 'Trip 1' })).toHaveAttribute(
      'href',
      '/trips/10',
    );
    expect(within(stamps[0] as HTMLElement).getByText('Apr 2020')).toBeInTheDocument();
    expect(
      within(stamps[0] as HTMLElement).getByRole('link', {
        name: 'Open the photo from Trip 1 in Immich',
      }),
    ).toHaveAttribute('href', 'https://photos.example.com/photos/photo-0');
    expect(screen.getByText('best 2:41, average 2:41')).toBeInTheDocument();
    expect(screen.queryByText('Win %')).not.toBeInTheDocument();
    await expectNoAccessibilityProblems(container);
  });

  it('picks up a board saved earlier today', async () => {
    saveGame({
      date: localDate(),
      cells: Array.from({ length: 25 }, (_, index) => (index === 0 ? 'stamp' : 'empty')),
      seconds: 75,
      hints: 2,
    });
    fakeServer({ 'GET /api/games/stamps/puzzle': puzzle() });
    renderGame();
    await screen.findByRole('heading', { name: 'Stamps #9' });

    expect(cell(1, 1)).toHaveAccessibleName('Row 1, column 1, region A, stamp');
    expect(screen.getByText('2 hints')).toBeInTheDocument();
    expect(screen.getByLabelText(/^Time 1:1\d$/)).toBeInTheDocument();
  });

  it('shows the passport page when today is already played', async () => {
    fakeServer({ 'GET /api/games/stamps/puzzle': puzzle(result) });
    renderGame();

    expect(await screen.findByText('Solved in 2:41 · no hints')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Trip 3' })).toBeInTheDocument();
  });

  it('says how many trips are missing', async () => {
    fakeServer({
      'GET /api/games/stamps/puzzle': {
        ...puzzle(),
        tripsNeeded: 2,
        size: 0,
        regions: [],
        photos: [],
      },
    });
    renderGame();

    expect(await screen.findByText(/Add 2 more trips/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open the inbox' })).toHaveAttribute('href', '/inbox');
  });
});
