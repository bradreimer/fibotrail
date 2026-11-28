import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';

import { App } from '../App';
import { expectNoAccessibilityProblems } from '../accessibility/accessibility';
import type { components } from '../api/schema';
import { fakeServer, status } from '../test/server';
import { installMatchMedia } from '../test/window';

type TripDetail = components['schemas']['TripDetail'];
type TrailInfo = components['schemas']['TrailInfo'];
type TrailPreview = components['schemas']['TrailPreview'];

const trip: TripDetail = {
  id: 7,
  status: 'added',
  name: 'Florida road trip',
  suggestedName: 'Orlando, March 2027',
  place: 'Orlando',
  country: 'United States of America',
  startDate: '2027-03-15',
  endDate: '2027-03-18',
  coverPhotoId: 'a1',
  stats: { days: 4, photos: 1, stops: 1, distanceKm: 0, farthestFromHomeKm: 4182, unplaced: 0 },
  parks: [],
  stops: [{ index: 0, latitude: 28.4, longitude: -81.55, place: 'Orlando', photoCount: 1 }],
  days: [
    {
      number: 1,
      date: '2027-03-15',
      place: 'Orlando',
      photos: [
        {
          id: 'a1',
          alt: 'Photo taken 2027-03-15 in Orlando',
          localTime: '2027-03-15T10:00:00',
          latitude: 28.4,
          longitude: -81.55,
          place: 'Orlando',
          stopIndex: 0,
          isVideo: false,
          durationMs: null,
          immichUrl: null,
          onTheWay: false,
        },
      ],
    },
  ],
};

const morning = [
  { latitude: 28.4, longitude: -81.55 },
  { latitude: 28.41, longitude: -81.55 },
];
const afternoon = [
  { latitude: 28.45, longitude: -81.5 },
  { latitude: 28.46, longitude: -81.49 },
];

const dayOne: TrailInfo = {
  id: 1,
  originalName: 'day-1.gpx',
  startUtc: '2027-03-15T14:00:00Z',
  endUtc: '2027-03-15T21:00:00Z',
  distanceKm: 12.4,
  movingMinutes: 180,
  pointCount: 5040,
  segments: [morning, afternoon],
};

const preview: TrailPreview = {
  originalName: 'day-2.gpx',
  startUtc: '2027-03-16T14:00:00Z',
  endUtc: '2027-03-16T20:00:00Z',
  distanceKm: 8.25,
  movingMinutes: 120,
  pointCount: 4321,
  overlapsTrip: true,
};

function renderTrip() {
  return render(
    <MemoryRouter initialEntries={['/trips', '/trips/7']} initialIndex={1}>
      <App />
    </MemoryRouter>,
  );
}

const gpx = () =>
  new File(['<gpx version="1.1"></gpx>'], 'day-2.gpx', { type: 'application/gpx+xml' });

beforeEach(() => installMatchMedia(390));

