import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * A stopwatch that starts when you call start, stops when you call stop, and pauses while the page
 * is hidden, so time in another app doesn't count. It shows whole seconds and starts from
 * `initial`, for a board picked up later the same day. See docs/specs/stamps-game.md#playing.
 */
export function useStopwatch(initial: number) {
  const [seconds, setSeconds] = useState(initial);
  const [running, setRunning] = useState(false);
  // Milliseconds counted before the current run, and when the current run began.
  const banked = useRef(initial * 1000);
  const since = useRef<number | null>(null);

  const read = useCallback(
    () => banked.current + (since.current === null ? 0 : Date.now() - since.current),
    [],
  );

  const pause = useCallback(() => {
    if (since.current === null) return;
    banked.current += Date.now() - since.current;
    since.current = null;
  }, []);

  useEffect(() => {
    if (!running) return;
    if (document.visibilityState !== 'hidden') since.current = Date.now();
    const tick = window.setInterval(() => setSeconds(Math.floor(read() / 1000)), 250);
    function onVisibility() {
      if (document.visibilityState === 'hidden') pause();
      else if (since.current === null) since.current = Date.now();
      setSeconds(Math.floor(read() / 1000));
    }
    document.addEventListener('visibilitychange', onVisibility);
    return () => {
      window.clearInterval(tick);
      document.removeEventListener('visibilitychange', onVisibility);
      pause();
    };
  }, [running, pause, read]);

  const start = useCallback(() => setRunning(true), []);
  /** Stops the clock and answers the whole seconds counted. */
  const stop = useCallback(() => {
    pause();
    setRunning(false);
    const total = Math.floor(read() / 1000);
    setSeconds(total);
    return total;
  }, [pause, read]);

  return { seconds, running, start, stop };
}
