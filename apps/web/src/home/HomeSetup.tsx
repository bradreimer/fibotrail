import { useMemo, useState } from 'react';

import { apiSend, type GetResponse } from '../api/client';
import { useSession } from '../api/session';
import { useApi } from '../api/useApi';
import { LazyMap } from '../map/LazyMap';
import type { MapMarker } from '../map/MapView';
import type { Position } from '../map/mapStyle';
import { ErrorState } from '../states/ErrorState';
import { Skeleton } from '../states/Skeleton';
import type { components } from '../api/schema';

export type HomeState = GetResponse<'/api/home'>;
export type SetHomeResponse = components['schemas']['SetHomeResponse'];

const count = new Intl.NumberFormat('en-US');

interface Choice extends Position {
  /** The name to show, or null for a pin that was moved by hand. */
  name: string | null;
}

interface HomeSetupProps {
  /** Called after the home location is saved and detection has run. */
  onConfirmed: (result: SetHomeResponse) => void;
}

/**
 * The home location step. Fibotrail suggests where home is, and you confirm it or choose
 * another place. Nothing is detected until you do. See docs/specs/trip-detection.md.
 */
export function HomeSetup({ onConfirmed }: HomeSetupProps) {
  const session = useSession();
  const home = useApi('/api/home');
  const [choice, setChoice] = useState<Choice | null>(null);
  const [choosing, setChoosing] = useState(false);
  const [query, setQuery] = useState('');
  const [saving, setSaving] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const places = useApi(
    choosing ? '/api/home/places' : null,
    `/api/home/places?query=${encodeURIComponent(query.trim())}`,
  );

  const start = home.data?.confirmed ?? home.data?.suggestion ?? null;
  const selected: Choice | null = choice ?? start;
  const markers = useMemo<MapMarker[]>(
    () =>
      selected
        ? [
            {
              id: 'home',
              kind: 'home',
              label: `Home: ${selected.name ?? 'the pin'}`,
              latitude: selected.latitude,
              longitude: selected.longitude,
            },
          ]
        : [],
    [selected],
  );

  if (home.loading)
    return <Skeleton shape="card" count={1} label="Looking for your home location" />;
  if (home.error) return <ErrorState message={home.error} onRetry={home.reload} />;
  if (!home.data) return null;

  if (!home.data.hasPositions) {
    return (
      <section className="state">
        <h2 className="state__title">Fibotrail can’t find trips yet</h2>
        <p>
          None of your {count.format(home.data.photoCount)} photos has a location. Fibotrail finds
          trips from where photos were taken, so it needs photos with a location. Turn on location
          in your camera app, then scan again after your next outing.
        </p>
      </section>
    );
  }

  const suggestion = home.data.suggestion;
  const untouched = choice === null && !home.data.confirmed && suggestion;

  async function save() {
    if (!selected || !session) return;
    setSaving(true);
    setProblem(null);
    try {
      onConfirmed(
        await apiSend<SetHomeResponse>('PUT', '/api/home', session.csrfToken, {
          latitude: selected.latitude,
          longitude: selected.longitude,
        }),
      );
    } catch (error) {
      setProblem(error instanceof Error ? error.message : 'The home location wasn’t saved.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="home-setup" aria-labelledby="home-setup-title">
      <h2 className="state__title" id="home-setup-title">
        Where is home?
      </h2>
      <p>
        {untouched
          ? `It looks like home is ${suggestion.name}. You took photos there on ${count.format(suggestion.days)} ${suggestion.days === 1 ? 'day' : 'days'} in the last 12 months.`
          : selected?.name
            ? `Home: ${selected.name}.`
            : 'Home is where you put the pin.'}{' '}
        Photos farther than 50 km from home count as away.
      </p>

      <LazyMap
        label="Map of your home location"
        markers={markers}
        draggableId={choosing ? 'home' : undefined}
        onDrag={(position) => setChoice({ ...position, name: null })}
      />

      {choosing && (
        <>
          <label htmlFor="home-search">Search the cities in your photos, or drag the pin.</label>
          <input
            id="home-search"
            className="home-setup__search"
            type="search"
            autoComplete="off"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
          />
          {places.error && <p role="alert">{places.error}</p>}
          {places.data && places.data.length === 0 && (
            <p>No city in your photos matches “{query.trim()}”. Drag the pin to choose a place.</p>
          )}
          {places.data && places.data.length > 0 && (
            <ul className="home-setup__places">
              {places.data.map((place) => (
                <li key={`${place.latitude},${place.longitude}`}>
                  <button
                    type="button"
                    className="home-setup__place"
                    aria-pressed={
                      selected?.latitude === place.latitude &&
                      selected.longitude === place.longitude
                    }
                    onClick={() => setChoice(place)}
                  >
                    {place.name}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </>
      )}

      {problem && <p role="alert">{problem}</p>}
      <div className="home-setup__actions">
        <button
          type="button"
          className="button"
          disabled={!selected || !session || saving}
          onClick={() => void save()}
        >
          Use this location
        </button>
        {!choosing && (
          <button
            type="button"
            className="button button--secondary"
            onClick={() => setChoosing(true)}
          >
            Choose another
          </button>
        )}
      </div>
    </section>
  );
}
