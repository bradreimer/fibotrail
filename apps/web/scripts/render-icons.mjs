// Draws the app icons and the iOS launch images, and writes them to public/.
// Run it with "npm run icons" after you change the drawing. The PNG files are committed.
//
// The spiral is the logo construction from the design and branding document: quarter circles
// through Fibonacci squares 8, 5, 3, 2, 1, and 1, with a coral pin at the outer end.

import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import sharp from 'sharp';

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');
const OCEAN = '#0B3C49';
const GOLD = '#E0A526';
const CORAL = '#E4684A';
const PAPER = '#F7F3EA';

// The spiral fits a 13 by 8 box. The outer end is at (0, 8).
const SPIRAL =
  'M0 8A8 8 0 0 1 8 0A5 5 0 0 1 13 5A3 3 0 0 1 10 8A2 2 0 0 1 8 6A1 1 0 0 1 9 5A1 1 0 0 1 10 6';

/** The spiral and its pin, centered in a box of the given size. */
function mark(size, scale, { pin = true } = {}) {
  const unit = (size * scale) / 15;
  const x = (size - 13 * unit) / 2;
  const y = (size - 8 * unit) / 2;
  const spiral = `<path d="${SPIRAL}" fill="none" stroke="${GOLD}" stroke-width="0.7" stroke-linecap="round"/>`;
  const dot = `<circle cx="0" cy="8" r="1.1" fill="${CORAL}"/><circle cx="0" cy="8" r="0.4" fill="${PAPER}"/>`;
  return `<g transform="translate(${x} ${y}) scale(${unit})">${spiral}${pin ? dot : ''}</g>`;
}

function svg(width, height, body) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">${body}</svg>\n`;
}

/** A square icon. "scale" is the share of the width that the mark takes. */
function icon(size, scale, options) {
  return svg(
    size,
    size,
    `<rect width="${size}" height="${size}" fill="${OCEAN}"/>${mark(size, scale, options)}`,
  );
}

/** A launch image: the mark in the middle of a Deep Ocean screen. */
function splash(width, height) {
  const size = Math.min(width, height) * 0.4;
  const inner = mark(size, 1).replace(
    '<g transform="',
    `<g transform="translate(${(width - size) / 2} ${(height - size) / 2}) `,
  );
  return svg(width, height, `<rect width="${width}" height="${height}" fill="${OCEAN}"/>${inner}`);
}

async function png(file, source) {
  await sharp(Buffer.from(source))
    .png({ compressionLevel: 9, palette: true })
    .toFile(path.join(root, 'public', file));
}

mkdirSync(path.join(root, 'public/icons'), { recursive: true });
mkdirSync(path.join(root, 'public/splash'), { recursive: true });

// The favicon is the simplified mark with no pin, so it stays readable when small.
const favicon = svg(
  64,
  64,
  `<rect width="64" height="64" rx="13" fill="${OCEAN}"/>${mark(64, 0.8, { pin: false })}`,
);
writeFileSync(path.join(root, 'public/icons/favicon.svg'), favicon);

await png('icons/apple-touch-icon.png', icon(180, 0.72));
await png('icons/icon-192.png', icon(192, 0.72));
await png('icons/icon-512.png', icon(512, 0.72));
// A maskable icon can be cropped to a circle, so its mark stays inside the middle 80%.
await png('icons/icon-maskable-512.png', icon(512, 0.56));

const screens = JSON.parse(readFileSync(path.join(root, 'splash-screens.json'), 'utf8'));
for (const screen of screens) {
  const w = screen.width * screen.ratio;
  const h = screen.height * screen.ratio;
  await png(`splash/splash-${w}x${h}.png`, splash(w, h));
  if (screen.landscape) await png(`splash/splash-${h}x${w}.png`, splash(h, w));
}
