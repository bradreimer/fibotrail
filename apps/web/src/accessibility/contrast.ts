/**
 * Contrast math for the design tokens. Intention: check the WCAG 2.2 AA ratios without a browser.
 * Method: read the custom properties out of tokens.css, resolve var() and color-mix(in srgb), and
 * compute the relative luminance as WCAG defines it.
 */

export type Rgb = [number, number, number];

/** Reads `--name: value;` declarations from CSS text. Later declarations win. */
export function declarations(css: string): Map<string, string> {
  const map = new Map<string, string>();
  const clean = css.replace(/\/\*[\s\S]*?\*\//g, '');
  for (const match of clean.matchAll(/(--[a-z0-9-]+)\s*:\s*([^;]+);/g)) {
    map.set(match[1]!, match[2]!.trim().replace(/\s+/g, ' '));
  }
  return map;
}

function hex(value: string): Rgb {
  const digits = value.slice(1);
  const full = digits.length === 3 ? [...digits].map((d) => d + d).join('') : digits;
  return [0, 2, 4].map((i) => parseInt(full.slice(i, i + 2), 16)) as Rgb;
}

/** Resolves a token to a color. A color-mix with transparent is composited over `backdrop`. */
export function resolve(
  name: string,
  tokens: Map<string, string>,
  backdrop: Rgb = [255, 255, 255],
): Rgb {
  const raw = tokens.get(name);
  if (raw === undefined) throw new Error(`Token ${name} isn't defined`);
  return parse(raw, tokens, backdrop);
}

function parse(value: string, tokens: Map<string, string>, backdrop: Rgb): Rgb {
  const variable = value.match(/^var\((--[a-z0-9-]+)\)$/);
  if (variable) return resolve(variable[1]!, tokens, backdrop);
  if (value.startsWith('#')) return hex(value);

  // color-mix(in srgb, A 75%, B) or color-mix(in srgb, A 60%, transparent)
  const mix = value.match(/^color-mix\(in srgb, (.+?) (\d+)%, (.+)\)$/);
  if (mix) {
    const share = Number(mix[2]) / 100;
    const first = parse(mix[1]!, tokens, backdrop);
    const second = mix[3] === 'transparent' ? backdrop : parse(mix[3]!, tokens, backdrop);
    return first.map((c, i) => c * share + second[i]! * (1 - share)) as Rgb;
  }
  throw new Error(`Can't read the color "${value}"`);
}

function channel(value: number): number {
  const s = value / 255;
  return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
}

export function luminance([r, g, b]: Rgb): number {
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
}

/** The WCAG contrast ratio, from 1 to 21. */
export function contrastRatio(a: Rgb, b: Rgb): number {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x) as [number, number];
  return (light + 0.05) / (dark + 0.05);
}
