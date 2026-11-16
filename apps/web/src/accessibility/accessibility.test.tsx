/* eslint-disable jsx-a11y/alt-text -- these tests feed the checker markup that breaks the rule on purpose */
import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { accessibleName, findAccessibilityProblems } from './accessibility';

function problemsIn(markup: React.ReactElement) {
  const { container } = render(markup);
  return findAccessibilityProblems(container);
}

describe('findAccessibilityProblems', () => {
  it('passes named controls and photos with alt text', () => {
    expect(
      problemsIn(
        <div>
          <img src="/a.jpg" alt="Orlando on March 15, 2027" />
          <button type="button">Add trip</button>
          <button type="button" aria-label="Close" />
          <a href="/trips">Trips</a>
          <label htmlFor="name">Name</label>
          <input id="name" />
          <button type="button" aria-label="Use as cover: Photo taken 2027-03-15">
            <img src="/b.jpg" alt="" />
          </button>
        </div>,
      ),
    ).toEqual([]);
  });

  it('reports an image with no alt attribute', () => {
    expect(problemsIn(<img src="/a.jpg" />)).toEqual(['The image /a.jpg has no alt attribute.']);
  });

  it('reports a photo with empty alt text and no named control around it', () => {
    expect(problemsIn(<img src="/a.jpg" alt="" />)).toHaveLength(1);
    expect(
      problemsIn(
        <button type="button">
          <img src="/a.jpg" alt="" />
        </button>,
      ),
    ).toHaveLength(2);
  });

  it('reports a button, a link, and an input with no name', () => {
    const problems = problemsIn(
      <div>
        <button type="button" />
        <a href="/trips">
          <svg />
        </a>
        <input />
      </div>,
    );
    expect(problems.map((problem) => problem.split(':')[0])).toEqual([
      'A button has no accessible name',
      'A a has no accessible name',
      'A input has no accessible name',
    ]);
  });

  it('skips what a screen reader skips', () => {
    expect(
      problemsIn(
        <div aria-hidden="true">
          <button type="button" />
          <img src="/a.jpg" />
        </div>,
      ),
    ).toEqual([]);
  });
});

describe('accessibleName', () => {
  it('prefers aria-label, then a labelling element, then content', () => {
    const { container } = render(
      <div>
        <span id="heading">Photo 3 of 6</span>
        <button type="button" aria-label="Close">
          X
        </button>
        <button type="button" aria-labelledby="heading">
          X
        </button>
        <button type="button">
          Next <span aria-hidden="true">›</span>
        </button>
      </div>,
    );
    const names = Array.from(container.querySelectorAll('button')).map(accessibleName);
    expect(names).toEqual(['Close', 'Photo 3 of 6', 'Next']);
  });
});
