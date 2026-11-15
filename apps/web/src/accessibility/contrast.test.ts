import { readFileSync } from 'node:fs';
import path from 'node:path';

import { describe, expect, it } from 'vitest';

import { contrastRatio, declarations, resolve, type Rgb } from './contrast';

const stylesDir = path.join(import.meta.dirname, '../styles');
const css = readFileSync(path.join(stylesDir, 'tokens.css'), 'utf8');
const lightBlock = css.slice(0, css.indexOf('@media (prefers-color-scheme: dark)'));
const darkBlock = css.slice(
  css.indexOf('@media (prefers-color-scheme: dark)'),
  css.indexOf('@media (prefers-reduced-motion'),
);

const light = declarations(lightBlock);
const dark = new Map([...light, ...declarations(darkBlock)]);

const TEXT = 4.5; // WCAG 2.2 AA, body text
const CONTROL = 3; // WCAG 2.2 AA, large text and the edges of controls

/** Each pair is a text or control color on the fill it sits on. */
const PAIRS: { text: string; fill: string; minimum: number; what: string }[] = [
  { text: '--fibo-ink', fill: '--fibo-paper', minimum: TEXT, what: 'body text' },
  { text: '--fibo-ink-muted', fill: '--fibo-paper', minimum: TEXT, what: 'muted text' },
  { text: '--fibo-ink', fill: '--fibo-mist', minimum: TEXT, what: 'text on a Mist fill' },
  { text: '--fibo-ink-muted', fill: '--fibo-mist', minimum: TEXT, what: 'muted text on Mist' },
  { text: '--fibo-teal', fill: '--fibo-paper', minimum: TEXT, what: 'links and Teal text' },
  { text: '--fibo-teal', fill: '--fibo-mist', minimum: CONTROL, what: 'Teal controls on Mist' },
  { text: '--fibo-on-teal', fill: '--fibo-teal', minimum: TEXT, what: 'text on a Teal button' },
  { text: '--fibo-on-ocean', fill: '--fibo-ocean', minimum: TEXT, what: 'text on Ocean' },
  {
    text: '--fibo-gold',
    fill: '--fibo-ocean',
    minimum: CONTROL,
    what: 'the focus ring on its halo',
  },
  { text: '--fibo-on-coral', fill: '--fibo-coral', minimum: TEXT, what: 'text on Coral' },
];

describe.each([
  ['light', light],
  ['dark', dark],
] as const)('contrast in %s mode', (_mode, tokens) => {
  it.each(PAIRS)('$what: $text on $fill is at least $minimum:1', ({ text, fill, minimum }) => {
    const ratio = contrastRatio(resolve(text, tokens), resolve(fill, tokens));
    expect(ratio, `${text} on ${fill} is ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(minimum);
  });

  it('keeps Paper text readable on the photo scrim over the worst photos, white and black', () => {
    const paper = resolve('--fibo-on-ocean', tokens);
    for (const photo of [
      [255, 255, 255],
      [0, 0, 0],
    ] as Rgb[]) {
      const scrim = resolve('--fibo-scrim-strong', tokens, photo);
      const ratio = contrastRatio(paper, scrim);
      expect(
        ratio,
        `Paper on the scrim over ${photo} is ${ratio.toFixed(2)}:1`,
      ).toBeGreaterThanOrEqual(TEXT);
    }
  });
});

describe('the focus ring', () => {
  const base = readFileSync(path.join(stylesDir, 'base.css'), 'utf8');
  const rule = base.match(/:focus-visible\s*\{([^}]*)\}/)?.[1] ?? '';

  it('is a Gold ring with an Ocean halo, so it shows on Paper, Ocean, and Ink', () => {
    expect(rule).toMatch(/outline:[^;]*var\(--fibo-gold\)/);
    expect(rule).toMatch(/box-shadow:[^;]*var\(--fibo-ocean\)/);
  });

  it('is thick enough to see', () => {
    expect(rule).toMatch(/outline:\s*var\(--fibo-marker\)/);
    expect(declarations(lightBlock).get('--fibo-marker')).toBe('3px');
  });
});

describe('contrast math', () => {
  it('gives 21:1 for black on white and 1:1 for a color on itself', () => {
    expect(contrastRatio([0, 0, 0], [255, 255, 255])).toBeCloseTo(21, 5);
    expect(contrastRatio([120, 30, 90], [120, 30, 90])).toBeCloseTo(1, 5);
  });

  it('resolves var() and color-mix', () => {
    const tokens = declarations(
      '--a: #ffffff; --b: #000000; --c: color-mix(in srgb, var(--a) 50%, var(--b));',
    );
    expect(resolve('--c', tokens)).toEqual([127.5, 127.5, 127.5]);
  });
});
