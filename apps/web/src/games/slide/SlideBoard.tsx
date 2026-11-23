import {
  useRef,
  useState,
  type CSSProperties,
  type KeyboardEvent as ReactKeyboardEvent,
  type PointerEvent as ReactPointerEvent,
} from 'react';

import { cellForArrow, cellOf, lineFrom, SIZE, type Direction } from './board';

/** A finger that moves less than this is a tap, not a drag. */
export const TAP_SLOP = 6;
/** A drag past this share of a cell snaps the tiles into place on release. */
export const SNAP_SHARE = 0.4;
/** A release faster than this, toward the empty cell, snaps too: a flick. In px per ms. */
export const FLICK_SPEED = 0.35;

interface Drag {
  pointerId: number;
  cell: number;
  tiles: number[];
  direction: Direction;
  startX: number;
  startY: number;
  cellSize: number;
  /** How far the tiles have moved toward the empty cell, from 0 to one cell. */
  offset: number;
  moved: boolean;
  /** The last two samples, for the speed at release. */
  samples: { t: number; offset: number }[];
}

/**
 * How far along the slide a pointer has moved, from 0 to one cell. A small give past each end
 * makes the edge feel soft rather than like a wall.
 */
export function offsetAlong(
  direction: Direction,
  dx: number,
  dy: number,
  cellSize: number,
): number {
  const along = dx * direction.dx + dy * direction.dy;
  const give = cellSize * 0.04;
  if (along < 0) return Math.max(-give, along * 0.25);
  if (along > cellSize) return Math.min(cellSize + give, cellSize + (along - cellSize) * 0.25);
  return along;
}

/** Whether a released drag snaps into place: far enough, or flicked toward the empty cell. */
export function snaps(offset: number, speed: number, cellSize: number): boolean {
  if (speed > FLICK_SPEED) return true;
  if (speed < -FLICK_SPEED) return false;
  return offset > cellSize * SNAP_SHARE;
}

/**
 * The 3 by 3 board. Each tile shows its part of the photo. You drag a tile, or a line of tiles,
 * toward the empty cell, and they follow the finger along that one axis; on release they snap into
 * place or spring back. A tap slides them at once. Arrow keys slide the tile next to the empty
 * cell. See docs/specs/slide-game.md#playing.
 */
