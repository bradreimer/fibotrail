import type { components } from '../../api/schema';

export type Course = components['schemas']['FetchCourse'];
export type RunEnd = components['schemas']['FetchEnd'];

/** A run's grade, as the server sends it. */
export interface RunGrade {
  end: RunEnd;
  tick: number;
  gatesPassed: number;
  caught: number[];
}

/**
 * One run in flight. Each `step` is one tick of 1/60 s. Lengths are integers in hundredths of a
 * pixel, so this matches the server's `FetchFlight` exactly; both test suites hold the same cases.
 * The browser flies it to draw the game, and the server flies it again from the taps to grade it.
 * See docs/specs/fetch-game.md.
 */
export class Flight {
  tick = 0;
  /** The top of the sprite. */
  y: number;
  velocity = 0;
  /** How far the world has moved. */
  scroll = 0;
  gatesPassed = 0;
  end: RunEnd | null = null;
  readonly caught = new Set<number>();

  constructor(readonly course: Course) {
    this.y = course.physics.startY;
  }

  /** Moves one tick, with a flap or not. Returns the treats caught on this tick. */
  step(flap: boolean): number[] {
    if (this.end) return [];
    const { physics, gates, treats } = this.course;
    this.tick += 1;
    if (flap) this.velocity = physics.flap;
    this.velocity += physics.gravity;
    this.y += this.velocity;
    this.scroll += physics.speed;

    const left = physics.dogX + this.scroll + physics.body.left;
    const right = physics.dogX + this.scroll + physics.body.right;
    const top = this.y + physics.body.top;
    const bottom = this.y + physics.body.bottom;

    if (top < 0) {
      this.end = 'ceiling';
      return [];
    }
    if (bottom >= physics.ground) {
      this.end = 'ground';
      return [];
    }

    let passed = 0;
    for (const gate of gates) {
      const gateRight = gate.x + physics.gateWidth;
      if (right > gate.x && left < gateRight && (top < gate.gapTop || bottom > gate.gapBottom)) {
        this.end = 'gate';
        return [];
      }
      if (left > gateRight) passed += 1;
    }
    this.gatesPassed = passed;

    const fresh: number[] = [];
    const radius = physics.treatRadius;
    treats.forEach((treat, index) => {
      const dx = treat.x - Math.min(Math.max(treat.x, left), right);
      const dy = treat.y - Math.min(Math.max(treat.y, top), bottom);
      if (dx * dx + dy * dy <= radius * radius && !this.caught.has(index)) {
        this.caught.add(index);
        fresh.push(index);
      }
    });

    if (left >= this.course.finishX) this.end = 'finish';
    return fresh;
  }

  grade(): RunGrade {
    if (!this.end) throw new Error("The run hasn't ended.");
    return {
      end: this.end,
      tick: this.tick,
      gatesPassed: this.gatesPassed,
      caught: [...this.caught].sort((a, b) => a - b),
    };
  }
}

/**
 * Flies a whole run. The first flap is on tick 1, when the run starts; `taps` are the ticks of the
 * flaps after it.
 */
export function fly(course: Course, taps: readonly number[]): RunGrade {
  const flight = new Flight(course);
  const flaps = new Set(taps);
  while (!flight.end) flight.step(flight.tick === 0 || flaps.has(flight.tick + 1));
  return flight.grade();
}
