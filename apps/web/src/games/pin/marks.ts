import type { components } from '../../api/schema';

export type PinBand = components['schemas']['PinBand'];
export type Compass = components['schemas']['Compass'];
export type PinMark = components['schemas']['PinMark'];

/** The square of each band, as in the share text. */
export const SQUARE: Record<PinBand, string> = {
  green: '🟩',
  yellow: '🟨',
  orange: '🟧',
  red: '🟥',
};

/** The arrow toward the answer. */
export const ARROW: Record<NonNullable<Compass>, string> = {
  n: '↑',
  ne: '↗',
  e: '→',
  se: '↘',
  s: '↓',
  sw: '↙',
  w: '←',
  nw: '↖',
};

/** What a band means, in words, so it's never shown by color alone. */
export const BAND_WORDS: Record<PinBand, string> = {
  green: 'Solved',
  yellow: 'Close',
  orange: 'Same part of the world',
  red: 'Far',
};

const COMPASS_WORDS: Record<NonNullable<Compass>, string> = {
  n: 'north',
  ne: 'northeast',
  e: 'east',
  se: 'southeast',
  s: 'south',
  sw: 'southwest',
  w: 'west',
  nw: 'northwest',
};

/** A distance as shown, such as "2,400 km". The server has already rounded it. */
export function formatKm(km: number): string {
  return `${km.toLocaleString('en-US')} km`;
}

/** A mark as VoiceOver reads it, such as "Close, 310 kilometers, go east". */
export function describeMark(mark: PinMark): string {
  if (mark.band === 'green')
    return `Solved, ${mark.distanceKm.toLocaleString('en-US')} kilometers or less`;
  const way = mark.direction ? `, go ${COMPASS_WORDS[mark.direction]}` : '';
  return `${BAND_WORDS[mark.band]}, ${mark.distanceKm.toLocaleString('en-US')} kilometers${way}`;
}
