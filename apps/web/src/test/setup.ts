import '@testing-library/jest-dom/vitest';

import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

afterEach(() => {
  cleanup();
  localStorage.clear();
});

// jsdom has no layout, so it has no scrollTo. Setting scrollTop is what a browser does.
Element.prototype.scrollTo = function scrollTo(this: Element, options?: ScrollToOptions | number) {
  this.scrollTop = typeof options === 'object' ? (options.top ?? 0) : 0;
};
