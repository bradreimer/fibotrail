import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';

import { App } from '../App';
import { expectNoAccessibilityProblems } from '../accessibility/accessibility';
import type { components } from '../api/schema';
import { fakeServer, status } from '../test/server';
import { installMatchMedia } from '../test/window';
import { dayHeading, formatDuration, formatKm, formatPhotoTime } from '../trip/format';
import { VIEWER_SWIPE } from '../trip/PhotoViewer';

type TripDetail = components['schemas']['TripDetail'];
type TripPhotoInfo = components['schemas']['TripPhotoInfo'];

const places = ['Orlando', 'Sanford', 'Deltona', 'DeLand'];

function photo(
  id: string,
  localTime: string,
  stopIndex: number | null,
  more: Partial<TripPhotoInfo> = {},
): TripPhotoInfo {
  const place = stopIndex === null ? null : places[stopIndex]!;
  return {
    id,
    alt: `Photo taken ${localTime.slice(0, 10)}${place ? ` in ${place}` : ''}`,
    localTime,
    latitude: stopIndex === null ? null : 28.4 + stopIndex * 0.27,
    longitude: stopIndex === null ? null : -81.55,
    place,
    stopIndex,
    isVideo: false,
    durationMs: null,
    immichUrl: `https://photos.example.com/photos/${id}`,
    onTheWay: false,
    ...more,
  };
}

const trip: TripDetail = {
  id: 7,
  status: 'added',
  name: 'Florida road trip',
  suggestedName: 'Sanford, March 2027',
  place: 'Sanford',
  country: 'United States of America',
  startDate: '2027-03-15',
  endDate: '2027-03-18',
  coverPhotoId: 'a1',
  stats: {
    days: 4,
    photos: 6,
    stops: 4,
    distanceKm: 90.1,
    farthestFromHomeKm: 4182.4,
    unplaced: 1,
  },
  stops: places.map((place, index) => ({
    index,
    latitude: 28.4 + index * 0.27,
    longitude: -81.55,
    place,
    photoCount: index === 1 ? 2 : 1,
  })),
  days: [
    {
      number: 1,
      date: '2027-03-15',
      place: 'Orlando',
      photos: [photo('a1', '2027-03-15T09:00:00', 0), photo('a2', '2027-03-15T12:00:00', null)],
    },
    {
      number: 2,
      date: '2027-03-16',
      place: 'Sanford',
      photos: [
        photo('b1', '2027-03-16T10:00:00', 1),
        photo('b2', '2027-03-16T23:30:00', 1, {
          alt: 'Photo taken 2027-03-16 at night in Sanford',
        }),
      ],
    },
    {
      number: 3,
      date: '2027-03-17',
      place: 'Deltona',
      photos: [
        photo('c1', '2027-03-17T10:00:00', 2, {
          isVideo: true,
          durationMs: 8836,
          alt: 'Video taken 2027-03-17 in Deltona',
        }),
      ],
    },
    {
      number: 4,
      date: '2027-03-18',
      place: 'DeLand',
      photos: [photo('d1', '2027-03-18T10:00:00', 3)],
    },
  ],
};

function renderTrip(path = '/trips/7') {
  return render(
    <MemoryRouter initialEntries={['/trips', path]} initialIndex={1}>
      <App />
    </MemoryRouter>,
  );
}

beforeEach(() => installMatchMedia(390));

