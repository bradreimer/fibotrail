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
