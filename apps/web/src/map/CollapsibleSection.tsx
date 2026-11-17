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
  open,
  onToggle,
  children,
}: {
  /** The anchor of the section: a link to `#id` opens and shows it. */
  id: string;
  title: string;
  /** A short note on the closed summary, such as how many places the section lists. */
  count?: string;
  open: boolean;
  onToggle: (open: boolean) => void;
  children: ReactNode;
}) {
  return (
    <section id={id} aria-labelledby={`${id}-heading`}>
      <details
        className="life-map__section"
        open={open}
        onToggle={(event) => onToggle(event.currentTarget.open)}
      >
        <summary className="life-map__summary">
          <h3 className="life-map__heading" id={`${id}-heading`}>
            {title}
          </h3>
          {count && <span className="life-map__count">{count}</span>}
        </summary>
        <div className="life-map__body">{children}</div>
      </details>
    </section>
  );
}
