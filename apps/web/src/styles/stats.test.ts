// Stats screen layout. Intention: on a phone, the Stats screen fits the screen width, and only the
// continent row scrolls sideways. Method: read the CSS. A grid with no column template gets one
// auto-sized column, which grows to its widest child, so the screen must set a shrinkable one.

import { readFileSync } from 'node:fs';
import path from 'node:path';

import { describe, expect, it } from 'vitest';

const css = readFileSync(path.join(import.meta.dirname, 'stats.css'), 'utf8').replace(
  /\/\*[\s\S]*?\*\//g,
  '',
);

/** Returns the declarations inside the first rule whose selector is exactly `selector`. */
function rule(selector: string): string {
  const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const match = css.match(new RegExp(`(?:^|\\})\\s*${escaped}\\s*\\{([^}]*)\\}`));
  return match?.[1] ?? '';
}

describe('stats screen layout', () => {
  it('uses one column that can shrink below its content', () => {
    expect(rule('.stats-screen')).toMatch(/grid-template-columns:\s*minmax\(0,\s*1fr\)/);
  });

  it('scrolls the continent row sideways', () => {
    expect(rule('.continents')).toMatch(/overflow-x:\s*auto/);
  });
});
