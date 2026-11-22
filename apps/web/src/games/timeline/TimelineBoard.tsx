import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react';

import type { components } from '../../api/schema';
import { dropCard, moveCard } from './order';

type Mark = components['schemas']['Mark'];

const WORD: Record<Mark, string> = {
  right: 'Right',
  close: 'Close',
  wrong: 'Wrong',
  earlier: '↑ Earlier',
  later: '↓ Later',
};

/** How long a finger rests on a card before it lifts, as on an iOS home screen. */
export const HOLD_MS = 250;
/** How far a finger may move during the hold and still lift the card. Farther, and it scrolls. */
const HOLD_SLOP = 8;
/** How near the edge of the scroll area a dragged card starts scrolling it. */
const EDGE = 72;
/** The fastest scroll while dragging, per frame. */
const MAX_SPEED = 14;

interface Drag {
  from: number;
  pointerId: number;
  startY: number;
  /** How far the card has moved: the pointer plus any scroll since the drag began. */
  dy: number;
  /** Where each place was when the drag started. */
  slots: { top: number; height: number }[];
}

interface Press {
  index: number;
  pointerId: number;
  startX: number;
  startY: number;
  element: HTMLElement;
  timer: number;
}

/** The place whose middle is nearest the middle of the dragged card. */
export function placeUnder(drag: Pick<Drag, 'from' | 'dy' | 'slots'>): number {
  const origin = drag.slots[drag.from]!;
  const middle = origin.top + origin.height / 2 + drag.dy;
  let best = drag.from;
  drag.slots.forEach((slot, index) => {
    const distance = Math.abs(slot.top + slot.height / 2 - middle);
    const bestSlot = drag.slots[best]!;
    if (distance < Math.abs(bestSlot.top + bestSlot.height / 2 - middle)) best = index;
  });
  return best;
}

/** The nearest ancestor that scrolls, or the page. */
function scrollParent(element: HTMLElement | null): HTMLElement {
  for (let node = element?.parentElement; node; node = node.parentElement) {
    const { overflowY } = getComputedStyle(node);
    if ((overflowY === 'auto' || overflowY === 'scroll') && node.scrollHeight > node.clientHeight)
      return node;
  }
  return (document.scrollingElement as HTMLElement | null) ?? document.documentElement;
}

/**
 * The cards while you play. You press and hold a card, then drag it, and the others shift to make
 * room; with a mouse the drag starts at once. A quick swipe scrolls the page as usual, and a card
 * dragged near the top or bottom edge scrolls it. A card shows only its photo and, after a check,
 * its mark: no trip name, no number. Hidden Move buttons, shown on focus, do the same for a
 * keyboard or VoiceOver. See docs/specs/timeline-game.md#playing.
 */
