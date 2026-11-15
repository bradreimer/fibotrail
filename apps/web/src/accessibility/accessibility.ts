/**
 * Accessibility checks that run on rendered screens. Intention: the rules of the design document's
 * Accessibility section that a test can check. Method: walk the DOM that a screen rendered and
 * report each image without alt text and each control without an accessible name. jsdom has no
 * layout, so contrast and text size are checked elsewhere (contrast.test.ts and the manual
 * checklist in docs/knowledge/accessibility-checklist.md).
 */

const CONTROLS = 'button, a[href], input:not([type="hidden"]), select, textarea, [role="button"]';

function isHidden(element: Element): boolean {
  return element.closest('[aria-hidden="true"], [hidden]') !== null;
}

function textOf(element: Element): string {
  return (element.textContent ?? '').replace(/\s+/g, ' ').trim();
}

/** The name a screen reader announces for a control, or an empty string when it has none. */
export function accessibleName(element: Element): string {
  const label = element.getAttribute('aria-label')?.trim();
  if (label) return label;

  const labelledBy = element.getAttribute('aria-labelledby');
  if (labelledBy) {
    const text = labelledBy
      .split(/\s+/)
      .map((id) => element.ownerDocument.getElementById(id))
      .filter((target): target is HTMLElement => target !== null)
      .map(textOf)
      .join(' ')
      .trim();
    if (text) return text;
  }

  const id = element.getAttribute('id');
  const forLabel = id
    ? element.ownerDocument.querySelector(`label[for="${CSS.escape(id)}"]`)
    : null;
  const label2 = forLabel ?? element.closest('label');
  if (label2 && textOf(label2)) return textOf(label2);

  // A name from the content: visible text, or the alt text of an image inside the control.
  const visible = Array.from(element.querySelectorAll('[aria-hidden="true"]')).reduce(
    (text, hidden) => text.replace(textOf(hidden), ''),
    textOf(element),
  );
  const images = Array.from(element.querySelectorAll('img'))
    .map((image) => image.getAttribute('alt')?.trim() ?? '')
    .join(' ')
    .trim();
  return `${visible} ${images}`.trim() || element.getAttribute('title')?.trim() || '';
}

/** Returns one plain sentence for each accessibility problem under `root`. Empty means none. */
export function findAccessibilityProblems(root: ParentNode): string[] {
  const problems: string[] = [];

  for (const image of Array.from(root.querySelectorAll('img'))) {
    if (isHidden(image)) continue;
    const src = image.getAttribute('src') ?? '(no src)';
    const alt = image.getAttribute('alt');
    if (alt === null) {
      problems.push(`The image ${src} has no alt attribute.`);
    } else if (alt.trim() === '') {
      // A photo may have empty alt text only when the control around it carries the name.
      const control = image.closest('button, a[href]');
      if (!control || accessibleName(control) === '') {
        problems.push(`The image ${src} has empty alt text and no named control around it.`);
      }
    }
  }

  for (const control of Array.from(root.querySelectorAll(CONTROLS))) {
    if (isHidden(control)) continue;
    if (accessibleName(control) === '') {
      problems.push(
        `A ${control.tagName.toLowerCase()} has no accessible name: ${control.outerHTML.slice(0, 120)}`,
      );
    }
  }

  return problems;
}

/** For tests: fails with a list of every problem on the page, dialogs included. */
export function expectNoAccessibilityProblems(root: ParentNode = document.body): void {
  const problems = findAccessibilityProblems(root);
  if (problems.length > 0) {
    throw new Error(`Accessibility problems:\n${problems.map((p) => `- ${p}`).join('\n')}`);
  }
}
