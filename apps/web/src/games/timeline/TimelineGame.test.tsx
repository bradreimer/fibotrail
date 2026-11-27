import { act, fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { expectNoAccessibilityProblems } from '../../accessibility/accessibility';
import { App } from '../../App';
import { fakeServer, status } from '../../test/server';
import { installMatchMedia } from '../../test/window';
import { localDate } from '../today';
import { HOLD_MS } from './TimelineBoard';

const answer = ['a', 'b', 'c', 'd', 'e'];
const names: Record<string, string> = {
  a: 'Orlando, March 2024',
  b: 'Lisbon',
  c: 'Paris',
  d: 'Rome',
  e: 'Tokyo',
};

const TWISTS = {
  classic: { name: 'Classic', rule: 'Green is in place. Yellow is one place away.', tries: 4 },
  arrows: {
    name: 'Arrows',
    rule: 'A photo out of place shows whether it goes earlier or later.',
    tries: 4,
  },
  countOnly: {
    name: 'Count only',
    rule: 'A check shows only how many photos are in place, not which ones. You get 6 tries.',
    tries: 6,
  },
} as const;

type TwistId = keyof typeof TWISTS;

const puzzle = (result: unknown = null, twist: TwistId = 'classic') => ({
  number: 2,
  date: localDate(),
  twist: { id: twist, planned: twist, name: TWISTS[twist].name, rule: TWISTS[twist].rule },
  maxTries: TWISTS[twist].tries,
  tripsNeeded: 0,
  cards: ['b', 'a', 'c', 'd', 'e'].map((id) => ({ photoId: id })),
  result,
});

const result = {
  solved: true,
  tries: 2,
  maxTries: 4,
  rows: [
    ['close', 'close', 'right', 'right', 'right'],
    ['right', 'right', 'right', 'right', 'right'],
  ],
  share: 'Fibotrail Timeline #2 2/4\n🟨🟨🟩🟩🟩\n🟩🟩🟩🟩🟩',
  answer: answer.map((id, index) => ({
    photoId: id,
    tripId: index + 10,
    tripName: names[id],
    date: `202${index}-06-01`,
    immichUrl: `https://photos.example.com/photos/${id}`,
  })),
  stats: { played: 1, winPercent: 100, currentStreak: 1, bestStreak: 1, tries: [0, 1, 0, 0] },
};

/** Grades an order the way the server does, for a twist. */
function gradeFor(twist: TwistId) {
  return (init?: RequestInit) => {
    const { order } = JSON.parse(String(init?.body)) as { order: string[] };
    const marks = order.map((id, index) => {
      const off = answer.indexOf(id) - index;
      if (off === 0) return 'right';
      if (twist === 'arrows') return off < 0 ? 'earlier' : 'later';
      return Math.abs(off) === 1 && twist === 'classic' ? 'close' : 'wrong';
    });
    if (twist !== 'countOnly') return { marks };
    // Count only puts the Right marks first, so the row doesn't say which cards they are.
    const right = marks.filter((mark) => mark === 'right');
    return { marks: [...right, ...marks.filter((mark) => mark !== 'right')] };
  };
}

const grade = gradeFor('classic');

function renderGame() {
  return render(
    <MemoryRouter initialEntries={['/games/timeline']}>
      <App />
    </MemoryRouter>,
  );
}

function board() {
  return document.querySelector('.timeline__cards') as HTMLElement;
}

/** The photo IDs of the cards, top to bottom. */
function cardIds() {
  return within(board())
    .getAllByRole('img')
    .map((image) => image.getAttribute('src')!.split('/')[3]);
}

function card(index: number) {
  return within(board()).getAllByRole('listitem')[index]!;
}

/** Lays the cards out 100 px apart, 90 px tall, as a phone would. */
function layOutCards() {
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (
    this: HTMLElement,
  ) {
    const index = this.parentElement ? Array.from(this.parentElement.children).indexOf(this) : 0;
    const top = this.classList.contains('timeline-card') ? index * 100 : 0;
    return {
      top,
      height: 90,
      bottom: top + 90,
      left: 0,
      right: 300,
      width: 300,
      x: 0,
      y: top,
      toJSON: () => ({}),
    };
  });
}

