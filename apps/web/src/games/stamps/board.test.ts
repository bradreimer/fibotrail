import { beforeEach, describe, expect, it } from 'vitest';

import { formatTime } from '../time';
import {
  autoCrossed,
  conflicts,
  isSolved,
  loadAutoCross,
  loadGame,
  nextCell,
  parseRegions,
  regionEdges,
  ruledOut,
  saveAutoCross,
  saveGame,
  type Cell,
} from './board';

/** A 5 by 5 board whose stamps go in columns 0, 2, 4, 1, 3: cells 0, 7, 14, 16, 23. */
const ROWS = ['ABBBB', 'ABBCC', 'ADDCC', 'ADDEE', 'AAEEE'];
const regions = parseRegions(ROWS);

function board(stamps: number[], crosses: number[] = []): Cell[] {
  return Array.from({ length: 25 }, (_, cell) =>
    stamps.includes(cell) ? 'stamp' : crosses.includes(cell) ? 'cross' : 'empty',
  );
}

beforeEach(() => localStorage.clear());

describe('a Stamps board', () => {
  it('reads the region letters row by row', () => {
    expect(regions.slice(0, 10)).toEqual([0, 1, 1, 1, 1, 0, 1, 1, 2, 2]);
  });

  it('cycles a tapped cell through empty, ✕, and stamp', () => {
    expect(nextCell('empty')).toBe('cross');
    expect(nextCell('cross')).toBe('stamp');
    expect(nextCell('stamp')).toBe('empty');
  });

  it('rules out a stamp’s row, column, region, and neighbors', () => {
    const out = ruledOut(5, regions, 7);
    // Row 1, column 2, region B, and the cells around row 1 column 2.
    expect([...out].sort((a, b) => a - b)).toEqual([1, 2, 3, 4, 5, 6, 8, 9, 11, 12, 13, 17, 22]);
    expect(out.has(7)).toBe(false);
  });

  it('marks only empty cells as ruled out', () => {
    const auto = autoCrossed(5, regions, board([0], [1]));
    expect(auto.has(1)).toBe(false);
    expect(auto.has(5)).toBe(true);
    expect(auto.has(6)).toBe(true);
    expect(auto.has(12)).toBe(false);
  });

  it('finds stamps that break a rule', () => {
    expect(conflicts(5, regions, board([0, 6])).size).toBe(2);
    expect(conflicts(5, regions, board([0, 4])).size).toBe(2);
    expect(conflicts(5, regions, board([0, 7])).size).toBe(0);
  });

  it('is solved with one stamp in each row, column, and region, and none touching', () => {
    expect(isSolved(5, regions, board([0, 7, 14, 16, 23]))).toBe(true);
    expect(isSolved(5, regions, board([0, 7, 14, 16]))).toBe(false);
    expect(isSolved(5, regions, board([0, 7, 14, 16, 22]))).toBe(false);
  });

  it('draws thick borders between regions and at the edges', () => {
    expect(regionEdges(5, regions, 0)).toEqual({
      top: true,
      right: true,
      bottom: false,
      left: true,
    });
    expect(regionEdges(5, regions, 7)).toEqual({
      top: false,
      right: true,
      bottom: true,
      left: false,
    });
  });

  it('saves one board for today, and forgets a board from another day', () => {
    saveGame({ date: '2026-11-20', cells: board([0], [1]), seconds: 42, hints: 1 });

    expect(loadGame('2026-11-20', 5)).toEqual({
      date: '2026-11-20',
      cells: board([0], [1]),
      seconds: 42,
      hints: 1,
    });
    expect(loadGame('2026-11-21', 5)).toBeNull();
    expect(loadGame('2026-11-20', 5)).toBeNull();
  });

  it('keeps Auto-✕ on unless it was turned off', () => {
    expect(loadAutoCross()).toBe(true);
    saveAutoCross(false);
    expect(loadAutoCross()).toBe(false);
  });
});

describe('a time', () => {
  it('shows as minutes and seconds, with hours when needed', () => {
    expect(formatTime(5)).toBe('0:05');
    expect(formatTime(161)).toBe('2:41');
    expect(formatTime(3761)).toBe('1:02:41');
  });
});
