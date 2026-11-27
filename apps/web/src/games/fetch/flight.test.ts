import { describe, expect, it } from 'vitest';

import { fly, Flight, type Course, type RunEnd } from './flight';

const UNIT = 100;

/**
 * The course both test suites use, so the TypeScript and C# simulations are held to the same
 * answers. The same course is in apps/server/tests/Fibotrail.Games.Tests/Fetch/FetchTests.cs.
 */
function sharedCourse(): Course {
  const centers = [300, 250, 350, 400, 300, 200, 280, 330];
  const heights = [260, 230, 300, 380, 320, 230, 240, 300];
  return {
    physics: {
      gravity: 50,
      flap: -750,
      speed: 200,
      worldWidth: 400 * UNIT,
      worldHeight: 700 * UNIT,
      ground: 620 * UNIT,
      dogX: 100 * UNIT,
      dogSize: 64 * UNIT,
      body: { left: 6 * UNIT, top: 20 * UNIT, right: 58 * UNIT, bottom: 44 * UNIT },
      startY: 318 * UNIT,
      gateWidth: 70 * UNIT,
      gapHeight: 250 * UNIT,
      treatRadius: 24 * UNIT,
    },
    gates: centers.map((center, index) => ({
      x: (440 + index * 240) * UNIT,
      gapTop: (center - 125) * UNIT,
      gapBottom: (center + 125) * UNIT,
      day: index + 1,
    })),
    treats: heights.map((y, index) => ({
      x: (440 + index * 240 + 155) * UNIT,
      y: y * UNIT,
      photoId: `p${index}`,
    })),
    finishX: (440 + 8 * 240) * UNIT,
  };
}

const range = (from: number, count: number) => Array.from({ length: count }, (_, i) => from + i);

/** The shared cases. A change to the physics changes these and the C# ones together. */
const CASES: [string, number[], RunEnd, number, number, number[]][] = [
  ['no taps falls to the ground', [], 'ground', 50, 0, []],
  ['a flap every tick hits the ceiling', range(2, 100), 'ceiling', 49, 0, []],
  ['flying too low hits the first gate', [16, 31, 46, 70, 99, 128], 'gate', 142, 0, []],
  [
    'the autopilot reaches the finish',
    [
      19, 48, 77, 106, 135, 164, 193, 211, 240, 269, 298, 338, 366, 395, 424, 459, 488, 517, 546,
      564, 579, 608, 637, 666, 684, 699, 728, 757, 786, 824, 852, 881, 910, 945, 974, 1003, 1032,
      1061, 1090, 1119,
    ],
    'finish',
    1127,
    8,
    [0, 5, 6, 7],
  ],
];

describe('the Fetch flight', () => {
  it.each(CASES)('matches the server: %s', (_, taps, end, tick, gatesPassed, caught) => {
    expect(fly(sharedCourse(), taps)).toEqual({ end, tick, gatesPassed, caught });
  });

  it('reports each treat once, on the tick it is caught', () => {
    const flight = new Flight(sharedCourse());
    const taps = new Set(CASES[3]![1]);
    const seen: number[] = [];
    flight.step(true);
    while (!flight.end) seen.push(...flight.step(taps.has(flight.tick + 1)));
    expect(seen).toEqual([0, 5, 6, 7]);
    expect(flight.step(true)).toEqual([]);
  });

  it("won't grade a run that hasn't ended", () => {
    expect(() => new Flight(sharedCourse()).grade()).toThrow();
  });
});
