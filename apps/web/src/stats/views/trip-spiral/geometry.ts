/** The side of each Fibonacci square, and the radius of the quarter circle drawn in it. */
export const FIBONACCI = [1, 1, 2, 3, 5, 8, 13, 21, 34, 55] as const;

/** Where on the spiral the first and the last trip sit, as shares of its length. */
export const FIRST_SHARE = 0.07;
export const LAST_SHARE = 0.985;

export interface Point {
  x: number;
  y: number;
}

/** One quarter circle, drawn clockwise on the screen from `start` to `end` around `center`. */
export interface Arc {
  center: Point;
  radius: number;
  /** The angle of the start point around the center, in radians, with y pointing down. */
  startAngle: number;
  start: Point;
  end: Point;
}

export interface Spiral {
  arcs: Arc[];
  /** The four corners of each Fibonacci square, in order. */
  squares: Point[][];
  /** The length of the spiral. */
  length: number;
  bounds: { minX: number; minY: number; maxX: number; maxY: number };
}

const QUARTER = Math.PI / 2;

function onCircle(center: Point, radius: number, angle: number): Point {
  return { x: center.x + radius * Math.cos(angle), y: center.y + radius * Math.sin(angle) };
}

/**
 * Builds the spiral from its center outward: quarter circles of 1, 1, 2, 3, 5, ... units, each
 * turning clockwise on the screen and starting where the last ended, in the direction it ended.
 * `quarterTurns` rotates the whole spiral by that many quarter turns.
 */
export function buildSpiral(quarterTurns = 0): Spiral {
  const arcs: Arc[] = [];
  const squares: Point[][] = [];
  let position: Point = { x: 0, y: 0 };
  let heading = quarterTurns * QUARTER;
  for (const radius of FIBONACCI) {
    const center = {
      x: position.x - radius * Math.sin(heading),
      y: position.y + radius * Math.cos(heading),
    };
    const startAngle = heading - QUARTER;
    const start = onCircle(center, radius, startAngle);
    const end = onCircle(center, radius, startAngle + QUARTER);
    arcs.push({ center, radius, startAngle, start, end });
    squares.push([
      center,
      start,
      { x: start.x + end.x - center.x, y: start.y + end.y - center.y },
      end,
    ]);
    position = end;
    heading += QUARTER;
  }
  const length = arcs.reduce((sum, arc) => sum + arc.radius * QUARTER, 0);
  const points = squares.flat();
  const bounds = {
    minX: Math.min(...points.map((point) => point.x)),
    minY: Math.min(...points.map((point) => point.y)),
    maxX: Math.max(...points.map((point) => point.x)),
    maxY: Math.max(...points.map((point) => point.y)),
  };
  return { arcs, squares, length, bounds };
}

/** The spiral that stands tallest, which suits a phone held upright. */
export function tallestSpiral(): Spiral {
  const options = [0, 1, 2, 3].map((turns) => buildSpiral(turns));
  const ratio = (spiral: Spiral) =>
    (spiral.bounds.maxY - spiral.bounds.minY) / (spiral.bounds.maxX - spiral.bounds.minX);
  return options.reduce((best, option) => (ratio(option) > ratio(best) + 1e-9 ? option : best));
}

/** The point at `share` of the spiral's length, from 0 at its center to 1 at its end. */
export function pointAt(spiral: Spiral, share: number): Point {
  let remaining = Math.min(Math.max(share, 0), 1) * spiral.length;
  for (const arc of spiral.arcs) {
    const length = arc.radius * QUARTER;
    if (remaining <= length + 1e-9) {
      return onCircle(arc.center, arc.radius, arc.startAngle + remaining / arc.radius);
    }
    remaining -= length;
  }
  return spiral.arcs[spiral.arcs.length - 1]!.end;
}

/** The shares for `count` trips, oldest first, at equal distances from 7 to 98.5 percent. */
export function tripShares(count: number): number[] {
  if (count <= 0) return [];
  if (count === 1) return [FIRST_SHARE];
  return Array.from(
    { length: count },
    (_, index) => FIRST_SHARE + ((LAST_SHARE - FIRST_SHARE) * index) / (count - 1),
  );
}

/** The SVG path of the whole spiral. */
export function spiralPath(spiral: Spiral): string {
  const first = spiral.arcs[0]!;
  const rest = spiral.arcs
    .map(
      (arc) =>
        `A ${arc.radius} ${arc.radius} 0 0 1 ${arc.end.x.toFixed(3)} ${arc.end.y.toFixed(3)}`,
    )
    .join(' ');
  return `M ${first.start.x.toFixed(3)} ${first.start.y.toFixed(3)} ${rest}`;
}

/** The dot radius for a trip, in pixels: 3.5 plus 1.4 times the square root of its days. */
export function dotRadius(days: number): number {
  return 3.5 + 1.4 * Math.sqrt(days);
}

/** The years that get a label: 2012, 2016, 2020, 2024, and every fourth year after. */
export function isLabeledYear(year: number): boolean {
  return year >= 2012 && (year - 2012) % 4 === 0;
}
