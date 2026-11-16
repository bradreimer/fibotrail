const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

export const CENTER = 100;
export const INNER = 38;
export const OUTER = 70;
export const ARC = 80;
const SLICE = Math.PI / 6;
const GAP = 0.03;

function point(radius: number, angle: number): string {
  return `${(CENTER + radius * Math.sin(angle)).toFixed(2)} ${(CENTER - radius * Math.cos(angle)).toFixed(2)}`;
}

/** The ring slice of a month, 1 to 12. January is at the top and the months run clockwise. */
export function slicePath(month: number): string {
  const from = (month - 1) * SLICE + GAP;
  const to = month * SLICE - GAP;
  return `M ${point(INNER, from)} L ${point(OUTER, from)} A ${OUTER} ${OUTER} 0 0 1 ${point(OUTER, to)} L ${point(INNER, to)} A ${INNER} ${INNER} 0 0 0 ${point(INNER, from)} Z`;
}

/** An arc outside the ring over the months of a season. `months` are in order, such as [12, 1, 2]. */
export function arcPath(months: readonly number[]): string {
  const first = months[0]!;
  const from = (first - 1) * SLICE + GAP;
  const to = from + months.length * SLICE - 2 * GAP;
  return `M ${point(ARC, from)} A ${ARC} ${ARC} 0 0 1 ${point(ARC, to)}`;
}

/** The middle of a month's slice, for its initial. */
export function labelPoint(month: number, radius = (INNER + OUTER) / 2): { x: number; y: number } {
  const angle = (month - 0.5) * SLICE;
  return { x: CENTER + radius * Math.sin(angle), y: CENTER - radius * Math.cos(angle) };
}

export function monthName(month: number): string {
  return MONTHS[month - 1]!;
}

/** A season's months in words, such as "Dec–Feb". */
export function monthRange(months: readonly number[]): string {
  return `${monthName(months[0]!)}–${monthName(months[months.length - 1]!)}`;
}

/** Shades a month by its days, from 0 (none) to 1 (the most). A month with days is never fully empty. */
export function shade(days: number, max: number): number {
  if (days <= 0 || max <= 0) return 0;
  return Math.max(0.25, days / max);
}
