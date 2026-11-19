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

const puzzle = (result: unknown = null) => ({
  number: 2,
  date: localDate(),
  maxTries: 4,
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
  })),
  stats: { played: 1, winPercent: 100, currentStreak: 1, bestStreak: 1, tries: [0, 1, 0, 0] },
};

/** Grades an order the way the server does. */
function grade(init?: RequestInit) {
  const { order } = JSON.parse(String(init?.body)) as { order: string[] };
  return {
    marks: order.map((id, index) => {
      const off = Math.abs(answer.indexOf(id) - index);
      return off === 0 ? 'right' : off === 1 ? 'close' : 'wrong';
    }),
  };
}

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
    const orlando = screen.getByRole('link', { name: /Orlando, March 2024/ });
    expect(orlando).toHaveAttribute('href', '/trips/10');
    expect(orlando).toHaveTextContent('Jun 1, 2020');
    expect(screen.getByRole('link', { name: /Tokyo/ })).toHaveAttribute('href', '/trips/14');
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
});