describe('GPS trails on the trip page', () => {
  it('draws each segment of each trail as its own line and lists the trails', async () => {
    fakeServer({ 'GET /api/trips/7': trip, 'GET /api/trips/7/trails': [dayOne] });
    const { container } = renderTrip();

    const map = await screen.findByRole('application', { name: /Map of the route/ });
    await waitFor(() => expect(JSON.parse(map.dataset.trails!)).toEqual([morning, afternoon]));
    expect(screen.getByText('GPS trail: the path your logger recorded.')).toBeInTheDocument();
    const list = screen.getByRole('heading', { name: 'GPS trails' }).closest('section')!;
    expect(within(list).getByText('day-1.gpx')).toBeInTheDocument();
    expect(within(list).getByText('Mar 15, 2027 · 12 km')).toBeInTheDocument();
    expectNoAccessibilityProblems(container);
  });

  it('says there is no trail yet, and draws none', async () => {
    fakeServer({ 'GET /api/trips/7': trip, 'GET /api/trips/7/trails': [] });
    renderTrip();

    expect(await screen.findByText(/No GPS trail yet/)).toBeInTheDocument();
    const map = screen.getByRole('application', { name: /Map of the route/ });
    expect(JSON.parse(map.dataset.trails!)).toEqual([]);
    expect(screen.queryByText('GPS trail: the path your logger recorded.')).not.toBeInTheDocument();
  });

  it('checks a picked file, shows its dates, distance, and points, and adds it', async () => {
    let trails: TrailInfo[] = [];
    const server = fakeServer({
      'GET /api/trips/7': trip,
      'GET /api/trips/7/trails': () => trails,
      'POST /api/trips/7/trails/preview': preview,
      'POST /api/trips/7/trails': () => {
        trails = [{ ...dayOne, id: 2, originalName: 'day-2.gpx' }];
        return trails[0];
      },
    });
    renderTrip();
    await userEvent.click(await screen.findByRole('button', { name: 'Add GPS trail' }));
    const sheet = screen.getByRole('dialog', { name: 'Add GPS trail' });
    expect(within(sheet).getByLabelText('GPX file')).toHaveFocus();
    expect(within(sheet).getByRole('button', { name: 'Add trail' })).toBeDisabled();

    await userEvent.upload(within(sheet).getByLabelText('GPX file'), gpx());

    expect(await within(sheet).findByText('Mar 16, 2027')).toBeInTheDocument();
    expect(within(sheet).getByText('8.3 km')).toBeInTheDocument();
    expect(within(sheet).getByText('4,321')).toBeInTheDocument();
    expect(within(sheet).queryByRole('alert')).not.toBeInTheDocument();
    const [check] = server.sent('POST', '/api/trips/7/trails/preview?name=day-2.gpx');
    expect(check!.headers).toMatchObject({
      'X-CSRF-Token': 'token-1',
      'Content-Type': 'application/gpx+xml',
    });
    expectNoAccessibilityProblems();

    await userEvent.click(within(sheet).getByRole('button', { name: 'Add trail' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(server.sent('POST', '/api/trips/7/trails?name=day-2.gpx')).toHaveLength(1);
    expect(await screen.findByText('day-2.gpx')).toBeInTheDocument();
  });

  it('warns when the trail’s dates don’t overlap the trip, and still lets you add it', async () => {
    fakeServer({
      'GET /api/trips/7': trip,
      'GET /api/trips/7/trails': [],
      'POST /api/trips/7/trails/preview': { ...preview, overlapsTrip: false },
    });
    renderTrip();
    await userEvent.click(await screen.findByRole('button', { name: 'Add GPS trail' }));
    const sheet = screen.getByRole('dialog', { name: 'Add GPS trail' });

    await userEvent.upload(within(sheet).getByLabelText('GPX file'), gpx());

    expect(await within(sheet).findByRole('alert')).toHaveTextContent(
      'This trail’s dates don’t overlap Florida road trip.',
    );
    expect(within(sheet).getByRole('button', { name: 'Add trail' })).toBeEnabled();
  });

  it('shows the plain reason when a file is refused', async () => {
    fakeServer({
      'GET /api/trips/7': trip,
      'GET /api/trips/7/trails': [],
      'POST /api/trips/7/trails/preview': status(400, {
        detail:
          "This file isn't a GPX file. Convert the logger's files to GPX with GPSBabel, then try again.",
      }),
    });
    renderTrip();
    await userEvent.click(await screen.findByRole('button', { name: 'Add GPS trail' }));
    const sheet = screen.getByRole('dialog', { name: 'Add GPS trail' });

    await userEvent.upload(within(sheet).getByLabelText('GPX file'), gpx());

    expect(await within(sheet).findByRole('alert')).toHaveTextContent(
      "This file isn't a GPX file.",
    );
    expect(within(sheet).getByRole('button', { name: 'Add trail' })).toBeDisabled();
  });

  it('Cancel and Escape close the sheet', async () => {
    fakeServer({ 'GET /api/trips/7': trip, 'GET /api/trips/7/trails': [] });
    renderTrip();
    await userEvent.click(await screen.findByRole('button', { name: 'Add GPS trail' }));
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Add GPS trail' }));
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('removes a trail', async () => {
    let trails = [dayOne];
    const server = fakeServer({
      'GET /api/trips/7': trip,
      'GET /api/trips/7/trails': () => trails,
      'DELETE /api/trips/7/trails/1': () => {
        trails = [];
        return status(204);
      },
    });
    renderTrip();

    await userEvent.click(await screen.findByRole('button', { name: 'Remove day-1.gpx' }));

    expect(await screen.findByText(/No GPS trail yet/)).toBeInTheDocument();
    expect(server.sent('DELETE', '/api/trips/7/trails/1')).toHaveLength(1);
  });
});

type TripAlignment = components['schemas']['TripAlignment'];

const camera = {
  ...trip.days[0]!.photos[0]!,
  id: 'a2',
  alt: 'Photo taken 2027-03-15',
  localTime: '2027-03-15T11:00:00',
  latitude: null,
  longitude: null,
  place: null,
  stopIndex: null,
};
const tripWithCamera: TripDetail = {
  ...trip,
  stats: { ...trip.stats, photos: 3, unplaced: 1 },
  days: [
    {
      ...trip.days[0]!,
      photos: [
        trip.days[0]!.photos[0]!,
        camera,
        { ...camera, id: 'a3', localTime: '2027-03-15T18:00:00' },
      ],
    },
  ],
};

function aligned(more: Partial<TripAlignment['trails'][number]> = {}): TripAlignment {
  return {
    photoCount: 3,
    outside: ['a3'],
    trails: [
      {
        trailId: 1,
        offsetMinutes: -247,
        source: 'estimated',
        estimatedOffsetMinutes: -247,
        photos: [
          {
            id: 'a1',
            localTime: '2027-03-15T10:00:00',
            latitude: 28.4,
            longitude: -81.55,
            estimated: false,
          },
          {
            id: 'a2',
            localTime: '2027-03-15T11:00:00',
            latitude: 28.41,
            longitude: -81.55,
            estimated: true,
          },
        ],
        ...more,
      },
    ],
  };
}

describe('photos on a GPS trail', () => {
  it('draws a dot for each photo on the trail, hollow when the trail gives its only position', async () => {
    fakeServer({
      'GET /api/trips/7': tripWithCamera,
      'GET /api/trips/7/trails': [dayOne],
      'GET /api/trips/7/trails/alignment': aligned(),
    });
    renderTrip();

    const map = await screen.findByRole('application', { name: /Map of the route/ });
    await waitFor(() => expect(JSON.parse(map.dataset.photoDots!)).toHaveLength(2));
    const dots = JSON.parse(map.dataset.photoDots!) as { id: string; estimated: boolean }[];
    expect(dots.map((dot) => [dot.id, dot.estimated])).toEqual([
      ['a1', false],
      ['a2', true],
    ]);
    expect(
      within(map).getByRole('button', { name: 'Photo at 11:00, estimated from the GPS trail' }),
    ).toHaveAttribute('data-kind', 'estimated-photo');
  });

  it('tapping a dot opens the viewer, which shows the estimated position and says so', async () => {
    fakeServer({
      'GET /api/trips/7': tripWithCamera,
      'GET /api/trips/7/trails': [dayOne],
      'GET /api/trips/7/trails/alignment': aligned(),
    });
    renderTrip();

    await userEvent.click(
      await screen.findByRole('button', { name: 'Photo at 11:00, estimated from the GPS trail' }),
    );

    const viewer = screen.getByRole('dialog', { name: 'Photo 2 of 3' });
    expect(
      within(viewer).getByText('Estimated from the GPS trail', { selector: 'p' }),
    ).toBeInTheDocument();
    const small = await within(viewer).findByRole('application', {
      name: 'Map of where this photo was taken, estimated from the GPS trail',
    });
    expect(
      within(small).getByRole('button', { name: 'Estimated from the GPS trail' }),
    ).toHaveAttribute('data-position', '28.41,-81.55');
  });

  it('lists each trail’s camera time and the photos outside every trail', async () => {
    fakeServer({
      'GET /api/trips/7': tripWithCamera,
      'GET /api/trips/7/trails': [dayOne],
      'GET /api/trips/7/trails/alignment': aligned(),
    });
    renderTrip();

    expect(
      await screen.findByText(/Camera time: trail − 4 h 07 min \(estimated\)/),
    ).toBeInTheDocument();
    expect(screen.getByText('1 photo is outside the trails’ times.')).toBeInTheDocument();
  });

  it('Adjust time tries an offset without saving, then Save stores it', async () => {
    let saved: TripAlignment = aligned();
    const server = fakeServer({
      'GET /api/trips/7': tripWithCamera,
      'GET /api/trips/7/trails': [dayOne],
      'GET /api/trips/7/trails/alignment': (init?: RequestInit) => {
        void init;
        return saved;
      },
      'PUT /api/trips/7/trails/1/offset': () => {
        saved = aligned({ offsetMinutes: -246, source: 'you' });
        return status(204);
      },
    });
    server.set('GET /api/trips/7/trails/alignment?trailId=1&offsetMinutes=-246', {
      ...aligned({ offsetMinutes: -246, source: 'trial' }),
      trails: [{ ...aligned().trails[0]!, offsetMinutes: -246, source: 'trial', photos: [] }],
    });
    renderTrip();
    await userEvent.click(await screen.findByRole('button', { name: 'Adjust time of day-1.gpx' }));
    const sheet = screen.getByRole('dialog', { name: 'Adjust time' });
    expect(within(sheet).getByText('2 of 3 photos land on this trail.')).toBeInTheDocument();
    expect(within(sheet).getByRole('button', { name: 'One hour earlier' })).toHaveFocus();
    expectNoAccessibilityProblems();

    await userEvent.click(within(sheet).getByRole('button', { name: 'One minute later' }));

    expect(within(sheet).getByText('Camera time: trail − 4 h 06 min')).toBeInTheDocument();
    expect(await within(sheet).findByText('0 of 3 photos land on this trail.')).toBeInTheDocument();
    expect(server.sent('PUT', '/api/trips/7/trails/1/offset')).toHaveLength(0);

    await userEvent.click(within(sheet).getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(server.sent('PUT', '/api/trips/7/trails/1/offset')[0]!.body).toEqual({ minutes: -246 });
    expect(
      await screen.findByText(/Camera time: trail − 4 h 06 min \(set by you\)/),
    ).toBeInTheDocument();
  });

  it('Use estimate removes the offset you chose', async () => {
    const server = fakeServer({
      'GET /api/trips/7': tripWithCamera,
      'GET /api/trips/7/trails': [dayOne],
      'GET /api/trips/7/trails/alignment': aligned({ offsetMinutes: -200, source: 'you' }),
      'PUT /api/trips/7/trails/1/offset': status(204),
    });
    renderTrip();
    await userEvent.click(await screen.findByRole('button', { name: 'Adjust time of day-1.gpx' }));

    await userEvent.click(screen.getByRole('button', { name: 'Use estimate' }));

    await waitFor(() =>
      expect(server.sent('PUT', '/api/trips/7/trails/1/offset')[0]!.body).toEqual({
        minutes: null,
      }),
    );
  });

  it('asks you to set the time when there is no estimate', async () => {
    fakeServer({
      'GET /api/trips/7': tripWithCamera,
      'GET /api/trips/7/trails': [dayOne],
      'GET /api/trips/7/trails/alignment': aligned({
        offsetMinutes: 0,
        source: 'none',
        estimatedOffsetMinutes: null,
        photos: [],
      }),
    });
    renderTrip();
    expect(
      await screen.findByText(/Camera time: trail \+ 0 h 00 min \(not estimated\)/),
    ).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Adjust time of day-1.gpx' }));

    const sheet = screen.getByRole('dialog', { name: 'Adjust time' });
    expect(within(sheet).getByText(/Fibotrail couldn’t estimate the time/)).toBeInTheDocument();
    expect(within(sheet).queryByRole('button', { name: 'Use estimate' })).not.toBeInTheDocument();
  });
});