beforeEach(() => installMatchMedia(390));
afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('the Timeline game', () => {
  it('shows photos with no trip name or number, and solves by dragging', async () => {
    const server = fakeServer({
      'GET /api/games/timeline/puzzle': puzzle(),
      'POST /api/games/timeline/check': grade,
      'POST /api/games/timeline/results': result,
    });
    const { container } = renderGame();

    expect(await screen.findByRole('heading', { name: 'Timeline #2' })).toBeInTheDocument();
    expect(cardIds()).toEqual(['b', 'a', 'c', 'd', 'e']);
    for (const name of Object.values(names)) {
      expect(board()).not.toHaveTextContent(name);
    }
    expect(board().textContent).not.toMatch(/[1-5]/);
    expect(screen.getByText(/4 tries left/)).toBeInTheDocument();
    await expectNoAccessibilityProblems(container);

    await userEvent.click(screen.getByRole('button', { name: 'Check' }));
    expect(await screen.findByText('3 right, 2 close, 0 wrong')).toBeInTheDocument();
    expect(within(card(2)).getByText('Right')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Move card 3 up' })).toBeDisabled();

    // Drag the first card down one place with a mouse.
    layOutCards();
    fireEvent.pointerDown(card(0), { pointerId: 1, pointerType: 'mouse', button: 0, clientY: 45 });
    fireEvent.pointerMove(card(0), { pointerId: 1, pointerType: 'mouse', clientY: 145 });
    expect(card(1)).toHaveClass('timeline-card--dragging');
    fireEvent.pointerUp(card(1), { pointerId: 1, pointerType: 'mouse', clientY: 145 });
    expect(cardIds()).toEqual(['a', 'b', 'c', 'd', 'e']);

    await userEvent.click(screen.getByRole('button', { name: 'Check' }));
    expect(await screen.findByText('Solved in 2 tries.')).toBeInTheDocument();
    const sent = server.sent('POST', '/api/games/timeline/results')[0]?.body as {
      guesses: string[][];
    };
    expect(sent.guesses).toEqual([
      ['b', 'a', 'c', 'd', 'e'],
      ['a', 'b', 'c', 'd', 'e'],
    ]);
  });

  it('lifts a card after a press and hold on a touch screen', async () => {
    fakeServer({ 'GET /api/games/timeline/puzzle': puzzle() });
    renderGame();
    await screen.findByRole('heading', { name: 'Timeline #2' });
    layOutCards();
    vi.useFakeTimers();

    fireEvent.pointerDown(card(4), { pointerId: 2, pointerType: 'touch', clientY: 445 });
    act(() => vi.advanceTimersByTime(HOLD_MS));
    fireEvent.pointerMove(card(4), { pointerId: 2, pointerType: 'touch', clientY: 45 });
    fireEvent.pointerUp(card(0), { pointerId: 2, pointerType: 'touch', clientY: 45 });

    expect(cardIds()).toEqual(['e', 'b', 'a', 'c', 'd']);
  });

  it('lets a quick swipe scroll instead of dragging', async () => {
    fakeServer({ 'GET /api/games/timeline/puzzle': puzzle() });
    renderGame();
    await screen.findByRole('heading', { name: 'Timeline #2' });
    layOutCards();
    vi.useFakeTimers();

    fireEvent.pointerDown(card(4), { pointerId: 3, pointerType: 'touch', clientY: 445 });
    fireEvent.pointerMove(card(4), { pointerId: 3, pointerType: 'touch', clientY: 300 });
    act(() => vi.advanceTimersByTime(HOLD_MS));
    fireEvent.pointerUp(card(4), { pointerId: 3, pointerType: 'touch', clientY: 100 });

    expect(cardIds()).toEqual(['b', 'a', 'c', 'd', 'e']);
    expect(document.querySelector('.timeline-card--dragging')).toBeNull();
  });

  it('shows each trip with its date and a link at the end', async () => {
    fakeServer({ 'GET /api/games/timeline/puzzle': puzzle(result) });
    const { container } = renderGame();

    expect(await screen.findByText('Solved in 2 tries.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Check' })).not.toBeInTheDocument();
    expect(screen.getByLabelText('Try 1: 3 right, 2 close, 0 wrong')).toBeInTheDocument();
    const orlando = screen.getByRole('link', { name: /^Orlando, March 2024/ });
    expect(orlando).toHaveAttribute('href', '/trips/10');
    expect(orlando).toHaveTextContent('Jun 1, 2020');
    expect(screen.getByRole('link', { name: /^Tokyo/ })).toHaveAttribute('href', '/trips/14');
    await expectNoAccessibilityProblems(container);
  });

  it('opens each answer photo in Immich, in a new tab', async () => {
    fakeServer({ 'GET /api/games/timeline/puzzle': puzzle(result) });
    const { container } = renderGame();

    const photo = await screen.findByRole('link', {
      name: 'Open the photo from Orlando, March 2024 in Immich',
    });
    expect(photo).toHaveAttribute('href', 'https://photos.example.com/photos/a');
    expect(photo).toHaveAttribute('target', '_blank');
    expect(photo).toHaveAttribute('rel', 'noopener noreferrer');
    expect(screen.getByText('Tap a photo to open it in Immich.')).toBeInTheDocument();
    await expectNoAccessibilityProblems(container);
  });

  it('opens the trip from the photo when there is no Immich address', async () => {
    fakeServer({
      'GET /api/games/timeline/puzzle': puzzle({
        ...result,
        answer: result.answer.map((card) => ({ ...card, immichUrl: null })),
      }),
    });
    const { container } = renderGame();

    expect(await screen.findByText('Solved in 2 tries.')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /in Immich/ })).not.toBeInTheDocument();
    expect(screen.queryByText('Tap a photo to open it in Immich.')).not.toBeInTheDocument();
    const photoLink = container.querySelector('.timeline-answer__photo-link');
    expect(photoLink).toHaveAttribute('href', '/trips/10');
    await expectNoAccessibilityProblems(container);
  });

  it('shows the problem when a check fails', async () => {
    fakeServer({
      'GET /api/games/timeline/puzzle': puzzle(),
      'POST /api/games/timeline/check': status(400, { detail: 'Only today can be played.' }),
    });
    renderGame();

    await userEvent.click(await screen.findByRole('button', { name: 'Check' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Only today can be played.');
  });

  it("names the day's twist and its rule", async () => {
    fakeServer({ 'GET /api/games/timeline/puzzle': puzzle() });
    renderGame();

    expect(await screen.findByText('Classic:')).toBeInTheDocument();
    expect(screen.getByText(/Green is in place/)).toBeInTheDocument();
  });

  it('shows arrows on cards out of place on an Arrows day', async () => {
    fakeServer({
      'GET /api/games/timeline/puzzle': puzzle(null, 'arrows'),
      'POST /api/games/timeline/check': gradeFor('arrows'),
    });
    const { container } = renderGame();
    expect(await screen.findByText('Arrows:')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Check' }));

    expect(await screen.findByText('3 right, 1 earlier, 1 later')).toBeInTheDocument();
    expect(within(card(0)).getByText('↓ Later')).toBeInTheDocument();
    expect(within(card(1)).getByText('↑ Earlier')).toBeInTheDocument();
    expect(card(0)).toHaveClass('timeline-card--later');
    await expectNoAccessibilityProblems(container);
  });

  it('says only how many are in place on a Count only day, and locks nothing', async () => {
    fakeServer({
      'GET /api/games/timeline/puzzle': puzzle(null, 'countOnly'),
      'POST /api/games/timeline/check': gradeFor('countOnly'),
    });
    renderGame();
    expect(await screen.findByText(/6 tries left/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Check' }));

    expect(await screen.findByText('Last check: 3 of 5 in place.')).toBeInTheDocument();
    expect(screen.getByLabelText('Try 1: 3 of 5 in place')).toBeInTheDocument();
    expect(screen.getByText(/5 tries left/)).toBeInTheDocument();
    expect(board().querySelector('.timeline-card__mark')).toBeNull();
    expect(board().querySelector('.timeline-card--locked')).toBeNull();
    expect(screen.getByRole('button', { name: 'Move card 3 up' })).toBeEnabled();
  });
});