export function TimelineBoard({
  order,
  marks,
  disabled,
  onChange,
}: {
  order: readonly string[];
  /** The mark each card got at the last check, by photo ID. Right cards are locked. */
  marks: ReadonlyMap<string, Mark>;
  disabled: boolean;
  onChange: (order: string[]) => void;
}) {
  const list = useRef<HTMLOListElement>(null);
  const [drag, setDrag] = useState<Drag | null>(null);
  const live = useRef<{ drag: Drag | null; press: Press | null; pointerY: number; scroll: number }>(
    { drag: null, press: null, pointerY: 0, scroll: 0 },
  );
  const locked = new Set(order.filter((id) => marks.get(id) === 'right'));

  function update(next: Drag | null) {
    live.current.drag = next;
    setDrag(next);
  }

  // The latest end handler, for the page-wide listeners below.
  const endRef = useRef<(pointerId: number, keep: boolean) => void>(() => {});

  useEffect(() => {
    const element = list.current;
    const state = live.current;
    const forgetPress = () => {
      if (state.press) window.clearTimeout(state.press.timer);
      state.press = null;
    };
    // While a card is lifted, the page mustn't scroll under the finger. Only a listener that isn't
    // passive can stop that, and React's touch listeners are passive.
    const stop = (event: TouchEvent) => {
      if (state.drag) event.preventDefault();
    };
    // A swipe can scroll without telling the cards, so a scroll, or a lifted finger, ends a hold
    // that hasn't lifted its card yet.
    const finish = (event: PointerEvent) =>
      endRef.current(event.pointerId, event.type === 'pointerup');
    element?.addEventListener('touchmove', stop, { passive: false });
    window.addEventListener('scroll', forgetPress, true);
    window.addEventListener('touchend', forgetPress);
    window.addEventListener('touchcancel', forgetPress);
    // A drag whose pointer capture failed still ends where the pointer lifts.
    window.addEventListener('pointerup', finish);
    window.addEventListener('pointercancel', finish);
    return () => {
      forgetPress();
      element?.removeEventListener('touchmove', stop);
      window.removeEventListener('scroll', forgetPress, true);
      window.removeEventListener('touchend', forgetPress);
      window.removeEventListener('touchcancel', forgetPress);
      window.removeEventListener('pointerup', finish);
      window.removeEventListener('pointercancel', finish);
    };
  }, []);

  // Scrolls the page while a lifted card is near its top or bottom edge.
  useEffect(() => {
    if (!drag) return;
    const scroller = scrollParent(list.current);
    const page = scroller === document.scrollingElement || scroller === document.documentElement;
    const startScroll = scroller.scrollTop;
    let frame = 0;
    const tick = () => {
      const current = live.current.drag;
      if (!current) return;
      const top = page ? 0 : scroller.getBoundingClientRect().top;
      const bottom = page ? window.innerHeight : scroller.getBoundingClientRect().bottom;
      const y = live.current.pointerY;
      const speed =
        y < top + EDGE
          ? -MAX_SPEED * Math.min(1, (top + EDGE - y) / EDGE)
          : y > bottom - EDGE
            ? MAX_SPEED * Math.min(1, (y - (bottom - EDGE)) / EDGE)
            : 0;
      if (speed !== 0) {
        scroller.scrollTop += speed;
        const dy = y - current.startY + (scroller.scrollTop - startScroll);
        if (dy !== current.dy) update({ ...current, dy });
      }
      live.current.scroll = scroller.scrollTop - startScroll;
      frame = requestAnimationFrame(tick);
    };
    live.current.scroll = 0;
    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
    // The loop reads the live drag; it only restarts when a drag begins or ends.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [drag === null]);

  const target = drag ? placeUnder(drag) : null;
  const shown = drag && target !== null ? dropCard(order, locked, drag.from, target) : order;
  const dragged = drag ? order[drag.from] : undefined;

  function lift(index: number, pointerId: number, startY: number, element: HTMLElement) {
    try {
      element.setPointerCapture(pointerId);
    } catch {
      // The pointer already ended. The next pointer event clears the drag.
    }
    const items = Array.from(list.current?.children ?? []);
    live.current.pointerY = startY;
    update({
      from: index,
      pointerId,
      startY,
      dy: 0,
      slots: items.map((item) => {
        const rect = item.getBoundingClientRect();
        return { top: rect.top, height: rect.height };
      }),
    });
  }

  function cancelPress() {
    if (live.current.press) window.clearTimeout(live.current.press.timer);
    live.current.press = null;
  }

  function start(event: ReactPointerEvent<HTMLLIElement>, index: number) {
    if (disabled || live.current.drag || locked.has(order[index]!)) return;
    if (event.pointerType === 'mouse' && event.button !== 0) return;
    const { pointerId, clientX, clientY, currentTarget } = event;
    if (event.pointerType !== 'touch') {
      lift(index, pointerId, clientY, currentTarget);
      return;
    }
    cancelPress();
    live.current.press = {
      index,
      pointerId,
      startX: clientX,
      startY: clientY,
      element: currentTarget,
      timer: window.setTimeout(() => {
        const press = live.current.press;
        live.current.press = null;
        if (press) lift(press.index, press.pointerId, live.current.pointerY, press.element);
      }, HOLD_MS),
    };
    live.current.pointerY = clientY;
  }

  function follow(event: ReactPointerEvent<HTMLLIElement>) {
    const { press, drag: current } = live.current;
    if (press && event.pointerId === press.pointerId) {
      live.current.pointerY = event.clientY;
      // Moving before the hold ends is a scroll, not a drag.
      if (Math.hypot(event.clientX - press.startX, event.clientY - press.startY) > HOLD_SLOP)
        cancelPress();
      return;
    }
    if (!current || event.pointerId !== current.pointerId) return;
    event.preventDefault();
    live.current.pointerY = event.clientY;
    update({ ...current, dy: event.clientY - current.startY + live.current.scroll });
  }

  function end(pointerId: number, keep: boolean) {
    if (live.current.press?.pointerId === pointerId) cancelPress();
    const current = live.current.drag;
    if (!current || pointerId !== current.pointerId) return;
    const next = dropCard(order, locked, current.from, placeUnder(current));
    update(null);
    if (keep && next.some((id, index) => id !== order[index])) onChange(next);
  }
  useEffect(() => {
    endRef.current = end;
  });

  return (
    <ol className="timeline__cards" ref={list}>
      {shown.map((id, index) => {
        const mark = marks.get(id);
        const isLocked = locked.has(id);
        const isDragged = id === dragged;
        // The dragged card follows the pointer from where its new place is drawn.
        const offset =
          isDragged && drag ? drag.dy - (drag.slots[index]!.top - drag.slots[drag.from]!.top) : 0;
        return (
          <li
            key={id}
            className={[
              'timeline-card',
              mark ? `timeline-card--${mark}` : '',
              isLocked ? 'timeline-card--locked' : '',
              isDragged ? 'timeline-card--dragging' : '',
            ]
              .filter(Boolean)
              .join(' ')}
            style={isDragged ? { transform: `translateY(${offset}px)` } : undefined}
            onPointerDown={(event) => start(event, index)}
            onPointerMove={follow}
            onPointerUp={(event) => end(event.pointerId, true)}
            onPointerCancel={(event) => end(event.pointerId, false)}
            onContextMenu={(event) => event.preventDefault()}
          >
            <img
              className="timeline-card__photo"
              src={`/api/photos/${id}/thumbnail`}
              alt={`Card ${index + 1} of ${order.length}`}
              draggable={false}
            />
            {mark && <span className="timeline-card__mark">{WORD[mark]}</span>}
            <span
              className="timeline-card__moves"
              onPointerDown={(event) => event.stopPropagation()}
            >
              <button
                type="button"
                className="timeline-card__move"
                aria-label={`Move card ${index + 1} up`}
                disabled={disabled || isLocked || index === 0}
                onClick={() => onChange(moveCard(order, locked, index, -1))}
              >
                ▲
              </button>
              <button
                type="button"
                className="timeline-card__move"
                aria-label={`Move card ${index + 1} down`}
                disabled={disabled || isLocked || index === order.length - 1}
                onClick={() => onChange(moveCard(order, locked, index, 1))}
              >
                ▼
              </button>
            </span>
          </li>
        );
      })}
    </ol>
  );
}
