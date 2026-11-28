import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';

import { expectNoAccessibilityProblems } from '../accessibility/accessibility';
import { App } from '../App';
import { fakeServer } from '../test/server';
import { installMatchMedia } from '../test/window';
import { dailyLine } from './DailyRing';
import { GAMES } from './registry';
import { localDate } from './today';

const stats = { played: 6, winPercent: 83, currentStreak: 3, bestStreak: 4, tries: [1, 2, 2, 0] };

const timeline = {
  id: 'timeline',
  name: 'Timeline',
  description: 'Put five of your photos in the order you took them.',
  playedToday: false,
  solvedToday: null,
  stats,
};

function renderAt(where: string) {
  return render(
    <MemoryRouter initialEntries={[where]}>
      <App />
    </MemoryRouter>,
  );
}

beforeEach(() => installMatchMedia(390));

describe('the Games screen', () => {
  it('is a tab, and shows a card for each game with its state and streak', async () => {
    const server = fakeServer({ 'GET /api/games': [timeline] });
    const { container } = renderAt('/games');

    const card = await screen.findByRole('link', { name: /Timeline/ });
    expect(card).toHaveAttribute('href', '/games/timeline');
    expect(card).toHaveTextContent("Today's puzzle is ready · 3-day streak");
    expect(screen.getByRole('link', { name: 'Games' })).toBeInTheDocument();
    expect(server.requests.some((request) => request.path.startsWith('/api/games?date='))).toBe(
      true,
    );
    await expectNoAccessibilityProblems(container);
  });

  it('shows only games that both the server and the registry list', async () => {
    fakeServer({ 'GET /api/games': [{ ...timeline, id: 'not-in-the-web-app', name: 'Ghost' }] });
    renderAt('/games');

    await screen.findByText('A new puzzle each day, made from your trips.');
    expect(screen.queryByText('Ghost')).not.toBeInTheDocument();
  });

  it('shows a timed game’s best time on its card, and no time on other cards', async () => {
    fakeServer({
      'GET /api/games': [
        timeline,
        {
          ...timeline,
          id: 'stamps',
          name: 'Stamps',
          stats: {
            ...stats,
            times: [
              { label: '6 × 6', days: 2, best: 95, average: 120 },
              { label: '7 × 7', days: 1, best: 161, average: 161 },
            ],
          },
        },
      ],
    });
    renderAt('/games');

    const card = await screen.findByRole('link', { name: /Stamps/ });
    expect(card).toHaveTextContent("Today's puzzle is ready · 3-day streak · best 1:35");
    expect(screen.getByRole('link', { name: /Timeline/ })).not.toHaveTextContent('best');
  });

  it('shows today’s progress, counting lost games and only games both registries list', async () => {
    fakeServer({
      'GET /api/games': [
        { ...timeline, playedToday: true, solvedToday: false },
        { ...timeline, id: 'slide', name: 'Slide' },
        { ...timeline, id: 'not-in-the-web-app', name: 'Ghost', playedToday: true },
      ],
    });
    const { container } = renderAt('/games');

    expect(await screen.findByText('1 of 2 done today')).toBeInTheDocument();
    expect(container.querySelector('.daily-ring__ring')).toHaveAttribute('aria-hidden', 'true');
    await expectNoAccessibilityProblems(container);
  });

  it('says when every game is done for today', async () => {
    fakeServer({
      'GET /api/games': [
        { ...timeline, playedToday: true, solvedToday: true },
        { ...timeline, id: 'slide', name: 'Slide', playedToday: true, solvedToday: true },
      ],
    });
    renderAt('/games');

    expect(await screen.findByText('All done for today ✨')).toBeInTheDocument();
  });

  it('says when today is solved', async () => {
    fakeServer({ 'GET /api/games': [{ ...timeline, playedToday: true, solvedToday: true }] });
    renderAt('/games');

    expect(await screen.findByText(/Solved today/)).toBeInTheDocument();
  });

  it('opens a game from its card, with a back link to Games', async () => {
    fakeServer({
      'GET /api/games': [timeline],
      'GET /api/games/timeline/puzzle': {
        number: 2,
        date: localDate(),
        twist: { id: 'classic', planned: 'classic', name: 'Classic', rule: 'Green is in place.' },
        maxTries: 4,
        tripsNeeded: 2,
        cards: [],
        result: null,
      },
    });
    renderAt('/games');

    await userEvent.click(await screen.findByRole('link', { name: /Timeline/ }));

    expect(await screen.findByRole('heading', { name: 'Timeline #2' })).toBeInTheDocument();
    expect(screen.getByText(/Add 2 more trips/)).toBeInTheDocument();
    const page = document.querySelector('.games-page') as HTMLElement;
    expect(within(page).getByRole('link', { name: 'Games' })).toHaveAttribute('href', '/games');
  });

  it('opens the Games screen for an unknown game', async () => {
    fakeServer({ 'GET /api/games': [timeline] });
    renderAt('/games/nothing-here');

    expect(await screen.findByRole('link', { name: /Timeline/ })).toBeInTheDocument();
  });
});

describe('the daily ring', () => {
  it('counts played games, and says when all are done', () => {
    expect(dailyLine(0, 5)).toBe('0 of 5 done today');
    expect(dailyLine(3, 5)).toBe('3 of 5 done today');
    expect(dailyLine(5, 5)).toBe('All done for today ✨');
    expect(dailyLine(0, 0)).toBe('0 of 0 done today');
  });
});

describe('the games registry', () => {
  it('lists games with unique IDs', () => {
    const ids = GAMES.map((game) => game.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("is the only file that imports a game's folder (ADR-022)", () => {
    const root = path.join(import.meta.dirname, '..');
    const gamesFolder = import.meta.dirname;
    const gameFolders = readdirSync(gamesFolder).filter((name) =>
      statSync(path.join(gamesFolder, name)).isDirectory(),
    );
    const files = (function walk(folder: string): string[] {
      return readdirSync(folder).flatMap((name) => {
        const full = path.join(folder, name);
        if (statSync(full).isDirectory()) return walk(full);
        return /\.(ts|tsx)$/.test(name) ? [full] : [];
      });
    })(root);

    const problems = files.flatMap((file) => {
      if (file === path.join(gamesFolder, 'registry.tsx')) return [];
      const source = readFileSync(file, 'utf8');
      const imports = [...source.matchAll(/from\s+'([^']+)'|import\(\s*'([^']+)'\s*\)/g)].map(
        (match) => match[1] ?? match[2] ?? '',
      );
      return imports.flatMap((target) => {
        if (!target.startsWith('.')) return [];
        const resolved = path.resolve(path.dirname(file), target);
        return gameFolders
          .filter((game) => {
            const folder = path.join(gamesFolder, game);
            const inside = (where: string) => where === folder || where.startsWith(`${folder}/`);
            return inside(resolved) && !inside(file);
          })
          .map((game) => `${path.relative(root, file)} imports the ${game} game`);
      });
    });
    expect(gameFolders.length).toBeGreaterThan(0);
    expect(problems).toEqual([]);
  });
});
