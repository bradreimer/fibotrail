import { describe, expect, it } from 'vitest';

import { dropCard, moveCard, summary } from './order';
import { placeUnder } from './TimelineBoard';

const order = ['a', 'b', 'c', 'd', 'e'];

describe('dropping a card', () => {
  it('moves it to the new place and shifts the others', () => {
    expect(dropCard(order, new Set(), 0, 3)).toEqual(['b', 'c', 'd', 'a', 'e']);
    expect(dropCard(order, new Set(), 4, 1)).toEqual(['a', 'e', 'b', 'c', 'd']);
    expect(dropCard(order, new Set(), 2, 2)).toEqual(order);
  });

  it('never moves a locked card, and flows the others around it', () => {
    expect(dropCard(order, new Set(['c']), 0, 4)).toEqual(['b', 'd', 'c', 'e', 'a']);
    expect(dropCard(order, new Set(['c']), 2, 0)).toEqual(order);
  });

  it('lands a drop on a locked place in the nearest free place', () => {
    expect(dropCard(order, new Set(['c']), 0, 2)).toEqual(['b', 'a', 'c', 'd', 'e']);
    expect(dropCard(order, new Set(['c']), 4, 2)).toEqual(['a', 'b', 'c', 'e', 'd']);
  });

  it('keeps a drop past either end on the board', () => {
    expect(dropCard(order, new Set(), 1, 9)).toEqual(['a', 'c', 'd', 'e', 'b']);
    expect(dropCard(order, new Set(), 1, -3)).toEqual(['b', 'a', 'c', 'd', 'e']);
  });
});

describe('moving a card by a button', () => {
  it('swaps with the next free card and jumps over locked ones', () => {
    expect(moveCard(['a', 'b', 'c'], new Set(), 1, -1)).toEqual(['b', 'a', 'c']);
    expect(moveCard(['a', 'b', 'c'], new Set(['b']), 0, 1)).toEqual(['c', 'b', 'a']);
    expect(moveCard(['a', 'b', 'c'], new Set(['a']), 0, 1)).toEqual(['a', 'b', 'c']);
    expect(moveCard(['a', 'b', 'c'], new Set(), 0, -1)).toEqual(['a', 'b', 'c']);
  });
});

describe('the place under a dragged card', () => {
  const slots = [0, 100, 200, 300, 400].map((top) => ({ top, height: 90 }));

  it('is the place whose middle is nearest', () => {
    expect(placeUnder({ from: 0, dy: 0, slots })).toBe(0);
    expect(placeUnder({ from: 0, dy: 160, slots })).toBe(2);
    expect(placeUnder({ from: 4, dy: -1000, slots })).toBe(0);
  });
});

describe('marks', () => {
  it('are summarized for screen readers', () => {
    expect(summary(['right', 'close', 'wrong', 'wrong'])).toBe('1 right, 1 close, 2 wrong');
  });
});
