/** The inner edge, in km: everything nearer than this sits on the home dot. */
export const HOME_RADIUS_KM = 50;
/** The outer edge, in km: about half the earth's circumference. */
export const OUTER_KM = 20000;
/** The rings that are drawn and labeled, in km. */
export const RING_KM = [100, 500, 1000, 5000, 10000] as const;

/** A distance as a share of the radius, from 0 at the home dot to 1 at the outer edge, on a log scale. */
export function radiusShare(km: number): number {
  const share =
    Math.log(Math.max(km, HOME_RADIUS_KM) / HOME_RADIUS_KM) / Math.log(OUTER_KM / HOME_RADIUS_KM);
  return Math.min(Math.max(share, 0), 1);
}

/** The position of a bearing on a circle of `radius`: 0 is straight up and bearings run clockwise. */
export function polar(bearing: number, radius: number): { x: number; y: number } {
  const angle = (bearing * Math.PI) / 180;
  return { x: radius * Math.sin(angle), y: -radius * Math.cos(angle) };
}

/** The dot radius for a trip: 3.5 plus 1.4 times the square root of its days, as in the trip spiral. */
export function dotRadius(days: number): number {
  return 3.5 + 1.4 * Math.sqrt(days);
}

const POINTS = [
  'north',
  'northeast',
  'east',
  'southeast',
  'south',
  'southwest',
  'west',
  'northwest',
] as const;

/** A bearing in words, on eight compass points. */
export function compassWord(bearing: number): string {
  const index = Math.round((((bearing % 360) + 360) % 360) / 45) % 8;
  return POINTS[index]!;
}

export interface Band {
  label: string;
  /** Inclusive lower bound in km. */
  from: number;
  /** Exclusive upper bound in km. */
  to: number;
}

export const BANDS: readonly Band[] = [
  { label: 'Under 500 km', from: 0, to: 500 },
  { label: '500 to 1,000 km', from: 500, to: 1000 },
  { label: '1,000 to 5,000 km', from: 1000, to: 5000 },
  { label: '5,000 to 10,000 km', from: 5000, to: 10000 },
  { label: 'Over 10,000 km', from: 10000, to: Number.POSITIVE_INFINITY },
];

export function inBand(km: number, band: Band): boolean {
  return km >= band.from && km < band.to;
}
