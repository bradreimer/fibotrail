import { useEffect, useRef, useState, type KeyboardEvent } from 'react';

import { apiGet, apiSend } from '../api/client';
import type { components } from '../api/schema';
import { countOf } from '../trips/dates';

export type TrailAlignment = components['schemas']['TrailAlignment'];

/** The widest offset, in minutes: 14 hours either way. */
export const MAX_OFFSET_MINUTES = 14 * 60;

/** An offset as the trail list says it, such as "trail + 5 h 07 min" or "trail − 4 h 00 min". */
export function formatOffset(minutes: number): string {
  const size = Math.abs(minutes);
  const sign = minutes < 0 ? '−' : '+';
  return `trail ${sign} ${Math.floor(size / 60)} h ${String(size % 60).padStart(2, '0')} min`;
}

/** Where an offset came from, as the trail list says it. */
export function offsetSourceLabel(source: TrailAlignment['source']): string {
  switch (source) {
    case 'you':
      return 'set by you';
    case 'estimated':
      return 'estimated';
    case 'trial':
      return 'not saved';
    default:
      return 'not estimated';
  }
}

interface OffsetSheetProps {
  tripId: number;
  trailName: string;
  alignment: TrailAlignment;
  /** All the trip's photos, for "38 of 52 photos land on this trail." */
  photoCount: number;
  csrfToken: string;
  /** Called after Save or Use estimate, so the page reads the alignment again. */
  onSaved: () => void;
  onCancel: () => void;
}

const STEPS = [
  { label: '−1 h', name: 'One hour earlier', minutes: -60 },
  { label: '−1 min', name: 'One minute earlier', minutes: -1 },
  { label: '+1 min', name: 'One minute later', minutes: 1 },
  { label: '+1 h', name: 'One hour later', minutes: 60 },
];

/**
 * Adjust time: moves a trail's offset a minute or an hour at a time, and shows at once how many
 * photos land on the trail, without saving. Save stores the offset you chose. Use estimate removes
 * it. See docs/specs/gps-trails.md#photo-alignment.
 */
export function OffsetSheet({
  tripId,
  trailName,
  alignment,
  photoCount,
  csrfToken,
  onSaved,
  onCancel,
}: OffsetSheetProps) {
  const [minutes, setMinutes] = useState(alignment.offsetMinutes);
  // The count for a trial offset, kept with the offset it was asked for.
  const [trial, setTrial] = useState<{ minutes: number; count: number } | null>(null);
  const landing =
    minutes === alignment.offsetMinutes
      ? alignment.photos.length
      : trial?.minutes === minutes
        ? trial.count
        : null;
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const sheet = useRef<HTMLDivElement>(null);
  const first = useRef<HTMLButtonElement>(null);

  // Focus starts in the sheet, and returns to where it was when the sheet closes.
  useEffect(() => {
    const before = document.activeElement as HTMLElement | null;
    first.current?.focus();
    return () => before?.focus?.();
  }, []);

  // Each change asks the server where the photos would land, without saving.
  useEffect(() => {
    if (minutes === alignment.offsetMinutes) return;
    let current = true;
    apiGet(
      `/api/trips/${tripId}/trails/alignment` as '/api/trips/{id}/trails/alignment',
      `trailId=${alignment.trailId}&offsetMinutes=${minutes}`,
    )
      .then((answer) => {
        const trail = answer.trails.find((item) => item.trailId === alignment.trailId);
        if (current && trail) setTrial({ minutes, count: trail.photos.length });
      })
      .catch((error: unknown) => {
        if (current) setProblem(error instanceof Error ? error.message : 'Try again.');
      });
    return () => {
      current = false;
    };
  }, [minutes, alignment, tripId]);

  async function save(value: number | null) {
    setBusy(true);
    setProblem(null);
    try {
      await apiSend('PUT', `/api/trips/${tripId}/trails/${alignment.trailId}/offset`, csrfToken, {
        minutes: value,
      });
      onSaved();
    } catch (error) {
      setProblem(error instanceof Error ? error.message : 'The time wasn’t saved. Try again.');
      setBusy(false);
    }
  }

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      onCancel();
      return;
    }
    if (event.key !== 'Tab') return;
    // Tab stays inside the sheet while it's open.
    const controls = sheet.current!.querySelectorAll<HTMLElement>('button:not([disabled])');
    const firstControl = controls[0]!;
    const last = controls[controls.length - 1]!;
    if (event.shiftKey && document.activeElement === firstControl) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      firstControl.focus();
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
      <div
        className="sheet"
        role="dialog"
        aria-modal="true"
        aria-labelledby="offset-sheet-title"
        ref={sheet}
        onKeyDown={onKeyDown}
      >
        <h2 className="state__title" id="offset-sheet-title">
          Adjust time
        </h2>
        <p>
          Move {trailName} until the photos land where you took them. The camera’s clock and time
          zone are both in this offset.
        </p>
        {alignment.estimatedOffsetMinutes === null && (
          <p className="trail-sheet__warning">
            Fibotrail couldn’t estimate the time. Set it so the photos land where you took them.
          </p>
        )}
        <p className="offset-sheet__value" aria-live="polite">
          Camera time: {formatOffset(minutes)}
        </p>
        <div className="offset-sheet__steps">
          {STEPS.map((step, index) => (
            <button
              key={step.label}
              type="button"
              className="button button--secondary"
              ref={index === 0 ? first : undefined}
              aria-label={step.name}
              disabled={busy || Math.abs(minutes + step.minutes) > MAX_OFFSET_MINUTES}
              onClick={() => setMinutes((current) => current + step.minutes)}
            >
              {step.label}
            </button>
          ))}
        </div>
        <p role="status" className="trail-sheet__status">
          {landing === null
            ? 'Checking where the photos land…'
            : `${landing} of ${countOf(photoCount, 'photo')} land on this trail.`}
        </p>
        {problem && <p role="alert">{problem}</p>}
        <div className="sheet__actions">
          <button
            type="button"
            className="button"
            disabled={busy}
            onClick={() => void save(minutes)}
          >
            Save
          </button>
          {alignment.estimatedOffsetMinutes !== null && (
            <button
              type="button"
              className="button button--secondary"
              disabled={busy}
              onClick={() => void save(null)}
            >
              Use estimate
            </button>
          )}
          <button type="button" className="button button--secondary" onClick={onCancel}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
