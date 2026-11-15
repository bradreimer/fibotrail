import '@testing-library/jest-dom/vitest';

import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

// jsdom has no WebGL. Every test gets a stand-in for the map.
vi.mock('../map/MapView', () => import('./MapViewMock'));

afterEach(() => {
  cleanup();
  localStorage.clear();
});

// jsdom has no layout, so it has no scrollTo. Setting scrollTop is what a browser does.
Element.prototype.scrollTo = function scrollTo(this: Element, options?: ScrollToOptions | number) {
  this.scrollTop = typeof options === 'object' ? (options.top ?? 0) : 0;
};
