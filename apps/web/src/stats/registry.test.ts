import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

import { VIEWS, viewsFor } from './registry';

const SRC = join(__dirname, '..');

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory()
      ? sourceFiles(path)
      : /\.(tsx?|mjs)$/.test(name)
        ? [path]
        : [];
  });
}

describe('the view registry', () => {
  it('lists each view once, with a heading', () => {
    expect(new Set(VIEWS.map((view) => view.id)).size).toBe(VIEWS.length);
    expect(VIEWS.every((view) => view.title.length > 0)).toBe(true);
  });

  it('picks the views of a slot, in order', () => {
    expect(viewsFor('stats').map((view) => view.id)).toEqual([
      'trips-per-year',
      'trip-length',
      'trip-spiral',
      'travel-clock',
      'distance-rings',
      'records',
    ]);
    expect(viewsFor('country').map((view) => view.id)).toEqual(['season-wheel']);
  });

  it("is the only thing outside a view's folder that imports it", () => {
    const offenders: string[] = [];
    for (const file of sourceFiles(SRC)) {
      const rel = relative(SRC, file).split(sep).join('/');
      if (rel === 'stats/registry.ts') continue;
      const inView = /^stats\/views\/([^/]+)\//.exec(rel)?.[1];
      const imports = [
        ...readFileSync(file, 'utf8').matchAll(/(?:from|import\()\s*['"]([^'"]+)['"]/g),
      ]
        .map((match) => match[1]!)
        .filter((specifier) => specifier.startsWith('.'));
      for (const specifier of imports) {
        const target = relative(SRC, join(file, '..', specifier))
          .split(sep)
          .join('/');
        const viewTarget = /^stats\/views\/([^/]+)(\/|$)/.exec(target)?.[1];
        if (viewTarget && viewTarget !== inView) offenders.push(`${rel} imports ${specifier}`);
      }
    }

    expect(offenders).toEqual([]);
  });
});
