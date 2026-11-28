import { useState } from 'react';

import { apiSend } from '../api/client';
import { formatKm } from './format';
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
  /** Called after a trail is added or removed, so the page reads the trails again. */
  onChange: () => void;
}

/** The GPS trails of a trip: a list with Remove, and Add GPS trail. See docs/specs/gps-trails.md. */
export function GpsTrails({
  tripId,
  tripName,
  csrfToken,
  trails,
  error,
  onRetry,
  onChange,
}: GpsTrailsProps) {
  const [adding, setAdding] = useState(false);
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
          {(trails ?? []).map((trail) => (
            <li key={trail.id}>
              <span className="trip-trails__name">{trail.originalName}</span>
              <span className="trip-trails__detail">
                {trailDates(trail)} · {formatKm(trail.distanceKm)}
              </span>
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
          ))}
        </ul>
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