describe('the trip page', () => {
  it('shows the header, route map, stat tiles, and photo timeline, in that order', async () => {
    fakeServer({ 'GET /api/trips/7': trip });
    const { container } = renderTrip();
    const name = await screen.findByRole('heading', { name: 'Florida road trip' });
    const map = await screen.findByRole('application', {
      name: 'Map of the route of Florida road trip',
    });
    const tiles = screen.getByRole('heading', { name: 'Trip numbers' });
    const timeline = screen.getByRole('heading', { name: 'Photos by day' });

    const order = [name, map, tiles, timeline];
    for (let index = 1; index < order.length; index++) {
      expect(
        order[index - 1]!.compareDocumentPosition(order[index]!) & Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
    }
    expect(screen.getByText('Mar 15–18, 2027 · 4 days')).toBeInTheDocument();
    expect(container.querySelector('.trip-page__cover')).toHaveAttribute(
      'src',
      '/api/photos/a1/preview',
    );
    expectNoAccessibilityProblems(container);
  });

  it('draws four pins and a gold start dot for four stops, with the route in order', async () => {
    fakeServer({ 'GET /api/trips/7': trip });
    renderTrip();
    const map = await screen.findByRole('application', { name: /Map of the route/ });

    const pins = within(map)
      .getAllByRole('button')
      .filter((pin) => pin.dataset.kind === 'stop');
    expect(pins.map((pin) => pin.textContent)).toEqual([
      'Stop 1: Orlando, 1 photo',
      'Stop 2: Sanford, 2 photos',
      'Stop 3: Deltona, 1 photo',
      'Stop 4: DeLand, 1 photo',
    ]);
    const start = within(map)
      .getAllByRole('button')
      .filter((pin) => pin.dataset.kind === 'start');
    expect(start).toHaveLength(1);
    expect(start[0]).toHaveAttribute('data-position', `${28.4},${-81.55}`);
    // Four points make three lines.
    expect(JSON.parse(map.dataset.route!)).toHaveLength(4);
    expect(
      screen.getByText('Photo route: lines connect the places where you took photos.'),
    ).toBeInTheDocument();
  });

  it('tapping a pin shows the photos of that stop', async () => {
    fakeServer({ 'GET /api/trips/7': trip });
    renderTrip();
    await userEvent.click(await screen.findByRole('button', { name: 'Stop 2: Sanford, 2 photos' }));

    const sheet = screen.getByRole('dialog', { name: 'Sanford' });
    expect(within(sheet).getByText('2 photos taken here.')).toBeInTheDocument();
    expect(within(sheet).getAllByRole('button', { name: /^Photo taken/ })).toHaveLength(2);
    expect(within(sheet).getByRole('button', { name: 'Close' })).toHaveFocus();
    expectNoAccessibilityProblems();
    await userEvent.click(within(sheet).getByRole('button', { name: 'Close' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('lists the stops in order as a text alternative to the map', async () => {
    fakeServer({ 'GET /api/trips/7': trip });
    renderTrip();
    const list = (await screen.findByText('Stops in order')).closest('details')!;
    expect(
      within(list)
        .getAllByRole('listitem')
        .map((item) => item.textContent),
    ).toEqual(['Orlando, 1 photo', 'Sanford, 2 photos', 'Deltona, 1 photo', 'DeLand, 1 photo']);
  });

  it('shows the five stat tiles and how many photos have no location', async () => {
    fakeServer({ 'GET /api/trips/7': trip });
    renderTrip();
    const tiles = (await screen.findByRole('heading', { name: 'Trip numbers' })).closest(
      'section',
    )!;
    expect(
      within(tiles)
        .getAllByRole('listitem')
        .map((tile) => tile.textContent),
    ).toEqual(['4Days', '6Photos', '4Stops', '90 kmDistance', '4,182 kmFarthest from home']);
    expect(within(tiles).getByText('1 photo has no location.')).toBeInTheDocument();
  });

  it('groups photos by local day, with a heading for each day', async () => {
    fakeServer({ 'GET /api/trips/7': trip });
    renderTrip();
    const headings = (await screen.findAllByRole('heading', { level: 4 })).map(
      (heading) => heading.textContent,
    );
    expect(headings).toEqual([
      'Day 1 · Mon, Mar 15 · Orlando',
      'Day 2 · Tue, Mar 16 · Sanford',
      'Day 3 · Wed, Mar 17 · Deltona',
      'Day 4 · Thu, Mar 18 · DeLand',
    ]);
    // The photo taken at 23:30 on day 2 is under day 2.
    const dayTwo = screen.getByRole('heading', { name: /^Day 2/ }).closest('section')!;
    expect(
      within(dayTwo).getByRole('button', { name: /2027-03-16 in Sanford/ }),
    ).toBeInTheDocument();
    expect(within(dayTwo).getAllByRole('button')).toHaveLength(2);
  });

  it('shows a video with a play mark and its length', async () => {
    fakeServer({ 'GET /api/trips/7': trip });
    renderTrip();
    const video = await screen.findByRole('button', {
      name: 'Video taken 2027-03-17 in Deltona, 0:09',
    });
    expect(video.querySelector('.photo-tile__play')).not.toBeNull();
    expect(within(video).getByText('0:09')).toBeInTheDocument();
  });

  it('says We can’t find that trip for a trip that doesn’t exist', async () => {
    fakeServer({ 'GET /api/trips/99': status(404) });
    renderTrip('/trips/99');
    expect(await screen.findByText("We can't find that trip.")).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open Trips' })).toHaveAttribute('href', '/trips');
  });

  it('explains an error and tries again', async () => {
    const server = fakeServer({ 'GET /api/trips/7': status(500) });
    renderTrip();
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    server.set('GET /api/trips/7', trip);
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByRole('heading', { name: 'Florida road trip' })).toBeInTheDocument();
  });
});

describe('the photo viewer', () => {
  it('opens a photo full screen at the preview size, with its time, place, map, and Open in Immich', async () => {
    fakeServer({ 'GET /api/trips/7': trip });
    renderTrip();
    await userEvent.click(
      await screen.findByRole('button', { name: 'Photo taken 2027-03-16 in Sanford' }),
    );

    const viewer = screen.getByRole('dialog', { name: 'Photo 3 of 6' });
    expect(
      within(viewer).getByRole('img', { name: 'Photo taken 2027-03-16 in Sanford' }),
    ).toHaveAttribute('src', '/api/photos/b1/preview');
    expect(within(viewer).getByText('Tuesday, March 16, 2027 at 10:00 AM')).toBeInTheDocument();
    expect(within(viewer).getByText('Sanford')).toBeInTheDocument();
    expect(
      within(viewer).getByRole('application', { name: /Map of where this photo was taken/ }),
    ).toBeInTheDocument();
    const link = within(viewer).getByRole('link', { name: 'Open in Immich' });
    expect(link).toHaveAttribute('href', 'https://photos.example.com/photos/b1');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
    expect(within(viewer).getByRole('button', { name: 'Close' })).toHaveFocus();
    expectNoAccessibilityProblems();
  });

  it('moves with the arrow keys and the buttons, and Escape returns to the page', async () => {
    fakeServer({ 'GET /api/trips/7': trip });
    renderTrip();
    const opened = await screen.findByRole('button', { name: 'Photo taken 2027-03-15 in Orlando' });
    await userEvent.click(opened);

    await userEvent.keyboard('{ArrowRight}');
    expect(screen.getByRole('dialog', { name: 'Photo 2 of 6' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    expect(screen.getByRole('dialog', { name: 'Photo 3 of 6' })).toBeInTheDocument();
    await userEvent.keyboard('{ArrowLeft}');
    expect(screen.getByRole('dialog', { name: 'Photo 2 of 6' })).toBeInTheDocument();

    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(opened).toHaveFocus();
  });

  it('a swipe left shows the next photo, and a swipe down closes the viewer', async () => {
    fakeServer({ 'GET /api/trips/7': trip });
    const { container } = renderTrip();
    await userEvent.click(
      await screen.findByRole('button', { name: 'Photo taken 2027-03-15 in Orlando' }),
    );
    const stage = container.ownerDocument.querySelector('.viewer__stage')!;
    const swipe = (dx: number, dy: number) => {
      fireEvent.pointerDown(stage, { pointerType: 'touch', clientX: 200, clientY: 300 });
      fireEvent.pointerMove(stage, { pointerType: 'touch', clientX: 200 + dx, clientY: 300 + dy });
      fireEvent.pointerUp(stage, { pointerType: 'touch', clientX: 200 + dx, clientY: 300 + dy });
    };

    swipe(-VIEWER_SWIPE - 10, 0);
    expect(screen.getByRole('dialog', { name: 'Photo 2 of 6' })).toBeInTheDocument();
    swipe(VIEWER_SWIPE + 10, 0);
    expect(screen.getByRole('dialog', { name: 'Photo 1 of 6' })).toBeInTheDocument();
    swipe(5, VIEWER_SWIPE + 10);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('shows no map for a photo with no location, and offers Play in Immich for a video', async () => {
    fakeServer({ 'GET /api/trips/7': trip });
    renderTrip();
    await userEvent.click(await screen.findByRole('button', { name: 'Photo taken 2027-03-15' }));
    expect(within(screen.getByRole('dialog')).queryByRole('application')).not.toBeInTheDocument();

    await userEvent.keyboard('{Escape}');
    await userEvent.click(screen.getByRole('button', { name: /^Video taken/ }));
    expect(
      within(screen.getByRole('dialog')).getByRole('link', { name: 'Play in Immich' }),
    ).toBeInTheDocument();
  });

  it('hides Open in Immich when IMMICH_EXTERNAL_URL is empty', async () => {
    const noLinks = {
      ...trip,
      days: trip.days.map((day) => ({
        ...day,
        photos: day.photos.map((item) => ({ ...item, immichUrl: null })),
      })),
    };
    fakeServer({ 'GET /api/trips/7': noLinks });
    renderTrip();
    await userEvent.click(
      await screen.findByRole('button', { name: 'Photo taken 2027-03-15 in Orlando' }),
    );
    expect(within(screen.getByRole('dialog')).queryByRole('link')).not.toBeInTheDocument();
  });
});

describe('rename and change cover', () => {
  it('saves both as a decision and shows the new name', async () => {
    const server = fakeServer({
      'GET /api/trips/7': trip,
      'GET /api/trips/7/photos': [
        { id: 'a1', alt: 'Photo taken Mar 15, 2027 in Orlando', isVideo: false },
        { id: 'd1', alt: 'Photo taken Mar 18, 2027 in DeLand', isVideo: false },
      ],
      'PUT /api/trips/7': () => {
        server.set('GET /api/trips/7', { ...trip, name: 'Spring in Florida', coverPhotoId: 'd1' });
        return status(204);
      },
    });
    renderTrip();
    await userEvent.click(await screen.findByRole('button', { name: 'Rename or change cover' }));
    const sheet = screen.getByRole('dialog', { name: 'Rename or change cover' });
    const name = within(sheet).getByLabelText('Name');
    expect(name).toHaveValue('Florida road trip');

    await userEvent.clear(name);
    await userEvent.type(name, 'Spring in Florida');
    await userEvent.click(await within(sheet).findByRole('button', { name: /DeLand/ }));
    await waitFor(() => expect(within(sheet).getByRole('button', { name: 'Save' })).toBeEnabled());
    await userEvent.click(within(sheet).getByRole('button', { name: 'Save' }));

    const sent = server.sent('PUT', '/api/trips/7')[0]!;
    expect(sent.body).toEqual({ name: 'Spring in Florida', coverPhotoId: 'd1' });
    expect(sent.headers['X-CSRF-Token']).toBe('token-1');
    expect(await screen.findByRole('heading', { name: 'Spring in Florida' })).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});

describe('hiding a photo from the map', () => {
  /** The trip as the server answers after photo `id` is put on the way: its stop goes, and the photo stays. */
  function hiddenTrip(id: string, onTheWay: boolean): TripDetail {
    const stops = onTheWay ? trip.stops.filter((stop) => stop.place !== 'Sanford') : trip.stops;
    return {
      ...trip,
      stats: { ...trip.stats, stops: stops.length },
      stops: stops.map((stop, index) => ({ ...stop, index })),
      days: trip.days.map((day) => ({
        ...day,
        photos: day.photos.map((photo) =>
          photo.id === id
            ? {
                ...photo,
                onTheWay,
                alt: onTheWay ? `${photo.alt}, hidden from the map` : photo.alt,
              }
            : photo,
        ),
      })),
    };
  }

  async function openViewer() {
    renderTrip();
    await userEvent.click(
      await screen.findByRole('button', { name: 'Photo taken 2027-03-16 in Sanford' }),
    );
    return screen.getByRole('dialog', { name: 'Photo 3 of 6' });
  }

  it('shows a Hide from map switch with its hint under the place, off for a photo that is on the map', async () => {
    fakeServer({ 'GET /api/trips/7': trip });
    const viewer = await openViewer();

    const box = within(viewer).getByRole('switch', { name: 'Hide from map' });
    expect(box).not.toBeChecked();
    expect(box).toHaveAccessibleDescription('Keeps this photo in the trip but off the map.');
    expectNoAccessibilityProblems();
  });

  it('saves at once, sends the anti-forgery token, and the map and the numbers follow', async () => {
    let current: TripDetail = trip;
    const server = fakeServer({
      'GET /api/trips/7': () => current,
      'PUT /api/photos/b1/on-the-way': () => {
        current = hiddenTrip('b1', true);
        return current;
      },
    });
    const viewer = await openViewer();

    await userEvent.click(within(viewer).getByRole('switch', { name: 'Hide from map' }));

    const sent = server.sent('PUT', '/api/photos/b1/on-the-way')[0]!;
    expect(sent.body).toEqual({ onTheWay: true });
    expect(sent.headers['X-CSRF-Token']).toBe('token-1');
    expect(within(viewer).getByRole('switch', { name: 'Hide from map' })).toBeChecked();
    // The page read the trip again: Sanford is gone from the pins, and the stop tile says 3.
    await waitFor(() =>
      expect(screen.queryByRole('button', { name: /^Stop \d: Sanford/ })).not.toBeInTheDocument(),
    );
    const tiles = screen.getByRole('heading', { name: 'Trip numbers' }).closest('section')!;
    expect(
      within(tiles)
        .getAllByRole('listitem')
        .map((tile) => tile.textContent),
    ).toEqual(['4Days', '6Photos', '3Stops', '90 kmDistance', '4,182 kmFarthest from home']);
    // The photo is still in the trip, and the switch still says it's hidden.
    expect(within(viewer).getByRole('switch', { name: 'Hide from map' })).toBeChecked();
  });

  it('shows a mark on the thumbnail, and its name says the photo is hidden from the map', async () => {
    fakeServer({ 'GET /api/trips/7': hiddenTrip('b1', true) });
    renderTrip();

    const tile = await screen.findByRole('button', {
      name: 'Photo taken 2027-03-16 in Sanford, hidden from the map',
    });

    expect(tile.querySelector('.photo-tile__hidden')).not.toBeNull();
    expect(screen.getAllByRole('button', { name: /^Photo taken/ })).toHaveLength(5);
  });

  it('brings the photo back when the box is cleared', async () => {
    let current: TripDetail = hiddenTrip('b1', true);
    const server = fakeServer({
      'GET /api/trips/7': () => current,
      'PUT /api/photos/b1/on-the-way': () => {
        current = hiddenTrip('b1', false);
        return current;
      },
    });
    renderTrip();
    await userEvent.click(
      await screen.findByRole('button', {
        name: 'Photo taken 2027-03-16 in Sanford, hidden from the map',
      }),
    );
    const viewer = screen.getByRole('dialog', { name: 'Photo 3 of 6' });
    expect(within(viewer).getByRole('switch', { name: 'Hide from map' })).toBeChecked();

    await userEvent.click(within(viewer).getByRole('switch', { name: 'Hide from map' }));

    expect(server.sent('PUT', '/api/photos/b1/on-the-way')[0]!.body).toEqual({ onTheWay: false });
    expect(await screen.findByRole('button', { name: /Stop 2: Sanford/ })).toBeInTheDocument();
    expect(within(viewer).getByRole('switch', { name: 'Hide from map' })).not.toBeChecked();
  });

  it('turns on from a tap anywhere on its row, and from the Space key', async () => {
    let hidden = false;
    const server = fakeServer({
      'GET /api/trips/7': () => hiddenTrip('b1', hidden),
      'PUT /api/photos/b1/on-the-way': () => {
        hidden = !hidden;
        return hiddenTrip('b1', hidden);
      },
    });
    const viewer = await openViewer();

    await userEvent.click(within(viewer).getByText('Hide from map'));
    expect(server.sent('PUT', '/api/photos/b1/on-the-way')[0]!.body).toEqual({ onTheWay: true });

    within(viewer).getByRole('switch', { name: 'Hide from map' }).focus();
    await userEvent.keyboard(' ');
    expect(server.sent('PUT', '/api/photos/b1/on-the-way')[1]!.body).toEqual({ onTheWay: false });
  });

  it('puts the box back and says so when the save fails', async () => {
    fakeServer({ 'GET /api/trips/7': trip, 'PUT /api/photos/b1/on-the-way': status(500) });
    const viewer = await openViewer();

    await userEvent.click(within(viewer).getByRole('switch', { name: 'Hide from map' }));

    expect(await within(viewer).findByRole('alert')).toHaveTextContent(
      "We couldn't save that. Try again.",
    );
    expect(within(viewer).getByRole('switch', { name: 'Hide from map' })).not.toBeChecked();
    expect(screen.getByRole('button', { name: /Stop 2: Sanford/ })).toBeInTheDocument();
  });
});

describe('formatting', () => {
  it('formats distances, video lengths, times, and day headings', () => {
    expect(formatKm(4.24)).toBe('4.2 km');
    expect(formatKm(90.1)).toBe('90 km');
    expect(formatKm(4182.4)).toBe('4,182 km');
    expect(formatDuration(8836)).toBe('0:09');
    expect(formatDuration(323200)).toBe('5:23');
    expect(formatPhotoTime('2027-03-16T23:30:00')).toBe('Tuesday, March 16, 2027 at 11:30 PM');
    expect(dayHeading(3, '2027-03-17', 'Orlando')).toBe('Day 3 · Wed, Mar 17 · Orlando');
    expect(dayHeading(1, '2027-03-15', null)).toBe('Day 1 · Mon, Mar 15');
  });
});
