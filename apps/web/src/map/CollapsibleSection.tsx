import type { ReactNode } from 'react';

/**
 * A section under the map that you can open and close. It is a native `details`, so a keyboard and a
 * screen reader work with it, and it is closed until you open it. The parent owns `open`, so a link
 * elsewhere on the page can open it. See docs/specs/life-map.md.
 */
export function CollapsibleSection({
  id,
  title,
  count,
  level = 3,
  open,
  onToggle,
  children,
}: {
  /** The anchor of the section: a link to `#id` opens and shows it. */
  id: string;
  title: string;
  /** A short note on the closed summary, such as how many places the section lists. */
  count?: string;
  /** The heading level: 3 for a section of the page, 4 for one inside another. */
  level?: 3 | 4;
  open: boolean;
  onToggle: (open: boolean) => void;
  children: ReactNode;
}) {
  const Heading = level === 3 ? 'h3' : 'h4';
  return (
    <section id={id} aria-labelledby={`${id}-heading`}>
      <details
        className="life-map__section"
        open={open}
        onToggle={(event) => onToggle(event.currentTarget.open)}
      >
        <summary className="life-map__summary">
          <Heading className="life-map__heading" id={`${id}-heading`}>
            {title}
          </Heading>
          {count && <span className="life-map__count">{count}</span>}
        </summary>
        <div className="life-map__body">{children}</div>
      </details>
    </section>
  );
}
