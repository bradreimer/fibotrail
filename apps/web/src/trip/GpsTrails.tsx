import { useState } from 'react';

import { apiSend } from '../api/client';
import type { components } from '../api/schema';
import { countOf } from '../trips/dates';
import { formatKm } from './format';
import { formatOffset, OffsetSheet, offsetSourceLabel } from './OffsetSheet';
import { trailDates, TrailSheet, type TrailInfo } from './TrailSheet';

interface GpsTrailsProps {
  tripId: number;
  tripName: string;
  csrfToken: string | null;
  /** The trip's trails, or null while they load or when they couldn't be read. */
  trails: readonly TrailInfo[] | null;
  /** Why the trails couldn't be read. */
  error: string | null;
  onRetry: () => void;
  /** Where the trip's photos land on each trail, or null while it loads. */
  alignment: components['schemas']['TripAlignment'] | null;
  /** Called after a trail is added or removed, so the page reads the trails again. */
  onChange: () => void;
  /** Called after an offset is saved, so the page reads the alignment again. */
  onAligned: () => void;
}

/** The GPS trails of a trip: a list with Remove, and Add GPS trail. See docs/specs/gps-trails.md. */
export function GpsTrails({
  tripId,
  tripName,
  csrfToken,
  trails,
  error,
  onRetry,
  alignment,
  onChange,
  onAligned,
}: GpsTrailsProps) {
  const [adding, setAdding] = useState(false);
  const [adjusting, setAdjusting] = useState<number | null>(null);
  const alignmentOf = (trailId: number) =>
    alignment?.trails.find((item) => item.trailId === trailId) ?? null;
  const adjustingTrail = trails?.find((trail) => trail.id === adjusting) ?? null;
  const adjustingAlignment = adjusting === null ? null : alignmentOf(adjusting);
  const [removing, setRemoving] = useState<number | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  async function remove(trail: TrailInfo) {
    if (!csrfToken) return;
    setRemoving(trail.id);
    setProblem(null);
    try {
      await apiSend('DELETE', `/api/trips/${tripId}/trails/${trail.id}`, csrfToken);
      onChange();
    } catch (failure) {
      setProblem(failure instanceof Error ? failure.message : 'The trail wasn’t removed.');
    } finally {
      setRemoving(null);
    }
  }

  return (
    <section className="trip-trails" aria-labelledby="trip-trails">
      <h3 className="trip-trails__heading" id="trip-trails">
        GPS trails
      </h3>
      {error ? (
        <p role="alert" className="trip-trails__empty">
          {error}{' '}
          <button type="button" className="button--link" onClick={onRetry}>
            Try again
          </button>
        </p>
      ) : trails && trails.length === 0 ? (
        <p className="trip-trails__empty">
          No GPS trail yet. Add a GPX file from a GPS logger to show where you went.
        </p>
      ) : (
        <ul className="trip-trails__list">
          {(trails ?? []).map((trail) => {
            const aligned = alignmentOf(trail.id);
            return (
              <li key={trail.id}>
                <span className="trip-trails__name">{trail.originalName}</span>
                <span className="trip-trails__detail">
                  {trailDates(trail)} · {formatKm(trail.distanceKm)}
                </span>
                {aligned && (
                  <span className="trip-trails__detail">
                    Camera time: {formatOffset(aligned.offsetMinutes)} (
                    {offsetSourceLabel(aligned.source)}) ·{' '}
                    <button
                      type="button"
                      className="button--link"
                      aria-label={`Adjust time of ${trail.originalName}`}
                      onClick={() => setAdjusting(trail.id)}
                    >
                      Adjust time
                    </button>
                  </span>
                )}
                <button
                  type="button"
                  className="button button--secondary trip-trails__remove"
                  aria-label={`Remove ${trail.originalName}`}
                  disabled={removing === trail.id}
                  onClick={() => void remove(trail)}
                >
                  Remove
                </button>
              </li>
            );
          })}
        </ul>
      )}
      {alignment && alignment.trails.length > 0 && alignment.outside.length > 0 && (
        <p className="trip-trails__empty">
          {alignment.outside.length === 1
            ? '1 photo is outside the trails’ times.'
            : `${countOf(alignment.outside.length, 'photo')} are outside the trails’ times.`}
        </p>
      )}
      {problem && <p role="alert">{problem}</p>}
      <button
        type="button"
        className="button button--secondary"
        disabled={!csrfToken}
        onClick={() => setAdding(true)}
      >
        Add GPS trail
      </button>
      {adjustingTrail && adjustingAlignment && csrfToken && (
        <OffsetSheet
          tripId={tripId}
          trailName={adjustingTrail.originalName}
          alignment={adjustingAlignment}
          photoCount={alignment?.photoCount ?? 0}
          csrfToken={csrfToken}
          onCancel={() => setAdjusting(null)}
          onSaved={() => {
            setAdjusting(null);
            onAligned();
          }}
        />
      )}
      {adding && csrfToken && (
        <TrailSheet
          tripId={tripId}
          tripName={tripName}
          csrfToken={csrfToken}
          onCancel={() => setAdding(false)}
          onAdded={() => {
            setAdding(false);
            onChange();
          }}
        />
      )}
    </section>
  );
}
