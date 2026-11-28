import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useStopwatch } from './stopwatch';

let visibility: DocumentVisibilityState = 'visible';

function setVisibility(state: DocumentVisibilityState) {
  visibility = state;
  document.dispatchEvent(new Event('visibilitychange'));
}

beforeEach(() => {
  vi.useFakeTimers();
  visibility = 'visible';
  Object.defineProperty(document, 'visibilityState', {
    configurable: true,
    get: () => visibility,
  });
});

afterEach(() => vi.useRealTimers());

describe('the Stamps stopwatch', () => {
  it('counts only while started and visible, from where it left off', () => {
    const { result } = renderHook(() => useStopwatch(10));
    act(() => void vi.advanceTimersByTime(5000));
    expect(result.current.seconds).toBe(10);

    act(() => result.current.start());
    act(() => void vi.advanceTimersByTime(3000));
    expect(result.current.seconds).toBe(13);

    act(() => setVisibility('hidden'));
    act(() => void vi.advanceTimersByTime(60_000));
    expect(result.current.seconds).toBe(13);

    act(() => setVisibility('visible'));
    act(() => void vi.advanceTimersByTime(2000));
    expect(result.current.seconds).toBe(15);

    let total = 0;
    act(() => {
      total = result.current.stop();
    });
    act(() => void vi.advanceTimersByTime(5000));
    expect(total).toBe(15);
    expect(result.current.seconds).toBe(15);
  });
});
