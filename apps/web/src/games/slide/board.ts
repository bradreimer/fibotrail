/**
 * The Slide board as plain data: 9 cells, row by row from the top left. A cell holds a tile number
 * from 1 to 8, where tile n belongs in cell n - 1, or 0 for the empty cell. The server replays the
 * same rules. See docs/specs/slide-game.md.
 */
export const SIZE = 3;

export const SOLVED: readonly number[] = [1, 2, 3, 4, 5, 6, 7, 8, 0];

export function isSolved(board: readonly number[]): boolean {
  return board.every((tile, index) => tile === SOLVED[index]);
}

/** Row and column of a cell. */
export function cellOf(index: number): { row: number; col: number } {
  return { row: Math.floor(index / SIZE), col: index % SIZE };
}

/** One step toward the empty cell: the direction a tile slides, as -1, 0, or 1 on each axis. */
export interface Direction {
  dx: number;
  dy: number;
}

/**
 * The tiles that slide when you take the tile in <paramref>cell</paramref>: it and every tile
 * between it and the empty cell, in the same row or column. They're listed nearest the empty cell
 * first, the order they move in. Null when the cell isn't in line with the empty cell.
 */
export function lineFrom(
  board: readonly number[],
  cell: number,
): { tiles: number[]; direction: Direction } | null {
  const empty = board.indexOf(0);
  if (cell === empty || cell < 0 || cell >= board.length) return null;
  const from = cellOf(cell);
  const to = cellOf(empty);
  if (from.row !== to.row && from.col !== to.col) return null;
  const direction = { dx: Math.sign(to.col - from.col), dy: Math.sign(to.row - from.row) };
  const tiles: number[] = [];
  for (
    let at = { ...to };
    at.row !== from.row || at.col !== from.col;
    at = { row: at.row - direction.dy, col: at.col - direction.dx }
  ) {
    const next = { row: at.row - direction.dy, col: at.col - direction.dx };
    tiles.push(board[next.row * SIZE + next.col]!);
  }
  return { tiles, direction };
}

/** The board after one tile slides into the empty cell, or the same board when it can't. */
export function move(board: readonly number[], tile: number): number[] {
  const from = board.indexOf(tile);
  const empty = board.indexOf(0);
  const a = cellOf(from);
  const b = cellOf(empty);
  if (tile === 0 || from < 0 || Math.abs(a.row - b.row) + Math.abs(a.col - b.col) !== 1) {
    return [...board];
  }
  const next = [...board];
  next[empty] = tile;
  next[from] = 0;
  return next;
}

/** The board after the tiles of a line slide, one after another. */
export function moveAll(board: readonly number[], tiles: readonly number[]): number[] {
  return tiles.reduce<number[]>((current, tile) => move(current, tile), [...board]);
}

/**
 * The cell whose tile an arrow key slides into the empty cell. The arrow is the way the tile
 * moves, as on a phone: Up slides the tile below the empty cell up. Null at an edge.
 */
export function cellForArrow(board: readonly number[], key: string): number | null {
  const { row, col } = cellOf(board.indexOf(0));
  const from: Record<string, { row: number; col: number }> = {
    ArrowUp: { row: row + 1, col },
    ArrowDown: { row: row - 1, col },
    ArrowLeft: { row, col: col + 1 },
    ArrowRight: { row, col: col - 1 },
  };
  const at = from[key];
  if (!at || at.row < 0 || at.row >= SIZE || at.col < 0 || at.col >= SIZE) return null;
  return at.row * SIZE + at.col;
}
