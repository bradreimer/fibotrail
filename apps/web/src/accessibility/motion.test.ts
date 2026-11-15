// Reduce motion. Intention: with the system setting on, nothing animates. Method: read the CSS.
// Every animation and transition must take its duration from a --fibo-dur-* token, and the tokens
// are 0ms under prefers-reduced-motion. An animation that repeats forever must also be switched
// off by name in a reduce-motion block, because a 0ms loop is still a loop.

import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

import { describe, expect, it } from 'vitest';

const folder = path.join(import.meta.dirname, '../styles');
const files = readdirSync(folder).filter((file) => file.endsWith('.css'));
const read = (file: string) =>
  readFileSync(path.join(folder, file), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');

/** Returns the text inside every `@media (prefers-reduced-motion: reduce) { ... }` block. */
function reduceBlocks(css: string): string[] {
  const blocks: string[] = [];
  for (const match of css.matchAll(/@media \(prefers-reduced-motion: reduce\)\s*\{/g)) {
    let depth = 1;
    let index = match.index! + match[0].length;
    const start = index;
    while (depth > 0 && index < css.length) {
      depth += css[index] === '{' ? 1 : css[index] === '}' ? -1 : 0;
      index++;
    }
    blocks.push(css.slice(start, index - 1));
  }
  return blocks;
}

describe('reduce motion', () => {
  it('sets every duration token to 0ms and the shift to 0px', () => {
    const block = reduceBlocks(read('tokens.css')).join('\n');
    for (const token of ['fast', 'base', 'page', 'signature']) {
      expect(block, `--fibo-dur-${token}`).toMatch(new RegExp(`--fibo-dur-${token}:\\s*0ms`));
    }
    expect(block).toMatch(/--fibo-shift:\s*0px/);
  });

  it('takes every animation and transition duration from a token', () => {
    const problems: string[] = [];
    for (const file of files) {
      for (const match of read(file).matchAll(
        /(?:^|[\s;{])((?:animation|transition)(?:-duration)?):\s*([^;}]+)/g,
      )) {
        const [, property, value] = match;
        if (value!.trim() === 'none') continue;
        if (!/var\(--fibo-dur-[a-z]+\)/.test(value!)) {
          problems.push(`${file}: "${property}: ${value!.trim()}" has no --fibo-dur-* duration`);
        }
      }
    }
    expect(problems).toEqual([]);
  });

  it('switches off every animation that repeats forever', () => {
    const problems: string[] = [];
    for (const file of files) {
      const css = read(file);
      const off = reduceBlocks(css).join('\n');
      for (const match of css.matchAll(/([^{}]+)\{[^{}]*animation:[^;}]*\binfinite\b[^;}]*/g)) {
        const selector = match[1]!.trim().split(',')[0]!.trim();
        if (!off.includes(selector)) {
          problems.push(
            `${file}: "${selector}" repeats forever and isn't switched off for reduce motion`,
          );
        }
      }
    }
    expect(problems).toEqual([]);
  });

  it('never smooth-scrolls', () => {
    for (const file of files) {
      const css = read(file);
      const outside = reduceBlocks(css).reduce((text, block) => text.replace(block, ''), css);
      expect(outside, file).not.toMatch(/scroll-behavior:\s*smooth/);
    }
  });
});
