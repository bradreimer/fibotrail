import { useEffect, useId, useMemo, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';

import { apiSend } from '../../api/client';
import type { components } from '../../api/schema';
import { useSession } from '../../api/session';
import { useApi } from '../../api/useApi';
import { LazyMap } from '../../map/LazyMap';
import type { Position } from '../../map/mapStyle';
import type { MapMarker } from '../../map/MapView';
import { ErrorState } from '../../states/ErrorState';
import { countOf, formatDateRange } from '../../trips/dates';
import { GameStatsPanel } from '../GameStatsPanel';
import { shareResult, type ShareOutcome } from '../share';
import { localDate } from '../today';
import { ARROW, BAND_WORDS, describeMark, formatKm, SQUARE, type PinMark } from './marks';
import { loadPlaces, type PickPlace } from './places';

type Puzzle = components['schemas']['PinPuzzleView'];
type Result = components['schemas']['PinResultView'];

/**
 * The Pin game: one of your trip photos, and a map. Tap where you took it. Each miss shows how far
 * off the pin is and which way to go. The server grades every pin and stores the day's result. See
 * docs/specs/pin-game.md.
 */
export default function PinGame() {
  const date = localDate();
  const puzzle = useApi('/api/games/pin/puzzle', `/api/games/pin/puzzle?date=${date}`);
  const [finished, setFinished] = useState<Result | null>(null);

  if (puzzle.loading) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading today's puzzle</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (puzzle.error) return <ErrorState message={puzzle.error} onRetry={puzzle.reload} />;
  if (!puzzle.data) return null;

  const data = puzzle.data;
  const heading = (
    <h2 className="timeline__title">
      Pin <span className="timeline__number">#{data.number}</span>
    </h2>
  );

  if (data.tripsNeeded > 0 || !data.photoId) {
    return (
      <section className="pin">
        {heading}
        <p className="games-page__lead">
          Pin needs a trip with photos that have a location. Add one, and today's puzzle is ready.{' '}
          <Link to="/inbox">Open the inbox</Link>
        </p>
      </section>
    );
  }

  const result = finished ?? data.result;
  if (result) return <PinResult heading={heading} result={result} />;
  return <PinPlay heading={heading} puzzle={data} photoId={data.photoId} onFinish={setFinished} />;
}

/** The pins of earlier tries, dimmed, and the one being placed. */
function pinMarkers(marks: readonly PinMark[], pin: Position | null): MapMarker[] {
  const past = marks.map((mark, index): MapMarker => ({
    id: `try-${index + 1}`,
    kind: 'past',
    label: `Try ${index + 1}: ${describeMark(mark)}`,
    latitude: mark.pin.latitude,
    longitude: mark.pin.longitude,
  }));
  return pin ? [...past, { id: 'pin', kind: 'guess', label: 'Your pin', ...pin }] : past;
}

/** Today's puzzle while you play it. The pins and their marks live here until the game ends. */
function PinPlay({
  heading,
  puzzle,
  photoId,
  onFinish,
}: {
  heading: ReactNode;
  puzzle: Puzzle;
  photoId: string;
  onFinish: (result: Result) => void;
}) {
  const session = useSession();
  const [pin, setPin] = useState<Position | null>(null);
  const [marks, setMarks] = useState<PinMark[]>([]);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [announcement, setAnnouncement] = useState('');
  const triesLeft = puzzle.maxTries - marks.length;
  const markers = useMemo(() => pinMarkers(marks, pin), [marks, pin]);

  function place(position: Position, how: string) {
    setPin(position);
    setAnnouncement(`Pin placed ${how}. Check when you're ready.`);
  }

  async function finish(all: readonly PinMark[]) {
    if (!session) return;
    onFinish(
      await apiSend<Result>('POST', '/api/games/pin/results', session.csrfToken, {
        date: puzzle.date,
        pins: all.map((mark) => mark.pin),
      }),
    );
  }

  async function check() {
    if (!session || !pin) return;
    setBusy(true);
    setProblem(null);
    try {
      const mark = await apiSend<PinMark>('POST', '/api/games/pin/check', session.csrfToken, {
        date: puzzle.date,
        pin,
      });
      const all = [...marks, mark];
      setMarks(all);
      setPin(null);
      setAnnouncement(describeMark(mark));
      if (mark.band === 'green' || all.length >= puzzle.maxTries) await finish(all);
    } catch (error) {
      setProblem(error instanceof Error ? error.message : 'Something went wrong. Try again.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="pin">
      {heading}
      <p className="games-page__lead">
        Tap the map where you took this photo. {countOf(triesLeft, 'try', 'tries')} left.
      </p>
      <img
        className="pin__photo"
        src={`/api/photos/${photoId}/preview`}
        alt="Today's puzzle. Where was this?"
      />
      <LazyMap
        className="pin__map"
        label="World map. Tap to place your pin."
        markers={markers}
        fit={false}
        onMapTap={(position) => !busy && place(position, 'on the map')}
      />
      <PlacePicker disabled={busy} onPick={(picked) => place(picked.center, `in ${picked.name}`)} />
      {marks.length > 0 && <MarkList marks={marks} />}
      <p className="visually-hidden" role="status">
        {announcement}
      </p>
      {problem && (
        <p className="timeline__problem" role="alert">
          {problem}
        </p>
      )}
      <button
        type="button"
        className="button timeline__check"
        disabled={busy || !session || !pin}
        onClick={() => void check()}
      >
        {busy ? 'Checking…' : pin ? 'Check' : 'Place a pin to check'}
      </button>
    </section>
  );
}

/**
 * The text alternative to the map: pick a country, then a region where the map has them. The pin
 * goes to the center of the place.
 */
function PlacePicker({
  disabled,
  onPick,
}: {
  disabled: boolean;
  onPick: (place: PickPlace) => void;
}) {
  const [open, setOpen] = useState(false);
  const [places, setPlaces] = useState<PickPlace[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [country, setCountry] = useState('');
  const countryId = useId();
  const regionId = useId();

  useEffect(() => {
    if (!open || places) return;
    let current = true;
    loadPlaces().then(
      (loaded) => current && setPlaces(loaded),
      () => current && setFailed(true),
    );
    return () => {
      current = false;
    };
  }, [open, places]);

  const chosen = places?.find((place) => place.key === country);

  return (
    <details
      className="pin__picker"
      onToggle={(event) => setOpen((event.currentTarget as HTMLDetailsElement).open)}
    >
      <summary>Pick a place from a list</summary>
      {failed && <p role="alert">The list didn't load. Close it and open it again.</p>}
      {!places && !failed && open && <p role="status">Loading places…</p>}
      {places && (
        <div className="pin__picker-fields">
          <label htmlFor={countryId}>Country</label>
          <select
            id={countryId}
            value={country}
            disabled={disabled}
            onChange={(event) => {
              setCountry(event.target.value);
              const place = places.find((item) => item.key === event.target.value);
              if (place) onPick(place);
            }}
          >
            <option value="">Choose a country</option>
            {places.map((place) => (
              <option key={place.key} value={place.key}>
                {place.name}
              </option>
            ))}
          </select>
          {chosen && chosen.regions.length > 0 && (
            <>
              <label htmlFor={regionId}>Region</label>
              <select
                id={regionId}
                defaultValue=""
                disabled={disabled}
                onChange={(event) => {
                  const region = chosen.regions.find((item) => item.key === event.target.value);
                  if (region) onPick(region);
                }}
              >
                <option value="">Anywhere in {chosen.name}</option>
                {chosen.regions.map((region) => (
                  <option key={region.key} value={region.key}>
                    {region.name}
                  </option>
                ))}
              </select>
            </>
          )}
        </div>
      )}
    </details>
  );
}

/** One line per try: the square, the arrow, and the distance. */
function MarkList({ marks }: { marks: readonly PinMark[] }) {
  return (
    <ol className="pin__marks" aria-label="Your tries">
      {marks.map((mark, index) => (
        <li key={index} aria-label={`Try ${index + 1}: ${describeMark(mark)}`}>
          <span aria-hidden="true">
            <span className="pin__square">{SQUARE[mark.band]}</span>
            {mark.direction && <span className="pin__arrow">{ARROW[mark.direction]}</span>}
            <span className="pin__detail">
              {BAND_WORDS[mark.band]} · {formatKm(mark.distanceKm)}
            </span>
          </span>
        </li>
      ))}
    </ol>
  );
}

/** The end of the game: a line from your last pin to the answer, and the photo with its trip. */
function PinResult({ heading, result }: { heading: ReactNode; result: Result }) {
  const [shared, setShared] = useState<ShareOutcome | null>(null);
  const label =
    shared === 'copied' ? 'Copied' : shared === 'failed' ? "Couldn't share. Try again." : 'Share';
  const { answer, marks } = result;
  const last = marks[marks.length - 1];
  const markers = useMemo(
    (): MapMarker[] => [
      ...pinMarkers(marks, null),
      {
        id: 'answer',
        kind: 'answer',
        label: `Where you took it: ${answer.tripName}`,
        latitude: answer.latitude,
        longitude: answer.longitude,
      },
    ],
    [marks, answer],
  );
  const image = (
    <img
      className="slide-photo__image"
      src={`/api/photos/${answer.photoId}/preview`}
      alt={answer.tripName}
    />
  );
  const route = useMemo(
    () => (last ? [last.pin, { latitude: answer.latitude, longitude: answer.longitude }] : []),
    [last, answer],
  );

  return (
    <section className="pin">
      {heading}
      <p className="timeline__verdict">
        {result.solved
          ? `Solved in ${countOf(result.tries, 'try', 'tries')}.`
          : `So close. It was ${last ? formatKm(last.distanceKm) : 'farther'} away.`}
      </p>
      <MarkList marks={marks} />
      <LazyMap
        className="pin__map"
        label={`World map with your pins and where you took the photo, in ${answer.place ?? answer.tripName}.`}
        markers={markers}
        route={route}
      />
      <figure className="slide-photo">
        {answer.immichUrl ? (
          <a
            className="slide-photo__link"
            href={answer.immichUrl}
            target="_blank"
            rel="noopener noreferrer"
            aria-label={`Open the photo from ${answer.tripName} in Immich`}
          >
            {image}
          </a>
        ) : (
          image
        )}
        <figcaption className="slide-photo__caption">
          <Link className="slide-photo__name" to={`/trips/${answer.tripId}`}>
            {answer.tripName}
          </Link>
          <span className="slide-photo__detail">
            {formatDateRange(answer.startDate, answer.endDate)}
            {answer.place && ` · ${answer.place}`}
          </span>
        </figcaption>
      </figure>
      {answer.immichUrl && <p className="games-page__lead">Tap the photo to open it in Immich.</p>}
      <button
        type="button"
        className="button timeline__check"
        onClick={() => void shareResult(result.share).then(setShared)}
      >
        {label}
      </button>
      <GameStatsPanel stats={result.stats} today={result.solved ? result.tries : undefined} />
      <p className="games-page__lead">A new puzzle comes tomorrow.</p>
    </section>
  );
}
