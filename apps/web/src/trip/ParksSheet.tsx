import { useEffect, useRef, useState, type KeyboardEvent } from 'react';

import { useApi } from '../api/useApi';
import { ErrorState } from '../states/ErrorState';
import { Skeleton } from '../states/Skeleton';

interface ParksSheetProps {
  /** The ids of the parks that photos of the trip place it in. They can't be unticked here. */
  fromPhotos: string[];
  /** The ids of the parks you marked. */
  marked: string[];
  problem?: string | null;
  busy?: boolean;
  onSave: (parks: string[]) => void;
  onCancel: () => void;
}

/**
 * The sheet for marking the parks you visited on a trip. A trip from before cameras recorded a
 * position has no photo in a park, so you tick the parks yourself. See docs/specs/insights.md.
 */
export function ParksSheet({
  fromPhotos,
  marked,
  problem,
  busy = false,
  onSave,
  onCancel,
}: ParksSheetProps) {
  const parks = useApi('/api/parks');
  const [chosen, setChosen] = useState(() => new Set(marked));
  const sheet = useRef<HTMLDivElement>(null);

  // Focus starts in the sheet, and returns to where it was when the sheet closes.
  useEffect(() => {
    const before = document.activeElement as HTMLElement | null;
    sheet.current?.querySelector<HTMLElement>('button')?.focus();
    return () => before?.focus?.();
  }, []);

  function toggle(id: string, on: boolean) {
    setChosen((current) => {
      const next = new Set(current);
      if (on) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      onCancel();
      return;
    }
    if (event.key !== 'Tab') return;
    // Tab stays inside the sheet while it's open.
    const controls = sheet.current!.querySelectorAll<HTMLElement>(
      'input:not([disabled]), button:not([disabled])',
    );
    const first = controls[0]!;
    const last = controls[controls.length - 1]!;
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  // The parks, grouped by resort in the order of the collection files.
  const groups = new Map<string, NonNullable<typeof parks.data>>();
  for (const park of parks.data ?? []) {
    const key = `${park.collection} · ${park.group}`;
    groups.set(key, [...(groups.get(key) ?? []), park]);
  }

  return (
    <div className="sheet-backdrop">
      {/* The backdrop is a button for a pointer only. The keyboard uses Cancel or Escape. */}
      <button
        type="button"
        className="sheet-backdrop__close"
        tabIndex={-1}
        aria-hidden="true"
        onClick={onCancel}
      />
      {/* eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- Escape and Tab are handled for the whole dialog. */}
      <div
        className="sheet parks-sheet"
        role="dialog"
        aria-modal="true"
        aria-labelledby="parks-title"
        ref={sheet}
        onKeyDown={onKeyDown}
      >
        <h2 className="state__title" id="parks-title">
          Parks you visited
        </h2>
        <p>
          Photos with a location place a trip in a park on their own. Mark a park here when the
          photos have no location.
        </p>
        {parks.loading && <Skeleton shape="card" count={1} label="Loading the parks" />}
        {parks.error && <ErrorState message={parks.error} onRetry={parks.reload} />}
        {[...groups].map(([heading, items]) => (
          <fieldset className="parks-sheet__group" key={heading}>
            <legend>{heading}</legend>
            {items.map((park) => {
              const detected = fromPhotos.includes(park.id);
              return (
                <label className="setting-row" key={park.id}>
                  <span>
                    {park.name}
                    {detected && <span className="parks-sheet__note"> · From photos</span>}
                  </span>
                  <input
                    type="checkbox"
                    role="switch"
                    className="switch"
                    checked={detected || chosen.has(park.id)}
                    disabled={detected}
                    onChange={(event) => toggle(park.id, event.target.checked)}
                  />
                </label>
              );
            })}
          </fieldset>
        ))}
        {problem && <p role="alert">{problem}</p>}
        <div className="sheet__actions">
          <button
            type="button"
            className="button"
            disabled={busy}
            onClick={() => onSave([...chosen])}
          >
            Save
          </button>
          <button type="button" className="button button--secondary" onClick={onCancel}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
