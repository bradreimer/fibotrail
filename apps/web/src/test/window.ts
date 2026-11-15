import { act } from '@testing-library/react';

type Listener = () => void;

const listeners = new Set<Listener>();
let width = 1024;

/** Stands in for window.matchMedia, which jsdom doesn't have. It answers min-width queries. */
export function installMatchMedia(initialWidth: number): void {
  width = initialWidth;
  listeners.clear();
  window.matchMedia = ((query: string) => {
    const min = Number(/min-width:\s*(\d+)px/.exec(query)?.[1] ?? 0);
    return {
      media: query,
      get matches() {
        return width >= min;
      },
      addEventListener: (_: string, listener: Listener) => listeners.add(listener),
      removeEventListener: (_: string, listener: Listener) => listeners.delete(listener),
    };
  }) as unknown as typeof window.matchMedia;
}

/** Changes the window width, as a rotation or a resize does. */
export function resizeTo(newWidth: number): void {
  act(() => {
    width = newWidth;
    listeners.forEach((listener) => listener());
  });
}
