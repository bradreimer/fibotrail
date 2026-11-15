import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react';

import { useApi } from '../api/useApi';
import { ErrorState } from '../states/ErrorState';
import { Skeleton } from '../states/Skeleton';
import type { Suggestion } from './SuggestionCard';

interface AddTripSheetProps {
  suggestion: Suggestion;
  /** What the confirm button says: "Add" in the inbox, "Save" on the trip page. */
  confirmLabel?: string;
  title?: string;
  /** Why the last try failed. */
  problem?: string | null;
  busy?: boolean;
  onConfirm: (name: string, coverPhotoId: string) => void;
  onCancel: () => void;
}

/**
 * The sheet that opens for Add trip. It shows the suggested name and cover, and you can change
 * both before you add the trip.
 */
export function AddTripSheet({
  suggestion,
  confirmLabel = 'Add',
  title = 'Add this trip',
  problem,
  busy = false,
  onConfirm,
  onCancel,
}: AddTripSheetProps) {
  const [name, setName] = useState(suggestion.name);
  const [cover, setCover] = useState(suggestion.coverPhotoId);
  const photos = useApi('/api/trips/{id}/photos', `/api/trips/${suggestion.id}/photos`);
  const input = useRef<HTMLInputElement>(null);
  const sheet = useRef<HTMLFormElement>(null);

  // Focus starts in the sheet, and returns to where it was when the sheet closes.
  useEffect(() => {
    const before = document.activeElement as HTMLElement | null;
    input.current?.focus();
    input.current?.select();
    return () => before?.focus?.();
  }, []);

  function submit(event: FormEvent) {
    event.preventDefault();
    if (name.trim() !== '') onConfirm(name.trim(), cover);
  }

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      onCancel();
      return;
    }
    if (event.key !== 'Tab') return;
    // Tab stays inside the sheet while it's open.
    const controls = sheet.current!.querySelectorAll<HTMLElement>('input, button:not([disabled])');
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
      <form
        className="sheet"
        role="dialog"
        aria-modal="true"
        aria-labelledby="add-trip-title"
        ref={sheet}
        onSubmit={submit}
        onKeyDown={onKeyDown}
      >
        <h2 className="state__title" id="add-trip-title">
          {title}
        </h2>
        <label htmlFor="add-trip-name">Name</label>
        <input
          id="add-trip-name"
          className="home-setup__search"
          ref={input}
          value={name}
          maxLength={100}
          autoComplete="off"
          onChange={(event) => setName(event.target.value)}
        />
        <fieldset className="cover-picker">
          <legend>Cover photo</legend>
          {photos.loading && <Skeleton shape="card" count={1} label="Loading the photos" />}
          {photos.error && <ErrorState message={photos.error} onRetry={photos.reload} />}
          {photos.data && (
            <div className="cover-picker__grid">
              {photos.data.map((photo) => (
                <button
                  key={photo.id}
                  type="button"
                  className="cover-picker__photo"
                  aria-pressed={photo.id === cover}
                  aria-label={`Use as cover: ${photo.alt}`}
                  onClick={() => setCover(photo.id)}
                >
                  <img
                    src={`/api/photos/${photo.id}/thumbnail`}
                    alt=""
                    loading="lazy"
                    draggable={false}
                  />
                </button>
              ))}
            </div>
          )}
        </fieldset>
        {problem && <p role="alert">{problem}</p>}
        <div className="sheet__actions">
          <button type="submit" className="button" disabled={busy || name.trim() === ''}>
            {confirmLabel}
          </button>
          <button type="button" className="button button--secondary" onClick={onCancel}>
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
}
