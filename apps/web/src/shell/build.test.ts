// Checks on the files that make the app installable and private. These read the source files,
// so a change that breaks a rule of the app shell spec fails here and names the file.

import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

import { describe, expect, it } from 'vitest';

import screens from '../../splash-screens.json';
import { splashLinks } from '../../vite-plugins/splash-screens';

const root = path.resolve(import.meta.dirname, '../..');
const read = (file: string) => readFileSync(path.join(root, file), 'utf8');

function filesIn(folder: string): string[] {
  return readdirSync(path.join(root, folder), { withFileTypes: true, recursive: true })
    .filter((entry) => entry.isFile())
    .map((entry) => path.relative(root, path.join(entry.parentPath, entry.name)));
}

describe('the web app manifest', () => {
  const manifest = JSON.parse(read('public/manifest.webmanifest')) as {
    name: string;
    display: string;
    theme_color: string;
    background_color: string;
    icons: { src: string; sizes: string; purpose: string }[];
  };

  it('names the app, opens standalone, and uses the brand colors', () => {
    expect(manifest.name).toBe('Fibotrail');
    expect(manifest.display).toBe('standalone');
    expect(manifest.theme_color).toBe('#0B3C49');
    expect(manifest.background_color).toBe('#0B3C49');
  });

  it('lists icons that exist, including a maskable one', () => {
    const icons = filesIn('public/icons');
    for (const icon of manifest.icons) {
      expect(icons).toContain(`public${icon.src}`);
    }
    expect(manifest.icons.some((icon) => icon.purpose === 'maskable')).toBe(true);
    expect(manifest.icons.some((icon) => icon.sizes === '512x512')).toBe(true);
  });
});

describe('index.html', () => {
  const html = read('index.html');

  it('asks iOS for standalone mode, the safe areas, and a home screen icon', () => {
    expect(html).toContain('name="apple-mobile-web-app-capable" content="yes"');
    expect(html).toContain('viewport-fit=cover');
    expect(html).toContain('rel="apple-touch-icon" href="/icons/apple-touch-icon.png"');
    expect(html).toContain('rel="manifest" href="/manifest.webmanifest"');
    expect(filesIn('public/icons')).toContain('public/icons/apple-touch-icon.png');
  });

  it('never disables zoom, so the system text size and pinch still work', () => {
    expect(html).not.toMatch(/user-scalable|maximum-scale/);
  });

  it('has a launch image on disk for every screen in the list', () => {
    const links = splashLinks(screens);
    const images = filesIn('public/splash');
    const hrefs = [...links.matchAll(/href="([^"]+)"/g)].map((match) => `public${match[1]}`);
    expect(hrefs.length).toBeGreaterThan(screens.length);
    expect(images.sort()).toEqual([...hrefs].sort());
    expect(links).toContain(
      'media="(device-width: 393px) and (device-height: 852px) and (-webkit-device-pixel-ratio: 3) and (orientation: portrait)" href="/splash/splash-1179x2556.png"',
    );
  });
});

describe('privacy', () => {
  it('no source file names an outside host, so the browser talks only to Fibotrail', () => {
    const sources = [
      'index.html',
      'public/manifest.webmanifest',
      ...filesIn('src').filter((file) => !/\.test\.tsx?$/.test(file) && !file.endsWith('.json')),
    ];
    // The SVG namespace is a name, not an address that is requested.
    const allowed = ['http://www.w3.org/2000/svg', 'http://localhost'];
    const found = sources.flatMap((file) =>
      (read(file).match(/(https?:)?\/\/[a-z0-9.-]+\.[a-z]{2,}[^\s"'`)]*/gi) ?? [])
        .filter((url) => !allowed.some((prefix) => url.startsWith(prefix)))
        .map((url) => `${file}: ${url}`),
    );
    expect(found).toEqual([]);
  });

  it('the service worker never caches an API answer', () => {
    const worker = read('src/service-worker.js');
    expect(worker).toContain("'/api/'");
    expect(worker).toContain('response.redirected');
  });
});

describe('design tokens', () => {
  const styles = filesIn('src/styles').filter((file) => !file.endsWith('tokens.css'));

  it('are the only source of colors, lengths, and durations', () => {
    const problems = styles.flatMap((file) =>
      read(file)
        .replace(/\/\*[\s\S]*?\*\//g, '')
        .split('\n')
        .flatMap((line, index) => {
          const literals =
            line.match(/#[0-9a-f]{3,8}\b|\brgba?\(|\bhsla?\(|\b\d*\.?\d+(px|ms|rem|em)\b/gi) ?? [];
          // 1px is the size of the hidden text for screen readers, not a design value.
          return literals
            .filter((literal) => literal !== '1px')
            .map((literal) => `${file}:${index + 1}: "${literal}" must be a token from tokens.css`);
        }),
    );
    expect(styles.length).toBeGreaterThan(0);
    expect(problems).toEqual([]);
  });

  it('swap for dark mode and for reduce motion', () => {
    const tokens = read('src/styles/tokens.css');
    expect(tokens).toMatch(/prefers-color-scheme: dark[\s\S]*--fibo-paper: #0e1a1d/);
    expect(tokens).toMatch(/prefers-reduced-motion: reduce[\s\S]*--fibo-dur-page: 0ms/);
    expect(tokens).toContain('--fibo-dur-page: 377ms');
    expect(tokens).toContain('--fibo-dur-base: 233ms');
  });
});

describe('iOS behavior', () => {
  const base = read('src/styles/base.css');
  const shell = read('src/styles/shell.css');

  it('keeps the page from scrolling, bouncing, or highlighting taps', () => {
    expect(base).toContain('overscroll-behavior: none');
    expect(base).toContain('-webkit-tap-highlight-color: transparent');
    expect(base).toContain('touch-action: manipulation');
    expect(base).toContain('user-select: none');
  });

  it('keeps every scroll area out of a stacking layer, so dialogs rise above the tab bar', () => {
    // -webkit-overflow-scrolling: touch traps a fixed child under the tab bar on iOS (#38).
    const styles = filesIn('src/styles').map((file) => ({ file, text: read(file) }));
    const found = styles
      .filter(({ text }) =>
        /-webkit-overflow-scrolling/.test(text.replace(/\/\*[\s\S]*?\*\//g, '')),
      )
      .map(({ file }) => file);
    expect(found).toEqual([]);
  });

  it('never lets a photo tile grow to fill its row, which would distort its proportions', () => {
    // #41: with flex-grow, every tile in a row got the same width and the photos were cropped wide.
    const css = read('src/styles/trip.css').replace(/\/\*[\s\S]*?\*\//g, '');
    const rule = css.match(/\.photo-tile\s*\{([^}]*)\}/)?.[1] ?? '';
    expect(rule).toMatch(/flex:\s*none/);
    expect(rule).not.toMatch(/flex-grow|flex:\s*[1-9]/);
  });

  it('respects the safe area at every edge', () => {
    for (const edge of ['top', 'right', 'bottom', 'left']) {
      expect(shell).toContain(`env(safe-area-inset-${edge})`);
    }
  });

  it('gives inputs at least 16 px and tap targets at least 44 px', () => {
    const tokens = read('src/styles/tokens.css');
    expect(tokens).toContain('--fibo-text-input: max(16px, 1rem)');
    expect(tokens).toContain('--fibo-tap: 44px');
    expect(base).toContain('font-size: var(--fibo-text-input)');
    expect(shell).toContain('min-height: var(--fibo-tap)');
  });
});
