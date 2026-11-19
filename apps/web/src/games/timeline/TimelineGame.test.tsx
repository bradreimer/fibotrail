import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';

import { expectNoAccessibilityProblems } from '../../accessibility/accessibility';
import { App } from '../../App';
import { fakeServer, status } from '../../test/server';
import { installMatchMedia } from '../../test/window';
import { localDate } from '../today';
import { moveCard, summary } from './TimelineGame';

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
  cards: ['b', 'a', 'c', 'd', 'e'].map((id) => ({ photoId: id, tripName: names[id] })),
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

function cardNames() {
  const list = document.querySelector('.timeline__cards') as HTMLElement;
  return within(list)
    .getAllByRole('listitem')
    .map((item) => item.querySelector('.timeline-card__name')!.firstChild!.textContent);
}

beforeEach(() => installMatchMedia(390));

describe('the Timeline game', () => {
  it('shows five cards, marks a try, and stores the result when solved', async () => {
    const server = fakeServer({
      'GET /api/games/timeline/puzzle': puzzle(),
      'POST /api/games/timeline/check': grade,
      'POST /api/games/timeline/results': result,
    });
    const { container } = renderGame();

    expect(await screen.findByRole('heading', { name: 'Timeline #2' })).toBeInTheDocument();
    expect(cardNames()).toEqual(['Lisbon', 'Orlando, March 2024', 'Paris', 'Rome', 'Tokyo']);
    expect(screen.getByText(/4 tries left/)).toBeInTheDocument();
    await expectNoAccessibilityProblems(container);

    await userEvent.click(screen.getByRole('button', { name: 'Check' }));

    expect(await screen.findByText('3 right, 2 close, 0 wrong')).toBeInTheDocument();
    expect(screen.getByText(/3 tries left/)).toBeInTheDocument();
    // A right card can't move.
    expect(screen.getByRole('button', { name: 'Move Paris up' })).toBeDisabled();

    await userEvent.click(screen.getByRole('button', { name: 'Move Orlando, March 2024 up' }));
    expect(cardNames()).toEqual(['Orlando, March 2024', 'Lisbon', 'Paris', 'Rome', 'Tokyo']);
    await userEvent.click(screen.getByRole('button', { name: 'Check' }));

    expect(await screen.findByText('Solved in 2 tries.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Share' })).toBeInTheDocument();
    expect(screen.getByText('Current streak')).toBeInTheDocument();
    const sent = server.sent('POST', '/api/games/timeline/results')[0]?.body as {
      guesses: string[][];
    };
    expect(sent.guesses).toEqual([
      ['b', 'a', 'c', 'd', 'e'],
      ['a', 'b', 'c', 'd', 'e'],
    ]);
  });

  it('shows the stored result when today is played', async () => {
    fakeServer({ 'GET /api/games/timeline/puzzle': puzzle(result) });
    const { container } = renderGame();

    expect(await screen.findByText('Solved in 2 tries.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Check' })).not.toBeInTheDocument();
    expect(screen.getByLabelText('Try 1: 3 right, 2 close, 0 wrong')).toBeInTheDocument();
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

describe('moving a card', () => {
  it('swaps with its neighbor and jumps over locked cards', () => {
    expect(moveCard(['a', 'b', 'c'], new Set(), 1, -1)).toEqual(['b', 'a', 'c']);
    expect(moveCard(['a', 'b', 'c'], new Set(['b']), 0, 1)).toEqual(['c', 'b', 'a']);
    expect(moveCard(['a', 'b', 'c'], new Set(['a']), 0, 1)).toEqual(['a', 'b', 'c']);
    expect(moveCard(['a', 'b', 'c'], new Set(), 0, -1)).toEqual(['a', 'b', 'c']);
  });

  it('summarizes marks', () => {
    expect(summary(['right', 'close', 'wrong', 'wrong'])).toBe('1 right, 1 close, 2 wrong');
  });
});
