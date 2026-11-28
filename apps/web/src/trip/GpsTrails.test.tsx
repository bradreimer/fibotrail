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