export function SlideBoard({
  board,
  photoId,
  solved,
  onSlide,
}: {
  board: readonly number[];
  photoId: string;
  solved: boolean;
  /** The tiles that slid, nearest the empty cell first. */
  onSlide: (tiles: number[]) => void;
}) {
  const boardRef = useRef<HTMLDivElement>(null);
  const dragRef = useRef<Drag | null>(null);
  const [drag, setDrag] = useState<Pick<Drag, 'tiles' | 'direction' | 'offset'> | null>(null);

  function slide(cell: number) {
    if (solved) return;
    const line = lineFrom(board, cell);
    if (!line) return;
    navigator.vibrate?.(8);
    onSlide(line.tiles);
  }

  function onPointerDown(event: ReactPointerEvent<HTMLButtonElement>, cell: number) {
    if (solved || dragRef.current || (event.pointerType === 'mouse' && event.button !== 0)) return;
    const line = lineFrom(board, cell);
    if (!line) return;
    const size = (boardRef.current?.getBoundingClientRect().width ?? 0) / SIZE;
    event.currentTarget.setPointerCapture?.(event.pointerId);
    dragRef.current = {
      pointerId: event.pointerId,
      cell,
      tiles: line.tiles,
      direction: line.direction,
      startX: event.clientX,
      startY: event.clientY,
      cellSize: size,
      offset: 0,
      moved: false,
      samples: [{ t: event.timeStamp, offset: 0 }],
    };
    setDrag({ tiles: line.tiles, direction: line.direction, offset: 0 });
  }

  function onPointerMove(event: ReactPointerEvent<HTMLButtonElement>) {
    const current = dragRef.current;
    if (!current || current.pointerId !== event.pointerId) return;
    const dx = event.clientX - current.startX;
    const dy = event.clientY - current.startY;
    if (!current.moved && Math.hypot(dx, dy) < TAP_SLOP) return;
    current.moved = true;
    current.offset = offsetAlong(current.direction, dx, dy, current.cellSize);
    current.samples = [
      ...current.samples.slice(-1),
      { t: event.timeStamp, offset: current.offset },
    ];
    setDrag({ tiles: current.tiles, direction: current.direction, offset: current.offset });
  }

  function onPointerUp(event: ReactPointerEvent<HTMLButtonElement>) {
    const current = dragRef.current;
    if (!current || current.pointerId !== event.pointerId) return;
    dragRef.current = null;
    setDrag(null);
    if (!current.moved) {
      slide(current.cell);
      return;
    }
    const [before, last] = current.samples;
    const speed =
      before && last && last.t > before.t ? (last.offset - before.offset) / (last.t - before.t) : 0;
    if (snaps(current.offset, speed, current.cellSize)) slide(current.cell);
  }

  function onPointerCancel() {
    dragRef.current = null;
    setDrag(null);
  }

  function onKeyDown(event: ReactKeyboardEvent<HTMLButtonElement>) {
    const cell = cellForArrow(board, event.key);
    if (cell === null) return;
    event.preventDefault();
    slide(cell);
    // Keep focus on the tile that moved, so the next arrow follows on.
    const tile = board[cell];
    window.requestAnimationFrame(() =>
      boardRef.current?.querySelector<HTMLElement>(`[data-tile="${tile}"]`)?.focus(),
    );
  }

  return (
    <div
      ref={boardRef}
      className={`slide-board${solved ? ' slide-board--solved' : ''}`}
      role="group"
      aria-label="Puzzle board. Slide a tile into the empty space."
    >
      {Array.from({ length: SIZE * SIZE - 1 }, (_, index) => index + 1).map((tile) => {
        const at = cellOf(board.indexOf(tile));
        const home = cellOf(tile - 1);
        const dragging = drag?.tiles.includes(tile) ?? false;
        const offset = dragging ? drag!.offset : 0;
        const style = {
          '--slide-row': at.row,
          '--slide-col': at.col,
          '--slide-home-row': home.row,
          '--slide-home-col': home.col,
          '--slide-dx': `${offset * (drag?.direction.dx ?? 0)}px`,
          '--slide-dy': `${offset * (drag?.direction.dy ?? 0)}px`,
        } as CSSProperties;
        const movable = !solved && lineFrom(board, board.indexOf(tile)) !== null;
        return (
          <button
            key={tile}
            type="button"
            data-tile={tile}
            className={`slide-tile${dragging ? ' slide-tile--dragging' : ''}`}
            style={style}
            aria-label={`Tile ${tile}, row ${at.row + 1}, column ${at.col + 1}`}
            aria-disabled={!movable}
            tabIndex={solved ? -1 : 0}
            onPointerDown={(event) => onPointerDown(event, board.indexOf(tile))}
            onPointerMove={onPointerMove}
            onPointerUp={onPointerUp}
            onPointerCancel={onPointerCancel}
            onKeyDown={onKeyDown}
            onLostPointerCapture={onPointerCancel}
            onClick={(event) => {
              // The pointer handlers already took a mouse or touch press. A click with no
              // pointer behind it, detail 0, comes from Enter, Space, or an assistive tool.
              if (event.detail === 0) slide(board.indexOf(tile));
            }}
          >
            <span className="slide-tile__face" aria-hidden="true">
              <img
                className="slide-tile__photo"
                src={`/api/photos/${photoId}/preview`}
                alt=""
                draggable={false}
              />
            </span>
          </button>
        );
      })}
      {/* The missing tile: it fades in once the photo is whole. */}
      <span
        className="slide-tile slide-tile--last"
        aria-hidden="true"
        style={
          {
            '--slide-row': SIZE - 1,
            '--slide-col': SIZE - 1,
            '--slide-home-row': SIZE - 1,
            '--slide-home-col': SIZE - 1,
            '--slide-dx': '0px',
            '--slide-dy': '0px',
          } as CSSProperties
        }
      >
        <span className="slide-tile__face">
          <img
            className="slide-tile__photo"
            src={`/api/photos/${photoId}/preview`}
            alt=""
            draggable={false}
          />
        </span>
      </span>
    </div>
  );
}
