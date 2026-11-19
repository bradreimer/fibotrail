import type { components } from '../../api/schema';

type Mark = components['schemas']['Mark'];

/** The places that hold a card you can still move. Locked (right) cards keep their place. */
function freePlaces(order: readonly string[], locked: ReadonlySet<string>): number[] {
  return order.flatMap((id, index) => (locked.has(id) ? [] : [index]));
}

/**
 * Drops the card at `from` into place `to`, as a drag does. The card moves there and the other
 * free cards shift by one to make room; locked cards never move, and a drop on one lands in the
 * nearest free place. Returns a new order, unchanged when the card is locked.
 */
export function dropCard(
  order: readonly string[],
  locked: ReadonlySet<string>,
  from: number,
  to: number,
): string[] {
  const card = order[from];
  if (card === undefined || locked.has(card)) return [...order];
  const free = freePlaces(order, locked);
  const clamped = Math.min(Math.max(to, 0), order.length - 1);
  // The nearest free place to the drop; on a tie, the one on the side the card came from.
  const target = free.reduce((best, place) => {
    const distance = Math.abs(place - clamped) - Math.abs(best - clamped);
    if (distance < 0) return place;
    if (distance === 0 && Math.abs(place - from) < Math.abs(best - from)) return place;
    return best;
  });
  const cards = free.map((place) => order[place]!);
  const [moved] = cards.splice(free.indexOf(from), 1);
  cards.splice(free.indexOf(target), 0, moved!);
  const next = [...order];
  free.forEach((place, index) => {
    next[place] = cards[index]!;
  });
  return next;
}

/** Moves the card at `from` one free place up (-1) or down (1), for the Move buttons. */
export function moveCard(
  order: readonly string[],
  locked: ReadonlySet<string>,
  from: number,
  step: -1 | 1,
): string[] {
  const free = freePlaces(order, locked);
  const target = free[free.indexOf(from) + step];
  return target === undefined ? [...order] : dropCard(order, locked, from, target);
}

/** "3 right, 1 close, 1 wrong", for screen readers after a check. */
export function summary(marks: readonly Mark[]): string {
  const count = (mark: Mark) => marks.filter((item) => item === mark).length;
  return `${count('right')} right, ${count('close')} close, ${count('wrong')} wrong`;
}
