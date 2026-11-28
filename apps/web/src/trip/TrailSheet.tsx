import { useEffect, useRef, useState, type ChangeEvent, type KeyboardEvent } from 'react';

import { apiUpload } from '../api/client';
import type { components } from '../api/schema';
import { formatDateRange } from '../trips/dates';
import { formatKm } from './format';

export type TrailPreview = components['schemas']['TrailPreview'];
export type TrailInfo = components['schemas']['TrailInfo'];

const number = new Intl.NumberFormat('en-US');

/** An instant in UTC as its date in the device's time zone, such as "2027-03-15". */
export function localDate(utc: string): string {
  const date = new Date(utc);
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** The dates a trail covers, such as "Mar 15–16, 2027", in the device's time zone. */
export function trailDates(trail: { startUtc: string; endUtc: string }): string {
  return formatDateRange(localDate(trail.startUtc), localDate(trail.endUtc));
}

interface TrailSheetProps {
  tripId: number;
  tripName: string;
  csrfToken: string;
  /** Called with the saved trail. */
  onAdded: (trail: TrailInfo) => void;
  onCancel: () => void;
}

/**
 * The sheet that opens for Add GPS trail. You pick a GPX file, Fibotrail checks it and shows what
 * it covers, and Add trail saves it. See docs/specs/gps-trails.md.
 */
export function TrailSheet({ tripId, tripName, csrfToken, onAdded, onCancel }: TrailSheetProps) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<TrailPreview | null>(null);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const picker = useRef<HTMLInputElement>(null);
  const sheet = useRef<HTMLDivElement>(null);

  // Focus starts in the sheet, and returns to where it was when the sheet closes.
  useEffect(() => {
    const before = document.activeElement as HTMLElement | null;
    picker.current?.focus();
    return () => before?.focus?.();
  }, []);

  async function choose(event: ChangeEvent<HTMLInputElement>) {
    const picked = event.target.files?.[0] ?? null;
    setFile(picked);
    setPreview(null);
    setProblem(null);
    if (!picked) return;
    setBusy(true);
    try {
      setPreview(
        await apiUpload<TrailPreview>(`/api/trips/${tripId}/trails/preview`, csrfToken, picked),
      );
    } catch (error) {
      setProblem(error instanceof Error ? error.message : 'Fibotrail couldn’t read that file.');
    } finally {
      setBusy(false);
    }
  }

  async function add() {
    if (!file) return;
    setBusy(true);
    setProblem(null);
    try {
      onAdded(await apiUpload<TrailInfo>(`/api/trips/${tripId}/trails`, csrfToken, file));
    } catch (error) {
      setProblem(error instanceof Error ? error.message : 'The trail wasn’t added. Try again.');
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
      <div
        className="sheet"
        role="dialog"
        aria-modal="true"
        aria-labelledby="trail-sheet-title"
        ref={sheet}
        onKeyDown={onKeyDown}
      >
        <h2 className="state__title" id="trail-sheet-title">
          Add GPS trail
        </h2>
        <p>
          Pick a GPX file from your GPS logger. Convert the logger’s own files to GPX with GPSBabel
          first.
        </p>
        <label htmlFor="trail-file">GPX file</label>
        <input
          id="trail-file"
          className="trail-sheet__file"
          ref={picker}
          type="file"
          accept=".gpx,application/gpx+xml"
          onChange={(event) => void choose(event)}
        />
        {busy && !preview && (
          <p role="status" className="trail-sheet__status">
            Checking the file…
          </p>
        )}
        {preview && (
          <dl className="trail-sheet__preview" aria-label={`What ${preview.originalName} covers`}>
            <div>
              <dt>Dates</dt>
              <dd>{trailDates(preview)}</dd>
            </div>
            <div>
              <dt>Distance</dt>
              <dd>{formatKm(preview.distanceKm)}</dd>
            </div>
            <div>
              <dt>Points</dt>
              <dd>{number.format(preview.pointCount)}</dd>
            </div>
          </dl>
        )}
        {preview && !preview.overlapsTrip && (
          <p role="alert" className="trail-sheet__warning">
            This trail’s dates don’t overlap {tripName}. Check that you picked the right file. You
            can still add it.
          </p>
        )}
        {problem && <p role="alert">{problem}</p>}
        <div className="sheet__actions">
          <button
            type="button"
            className="button"
            disabled={busy || !preview}
            onClick={() => void add()}
          >
            Add trail
          </button>
          <button type="button" className="button button--secondary" onClick={onCancel}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
