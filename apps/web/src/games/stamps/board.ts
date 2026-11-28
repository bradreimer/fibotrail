/**
 * The rules of a Stamps board in the browser: what a stamp rules out, which stamps break a rule,
 * and when the board is solved. Cells are numbered row by row from the top left. The server has the
 * same rules and grades the result. See docs/specs/stamps-game.md#playing.
 */

/** What you've put on a cell. */
export type Cell = 'empty' | 'cross' | 'stamp';

/** The region of each cell, from the rows of letters the server sends, such as "AABBBCC". */
export function parseRegions(rows: readonly string[]): number[] {
  return rows.flatMap((row) => [...row].map((letter) => letter.charCodeAt(0) - 65));
}

/** The letter of a region: A for 0. */
export function letterOf(region: number): string {
  return String.fromCharCode(65 + region);
}

/** True when two different cells share a side or a corner. */
export function touch(size: number, a: number, b: number): boolean {
  return (
    a !== b &&
    Math.abs(Math.floor(a / size) - Math.floor(b / size)) <= 1 &&
    Math.abs((a % size) - (b % size)) <= 1
  );
}

/** The cells a stamp rules out: the rest of its row, column, and region, and the 8 around it. */
export function ruledOut(size: number, regions: readonly number[], stamp: number): Set<number> {
  const out = new Set<number>();
  for (let cell = 0; cell < size * size; cell++) {
    if (cell === stamp) continue;
    if (
      Math.floor(cell / size) === Math.floor(stamp / size) ||
      cell % size === stamp % size ||
      regions[cell] === regions[stamp] ||
      touch(size, cell, stamp)
    ) {
      out.add(cell);
    }
  }
  return out;
}

/** The cells of the stamps on a board. */
export function stampsOf(cells: readonly Cell[]): number[] {
  return cells.flatMap((cell, index) => (cell === 'stamp' ? [index] : []));
}

/** The cells of your own ✕ marks. */
export function crossesOf(cells: readonly Cell[]): number[] {
  return cells.flatMap((cell, index) => (cell === 'cross' ? [index] : []));
}

/** Empty cells that a stamp rules out, for the faint ✕ of Auto-✕. */
export function autoCrossed(
  size: number,
  regions: readonly number[],
  cells: readonly Cell[],
): Set<number> {
  const out = new Set<number>();
  for (const stamp of stampsOf(cells)) {
    for (const cell of ruledOut(size, regions, stamp)) if (cells[cell] === 'empty') out.add(cell);
  }
  return out;
}

/** Stamps that share a row, column, or region with another stamp, or touch one. */
export function conflicts(
  size: number,
  regions: readonly number[],
  cells: readonly Cell[],
): Set<number> {
  const stamps = stampsOf(cells);
  const out = new Set<number>();
  for (const a of stamps) {
    for (const b of stamps) {
      if (a !== b && ruledOut(size, regions, a).has(b)) {
        out.add(a);
        out.add(b);
      }
    }
  }
  return out;
}

/** True when every row, column, and region has one stamp and no stamp breaks a rule. */
export function isSolved(
  size: number,
  regions: readonly number[],
  cells: readonly Cell[],
): boolean {
  return stampsOf(cells).length === size && conflicts(size, regions, cells).size === 0;
}

/** The tap cycle: empty, then ✕, then stamp, then empty again. */
export function nextCell(cell: Cell): Cell {
  if (cell === 'empty') return 'cross';
  if (cell === 'cross') return 'stamp';
  return 'empty';
}

/** Which sides of a cell border another region or the edge, for the 3 px region borders. */
export function regionEdges(
  size: number,
  regions: readonly number[],
  cell: number,
): { top: boolean; right: boolean; bottom: boolean; left: boolean } {
  const row = Math.floor(cell / size);
  const column = cell % size;
  const differs = (other: number) => regions[other] !== regions[cell];
  return {
    top: row === 0 || differs(cell - size),
    right: column === size - 1 || differs(cell + 1),
    bottom: row === size - 1 || differs(cell + size),
    left: column === 0 || differs(cell - 1),
  };
}

/** A board in progress, as saved on this device so it can be picked up the same day. */
export interface SavedGame {
  date: string;
  cells: Cell[];
  seconds: number;
  hints: number;
}

const GAME_KEY = 'fibotrail.stamps.game';
const AUTO_CROSS_KEY = 'fibotrail.stamps.autoCross';
const LETTERS: Record<Cell, string> = { empty: '.', cross: 'x', stamp: 's' };

/**
 * The board saved for this date and size, or null. One board is kept, so a board from another day
 * is replaced. Storage can be missing, as in a private window, so every read and write is guarded.
 */
export function loadGame(date: string, size: number): SavedGame | null {
  try {
    const text = localStorage.getItem(GAME_KEY);
    if (!text) return null;
    const saved = JSON.parse(text) as {
      date?: unknown;
      cells?: unknown;
      seconds?: unknown;
      hints?: unknown;
    };
    if (
      saved.date !== date ||
      typeof saved.cells !== 'string' ||
      saved.cells.length !== size * size
    ) {
      localStorage.removeItem(GAME_KEY);
      return null;
    }
    const cells = [...saved.cells].map((letter): Cell =>
      letter === 'x' ? 'cross' : letter === 's' ? 'stamp' : 'empty',
    );
    return {
      date,
      cells,
      seconds: typeof saved.seconds === 'number' ? saved.seconds : 0,
      hints: typeof saved.hints === 'number' ? saved.hints : 0,
    };
  } catch {
    return null;
  }
}

export function saveGame(game: SavedGame): void {
  try {
    localStorage.setItem(
      GAME_KEY,
      JSON.stringify({ ...game, cells: game.cells.map((cell) => LETTERS[cell]).join('') }),
    );
  } catch {
    // Without storage, the board starts over next time.
  }
}

export function clearSavedGame(): void {
  try {
    localStorage.removeItem(GAME_KEY);
  } catch {
    // Nothing to clear.
  }
}

/** Whether Auto-✕ is on. It's on unless you turned it off on this device. */
export function loadAutoCross(): boolean {
  try {
    return localStorage.getItem(AUTO_CROSS_KEY) !== 'off';
  } catch {
    return true;
  }
}

export function saveAutoCross(on: boolean): void {
  try {
    localStorage.setItem(AUTO_CROSS_KEY, on ? 'on' : 'off');
  } catch {
    // The switch still works for this visit.
  }
}
