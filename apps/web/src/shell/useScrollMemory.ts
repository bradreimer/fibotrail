import { useLayoutEffect, type RefObject } from 'react';
import { useLocation, useNavigationType } from 'react-router-dom';

/** Scroll positions by history entry. It lasts as long as the page does. */
const positions = new Map<string, number>();

/**
 * Keeps the scroll position of the content area for each history entry. Going back returns to
 * the position you left. A new screen starts at the top.
 */
export function useScrollMemory(area: RefObject<HTMLElement | null>): void {
  const { key } = useLocation();
  const type = useNavigationType();

  useLayoutEffect(() => {
    const element = area.current;
    if (!element) return;
    element.scrollTo({ top: type === 'POP' ? (positions.get(key) ?? 0) : 0 });
    const remember = () => positions.set(key, element.scrollTop);
    element.addEventListener('scroll', remember, { passive: true });
    return () => {
      remember();
      element.removeEventListener('scroll', remember);
    };
  }, [area, key, type]);
}
