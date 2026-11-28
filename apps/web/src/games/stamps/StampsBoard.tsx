import {
  useEffect,
  useRef,
  useState,
  type CSSProperties,
  type KeyboardEvent,
  type MouseEvent,
  type PointerEvent,
} from 'react';

import { letterOf, regionEdges, type Cell } from './board';

/** A press that moves farther than this, in px, is a drag, not a tap. */
export const DRAG_PX = 6;

/** A press held this long without moving shows the cell's photo. */
export const PEEK_MS = 500;

/** A click this soon after a pointer tap is the same tap, so it's ignored. */
const SAME_TAP_MS = 700;

const STATE_NAMES: Record<Cell, string> = { empty: 'empty', cross: 'crossed out', stamp: 'stamp' };

interface Gesture {
  pointerId: number;
  x: number;
  y: number;
  cell: number;
  mode: 'mark' | 'clear' | null;
  peeked: boolean;
}

/**
 * The Stamps board: N by N cells, each a button. A tap moves a cell through empty, ✕, and stamp.
 * A drag marks ✕ on the empty cells it passes, or clears ✕ when it starts on one. A press held
 * still shows the cell's photo. The arrow keys move focus. See docs/specs/stamps-game.md#playing.
 */
export function StampsBoard({
  size,
  regions,
  photos,
  cells,
  autoCrossed,
  conflicts,
  hint,
  onTap,
  onDrag,
  onDragStart,
}: {
  size: number;
  regions: readonly number[];
  photos: readonly string[];
  cells: readonly Cell[];
  autoCrossed: ReadonlySet<number>;
  conflicts: ReadonlySet<number>;
  hint: number | null;
  onTap: (cell: number) => void;
  /** A drag reached a cell: mark it ✕, or clear its ✕. */
  onDrag: (cell: number, mode: 'mark' | 'clear') => void;
  /** A drag began, so the board can save one undo step for the whole drag. */
  onDragStart: () => void;
}) {
  const board = useRef<HTMLDivElement>(null);
  const buttons = useRef<(HTMLButtonElement | null)[]>([]);
  const gesture = useRef<Gesture | null>(null);
  const peekTimer = useRef<number | undefined>(undefined);
  const lastPointerTap = useRef(0);
  const [focus, setFocus] = useState(0);
  const [peek, setPeek] = useState<string | null>(null);

  useEffect(() => () => window.clearTimeout(peekTimer.current), []);

  function cellAt(x: number, y: number): number | null {
    const box = board.current?.getBoundingClientRect();
    if (!box || box.width === 0) return null;
    const column = Math.floor(((x - box.left) / box.width) * size);
    const row = Math.floor(((y - box.top) / box.height) * size);
    if (row < 0 || row >= size || column < 0 || column >= size) return null;
    return row * size + column;
  }

  function onPointerDown(event: PointerEvent<HTMLDivElement>) {
    const cell = cellAt(event.clientX, event.clientY);
    if (cell === null) return;
    board.current?.setPointerCapture?.(event.pointerId);
    gesture.current = {
      pointerId: event.pointerId,
      x: event.clientX,
      y: event.clientY,
      cell,
      mode: null,
      peeked: false,
    };
    window.clearTimeout(peekTimer.current);
    peekTimer.current = window.setTimeout(() => {
      if (!gesture.current || gesture.current.mode) return;
      gesture.current.peeked = true;
      setPeek(photos[cell] ?? null);
    }, PEEK_MS);
  }

  function onPointerMove(event: PointerEvent<HTMLDivElement>) {
    const now = gesture.current;
    if (!now || now.pointerId !== event.pointerId || now.peeked) return;
    if (!now.mode) {
      if (Math.hypot(event.clientX - now.x, event.clientY - now.y) <= DRAG_PX) return;
      window.clearTimeout(peekTimer.current);
      now.mode = cells[now.cell] === 'cross' ? 'clear' : 'mark';
      onDragStart();
      onDrag(now.cell, now.mode);
    }
    const cell = cellAt(event.clientX, event.clientY);
    if (cell !== null) onDrag(cell, now.mode);
  }

  function onPointerUp(event: PointerEvent<HTMLDivElement>) {
    const now = gesture.current;
    if (!now || now.pointerId !== event.pointerId) return;
    window.clearTimeout(peekTimer.current);
    gesture.current = null;
    lastPointerTap.current = event.timeStamp;
    if (now.peeked) {
      setPeek(null);
      return;
    }
    if (!now.mode) {
      setFocus(now.cell);
      onTap(now.cell);
    }
  }

  function onPointerCancel() {
    window.clearTimeout(peekTimer.current);
    gesture.current = null;
    setPeek(null);
  }

  function onClick(event: MouseEvent<HTMLButtonElement>, cell: number) {
    // A pointer tap was handled on release. Keyboard and VoiceOver activation arrive as a click.
    if (lastPointerTap.current > 0 && event.timeStamp - lastPointerTap.current < SAME_TAP_MS)
      return;
    setFocus(cell);
    onTap(cell);
  }

  function onKeyDown(event: KeyboardEvent<HTMLButtonElement>, cell: number) {
    const row = Math.floor(cell / size);
    const column = cell % size;
    const moves: Record<string, [number, number]> = {
      ArrowUp: [-1, 0],
      ArrowDown: [1, 0],
      ArrowLeft: [0, -1],
      ArrowRight: [0, 1],
    };
    const move = moves[event.key];
    if (!move) return;
    event.preventDefault();
    const next =
      Math.min(size - 1, Math.max(0, row + move[0])) * size +
      Math.min(size - 1, Math.max(0, column + move[1]));
    setFocus(next);
    buttons.current[next]?.focus();
  }

  return (
    <div className="stamps-board-wrap">
      <div
        ref={board}
        className="stamps-board"
        role="group"
        aria-label={`Board, ${size} by ${size}`}
        style={{ '--stamps-size': size } as CSSProperties}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerCancel={onPointerCancel}
        onContextMenu={(event) => event.preventDefault()}
      >
        {cells.map((cell, index) => {
          const region = regions[index] ?? 0;
          const edges = regionEdges(size, regions, index);
          const auto = cell === 'empty' && autoCrossed.has(index);
          const conflict = cell === 'stamp' && conflicts.has(index);
          const name = [
            `Row ${Math.floor(index / size) + 1}, column ${(index % size) + 1}, region ${letterOf(region)}`,
            auto ? 'ruled out' : STATE_NAMES[cell],
            conflict ? 'breaks a rule' : null,
            hint === index ? 'hint' : null,
          ]
            .filter(Boolean)
            .join(', ');
          const classes = [
            'stamps-cell',
            cell === 'stamp' && 'stamps-cell--stamp',
            conflict && 'stamps-cell--conflict',
            hint === index && 'stamps-cell--hint',
          ]
            .filter(Boolean)
            .join(' ');
          // Each cell draws its top and left lines in whole pixels, and the board draws its outer
          // edge, so a line is never split into half pixels that a browser rounds away.
          const line = (edge: boolean, first: boolean) =>
            first ? '0' : edge ? 'var(--stamps-thick)' : 'var(--stamps-thin)';
          const style = {
            '--stamps-region': `var(--stamps-region-${region % 8})`,
            borderTopWidth: line(edges.top, index < size),
            borderLeftWidth: line(edges.left, index % size === 0),
          } as CSSProperties;
          return (
            <button
              key={index}
              ref={(element) => {
                buttons.current[index] = element;
              }}
              type="button"
              className={classes}
              style={style}
              aria-label={name}
              tabIndex={index === focus ? 0 : -1}
              onClick={(event) => onClick(event, index)}
              onKeyDown={(event) => onKeyDown(event, index)}
            >
              <img
                className="stamps-cell__photo"
                src={`/api/photos/${photos[index]}/thumbnail`}
                alt=""
                draggable={false}
                loading="lazy"
              />
              {cell === 'cross' && (
                <span className="stamps-cell__cross" aria-hidden="true">
                  ✕
                </span>
              )}
              {auto && (
                <span className="stamps-cell__cross stamps-cell__cross--auto" aria-hidden="true">
                  ✕
                </span>
              )}
              {cell === 'stamp' && (
                <span className="stamps-cell__stamp" aria-hidden="true">
                  {letterOf(region)}
                </span>
              )}
            </button>
          );
        })}
      </div>
      {peek && (
        <div className="stamps-peek" aria-hidden="true">
          <img className="stamps-peek__photo" src={`/api/photos/${peek}/preview`} alt="" />
        </div>
      )}
    </div>
  );
}
