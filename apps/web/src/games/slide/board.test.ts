import { describe, expect, it } from 'vitest';

import { cellForArrow, isSolved, lineFrom, move, moveAll, SOLVED } from './board';
import { offsetAlong, snaps } from './SlideBoard';

describe('the Slide board', () => {
  it('slides a tile only into the empty cell next to it', () => {
    expect(move(SOLVED, 6)).toEqual([1, 2, 3, 4, 5, 0, 7, 8, 6]);
    expect(move(SOLVED, 5)).toEqual(SOLVED);
    // Cell 3 starts a row, so it isn't next to cell 2 at the end of the row before.
    expect(move([1, 2, 0, 3, 4, 5, 6, 7, 8], 3)).toEqual([1, 2, 0, 3, 4, 5, 6, 7, 8]);
  });

  it('slides a whole line toward the empty cell, nearest first', () => {
    expect(lineFrom(SOLVED, 6)).toEqual({ tiles: [8, 7], direction: { dx: 1, dy: 0 } });
    expect(lineFrom(SOLVED, 2)).toEqual({ tiles: [6, 3], direction: { dx: 0, dy: 1 } });
    expect(lineFrom(SOLVED, 4)).toBeNull();
    expect(lineFrom(SOLVED, 8)).toBeNull();
    expect(moveAll(SOLVED, [8, 7])).toEqual([1, 2, 3, 4, 5, 6, 0, 7, 8]);
  });

  it('maps an arrow to the tile that moves that way', () => {
    expect(cellForArrow(SOLVED, 'ArrowLeft')).toBeNull();
    expect(cellForArrow(SOLVED, 'ArrowRight')).toBe(7);
    expect(cellForArrow(SOLVED, 'ArrowDown')).toBe(5);
    expect(cellForArrow(SOLVED, 'Enter')).toBeNull();
  });

  it('knows a solved board', () => {
    expect(isSolved(SOLVED)).toBe(true);
    expect(isSolved(move(SOLVED, 8))).toBe(false);
  });
});

describe('dragging a tile', () => {
  const right = { dx: 1, dy: 0 };

  it('follows the finger along one axis, with a little give past each end', () => {
    expect(offsetAlong(right, 40, 25, 100)).toBe(40);
    expect(offsetAlong(right, -40, 0, 100)).toBe(-4);
    expect(offsetAlong(right, 180, 0, 100)).toBe(104);
    expect(offsetAlong({ dx: 0, dy: -1 }, 0, -30, 100)).toBe(30);
  });

  it('snaps past 40% of a cell or on a flick, and springs back otherwise', () => {
    expect(snaps(50, 0, 100)).toBe(true);
    expect(snaps(30, 0, 100)).toBe(false);
    expect(snaps(15, 0.5, 100)).toBe(true);
    expect(snaps(80, -0.5, 100)).toBe(false);
  });
});
