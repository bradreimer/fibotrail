import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';

import { apiSend } from '../api/client';
import { useSession } from '../api/session';
import { useApi } from '../api/useApi';
import { AddTripSheet } from '../inbox/AddTripSheet';
import type { Suggestion } from '../inbox/SuggestionCard';
import { LazyMap } from '../map/LazyMap';
import type { MapMarker } from '../map/MapView';
import { BackLink } from '../shell/BackLink';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { dayHeading } from '../trip/format';
import { PhotoTile, type TripPhoto } from '../trip/PhotoTile';
import { PhotoViewer } from '../trip/PhotoViewer';
import { StatTiles } from '../trip/StatTiles';
import { countOf, formatDateRange } from '../trips/dates';

/** One trip: the header, the route map, the numbers, and the photos by day. See docs/specs/trip-page.md. */
export function TripScreen() {
  const { id } = useParams();
  const session = useSession();
  const [version, setVersion] = useState(0);
  const trip = useApi('/api/trips/{id}', `/api/trips/${id}`, version);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [stop, setStop] = useState<number | null>(null);
  const [viewing, setViewing] = useState<number | null>(null);
  const [mapOpen, setMapOpen] = useState(false);
  const stopClose = useRef<HTMLButtonElement>(null);

  // Focus moves into the stop's sheet as it opens.
  useEffect(() => {
    if (stop !== null) stopClose.current?.focus();
  }, [stop]);

  const data = trip.data;
  const photos = useMemo<TripPhoto[]>(() => data?.days.flatMap((day) => day.photos) ?? [], [data]);
  const route = useMemo(() => data?.stops ?? [], [data]);
  const markers = useMemo<MapMarker[]>(() => {
    if (!data || data.stops.length === 0) return [];
    const first = data.stops[0]!;
    return [
      {
        id: 'start',
        kind: 'start',
        label: `Start: ${first.place ?? 'first stop'}`,
        latitude: first.latitude,
        longitude: first.longitude,
      },
      ...data.stops.map((item) => ({
        id: String(item.index),
        kind: 'stop' as const,
        label: `Stop ${item.index + 1}: ${item.place ?? 'Unnamed place'}, ${countOf(item.photoCount, 'photo')}`,
        latitude: item.latitude,
        longitude: item.longitude,
        count: item.photoCount,
      })),
    ];
  }, [data]);

  if (trip.loading && !data) {
    return (
      <div className="trip-page" role="status" aria-busy="true">
        <BackLink to="/trips">Trips</BackLink>
        <span className="visually-hidden">Loading the trip</span>
        <span className="skeleton trip-page__cover" aria-hidden="true" />
        <span className="skeleton map-frame" aria-hidden="true" />
        <span className="skeleton skeleton--text" aria-hidden="true" />
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (trip.errorStatus === 404) {
    return (
      <EmptyState
        title="Trip not found"
        message="We can't find that trip."
        action={
          <Link className="button" to="/trips">
            Open Trips
          </Link>
        }
      />
    );
  }
  if (trip.error) return <ErrorState message={trip.error} onRetry={trip.reload} />;
  if (!data) return null;

  const dates = formatDateRange(data.startDate, data.endDate);
  // The sheet of the inbox takes a suggestion. The trip page gives it what it reads.
  const asSuggestion = {
    id: data.id,
    name: data.name,
    coverPhotoId: data.coverPhotoId,
  } as Suggestion;
  const stopPhotos = stop === null ? [] : photos.filter((photo) => photo.stopIndex === stop);
  const open = (photo: TripPhoto) => setViewing(photos.indexOf(photo));

  async function save(name: string, coverPhotoId: string) {
    if (!session) return;
    setSaving(true);
    setProblem(null);
    try {
      await apiSend('PUT', `/api/trips/${data!.id}`, session.csrfToken, { name, coverPhotoId });
      setEditing(false);
      setVersion((current) => current + 1);
    } catch (error) {
      setProblem(error instanceof Error ? error.message : 'The change wasn’t saved.');
    } finally {
      setSaving(false);
    }
  }

  /** Puts a photo on the way, or brings it back. The trip is read again, so the map and the numbers follow. */
  async function hide(photoId: string, onTheWay: boolean) {
    if (!session) throw new Error('Sign in to continue.');
    await apiSend('PUT', `/api/photos/${photoId}/on-the-way`, session.csrfToken, { onTheWay });
    setVersion((current) => current + 1);
  }

  return (
    <article className="trip-page">
      <BackLink to="/trips">Trips</BackLink>
      <header className="trip-page__header">
        <img
          className="trip-page__cover"
          src={`/api/photos/${data.coverPhotoId}/preview`}
          alt={`${data.name}, ${dates}`}
          draggable={false}
        />
        <div className="trip-page__title">
          <h2 className="trip-page__name">{data.name}</h2>
          <p className="trip-page__line">
            {dates} · {countOf(data.stats.days, 'day')}
          </p>
        </div>
        <button
          type="button"
          className="button button--secondary trip-page__edit"
          onClick={() => setEditing(true)}
        >
          Rename or change cover
        </button>
      </header>

      <div className="trip-page__body">
        <section className="trip-page__map" aria-labelledby="trip-route">
          <h3 className="visually-hidden" id="trip-route">
            Route
          </h3>
          {route.length > 0 ? (
            <div className={mapOpen ? 'trip-map trip-map--open' : 'trip-map'}>
              <LazyMap
                label={`Map of the route of ${data.name}`}
                markers={markers}
                route={route}
                onSelect={(selected) => setStop(Number(selected))}
                detail="fine"
              />
              <button
                type="button"
                className="viewer__button trip-map__toggle"
                onClick={() => setMapOpen(!mapOpen)}
              >
                {mapOpen ? 'Close map' : 'Full screen'}
              </button>
            </div>
          ) : null}
          <p className="map-caption">
            Photo route: lines connect the places where you took photos.
          </p>
          <details className="trip-page__stops">
            <summary>Stops in order</summary>
            <ol>
              {data.stops.map((item) => (
                <li key={item.index}>
                  <button
                    type="button"
                    className="button--link"
                    onClick={() => setStop(item.index)}
                  >
                    {item.place ?? 'Unnamed place'}, {countOf(item.photoCount, 'photo')}
                  </button>
                </li>
              ))}
            </ol>
          </details>
          <StatTiles stats={data.stats} />
          <section className="trip-parks" aria-labelledby="trip-parks">
            <h3 className="trip-parks__heading" id="trip-parks">
              Parks
            </h3>
            {data.parks.length === 0 ? (
              <p className="trip-parks__empty">No Disney or Universal parks on this trip yet.</p>
            ) : (
              <ul className="trip-parks__list">
                {data.parks.map((park) => (
                  <li key={park.id}>
                    <span>{park.name}</span>
                    <span className="trip-parks__source">
                      {park.source === 'photos' ? 'From photos' : 'Marked by you'}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </section>

        <section className="trip-page__timeline" aria-labelledby="trip-photos">
          <h3 className="visually-hidden" id="trip-photos">
            Photos by day
          </h3>
          {data.days.map((day) => (
            <section key={day.date} className="trip-day" aria-labelledby={`day-${day.number}`}>
              <h4 className="trip-day__heading" id={`day-${day.number}`}>
                {dayHeading(day.number, day.date, day.place)}
              </h4>
              <div className="photo-row">
                {day.photos.map((photo) => (
                  <PhotoTile key={photo.id} photo={photo} onOpen={() => open(photo)} />
                ))}
              </div>
            </section>
          ))}
        </section>
      </div>

      {stop !== null && (
        <div className="sheet-backdrop">
          <button
            type="button"
            className="sheet-backdrop__close"
            tabIndex={-1}
            aria-hidden="true"
            onClick={() => setStop(null)}
          />
          {/* eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- Escape closes the sheet. */}
          <div
            className="sheet"
            role="dialog"
            aria-modal="true"
            aria-labelledby="stop-title"
            onKeyDown={(event) => event.key === 'Escape' && setStop(null)}
          >
            <h3 className="state__title" id="stop-title">
              {data.stops[stop]?.place ?? 'Unnamed place'}
            </h3>
            <p>{countOf(stopPhotos.length, 'photo')} taken here.</p>
            <div className="photo-row">
              {stopPhotos.map((photo) => (
                <PhotoTile
                  key={photo.id}
                  photo={photo}
                  onOpen={() => {
                    setStop(null);
                    open(photo);
                  }}
                />
              ))}
            </div>
            <div className="sheet__actions">
              <button
                type="button"
                className="button button--secondary"
                ref={stopClose}
                onClick={() => setStop(null)}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {viewing !== null && photos[viewing] && (
        <PhotoViewer
          photos={photos}
          index={viewing}
          onIndexChange={setViewing}
          onClose={() => setViewing(null)}
          onHideChange={hide}
        />
      )}

      {editing && (
        <AddTripSheet
          suggestion={asSuggestion}
          title="Rename or change cover"
          confirmLabel="Save"
          busy={saving}
          problem={problem}
          onCancel={() => {
            setEditing(false);
            setProblem(null);
          }}
          onConfirm={(name, coverPhotoId) => void save(name, coverPhotoId)}
        />
      )}
    </article>
  );
}
