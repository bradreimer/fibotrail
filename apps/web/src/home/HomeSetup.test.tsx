import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';

import { App } from '../App';
import { foundText } from '../screens/InboxScreen';
import { fakeServer, status } from '../test/server';
import { installMatchMedia } from '../test/window';

const vancouver = {
  latitude: 49.25,
  longitude: -123.1,
  name: 'Vancouver, British Columbia, Canada',
};
const orlando = {
  latitude: 28.4,
  longitude: -81.55,
  name: 'Orlando, Florida, United States of America',
};
const suggested = {
  confirmed: null,
  suggestion: { ...vancouver, days: 212 },
  hasPositions: true,
  photoCount: 71314,
};
const saved = { home: vancouver, trips: 12, newSuggestions: 12 };

function renderInbox() {
  return render(
    <MemoryRouter initialEntries={['/inbox']}>
      <App />
    </MemoryRouter>,
  );
}

beforeEach(() => installMatchMedia(390));

describe('the home location step', () => {
  it('suggests a home location by name and on a map, and waits for you to confirm it', async () => {
    const server = fakeServer({ 'GET /api/home': suggested });
    renderInbox();

    expect(
      await screen.findByText(
        /It looks like home is Vancouver, British Columbia, Canada\. You took photos there on 212 days in the last 12 months\./,
      ),
    ).toBeInTheDocument();
    const pin = await screen.findByRole('button', { name: `Home: ${vancouver.name}` });
    expect(pin).toHaveAttribute('data-position', '49.25,-123.1');
    // The home location map keeps the 1:50 million outline (ADR-020).
    expect(screen.getByRole('application')).toHaveAttribute('data-detail', 'coarse');
    expect(screen.getByRole('button', { name: 'Use this location' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Choose another' })).toBeInTheDocument();
    expect(server.sent('PUT', '/api/home')).toHaveLength(0);
  });

  it('saves the suggestion with Use this location, and then shows the inbox', async () => {
    const server = fakeServer({
      'GET /api/home': suggested,
      'PUT /api/home': () => {
        // Detection ran on the server: home is confirmed, and the inbox has suggestions.
        server.set('GET /api/home', { ...suggested, confirmed: vancouver });
        server.set('GET /api/inbox/count', { count: 12 });
        return saved;
      },
    });
    renderInbox();
    const use = await screen.findByRole('button', { name: 'Use this location' });
    await waitFor(() => expect(use).toBeEnabled());

    await userEvent.click(use);

    const sent = server.sent('PUT', '/api/home')[0]!;
    expect(sent.body).toEqual({ latitude: 49.25, longitude: -123.1 });
    expect(sent.headers['X-CSRF-Token']).toBe('token-1');
    // The home step gives way to the inbox, and the badge shows what detection found.
    expect(await screen.findByRole('tab', { name: 'Suggestions' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Where is home?' })).not.toBeInTheDocument();
    expect(
      await screen.findByRole('link', { name: 'Inbox, 12 possible trips' }),
    ).toBeInTheDocument();
  });

  it('lets you choose another place by searching the cities in your photos', async () => {
    const server = fakeServer({
      'GET /api/home': suggested,
      'GET /api/home/places': [orlando, vancouver],
      'GET /api/home/places?query=orl': [orlando],
      'PUT /api/home': { ...saved, home: orlando },
    });
    renderInbox();
    await userEvent.click(await screen.findByRole('button', { name: 'Choose another' }));

    await userEvent.type(screen.getByLabelText(/Search the cities in your photos/), 'orl');
    await waitFor(() => expect(screen.queryByRole('button', { name: vancouver.name })).toBeNull());
    await userEvent.click(screen.getByRole('button', { name: orlando.name }));

    expect(screen.getByRole('button', { name: orlando.name })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(await screen.findByRole('button', { name: `Home: ${orlando.name}` })).toHaveAttribute(
      'data-position',
      '28.4,-81.55',
    );
    await userEvent.click(screen.getByRole('button', { name: 'Use this location' }));
    expect(server.sent('PUT', '/api/home')[0]!.body).toEqual({ latitude: 28.4, longitude: -81.55 });
  });

  it('lets you choose another place by moving the pin', async () => {
    const server = fakeServer({
      'GET /api/home': suggested,
      'GET /api/home/places': [],
      'PUT /api/home': saved,
    });
    renderInbox();
    await userEvent.click(await screen.findByRole('button', { name: 'Choose another' }));

    await userEvent.click(await screen.findByRole('button', { name: 'Drag the pin to Sydney' }));

    expect(screen.getByText(/Home is where you put the pin\./)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Use this location' }));
    expect(server.sent('PUT', '/api/home')[0]!.body).toEqual({
      latitude: -33.87,
      longitude: 151.21,
    });
  });

  it('says so when no city matches the search', async () => {
    fakeServer({ 'GET /api/home': suggested, 'GET /api/home/places': [] });
    renderInbox();
    await userEvent.click(await screen.findByRole('button', { name: 'Choose another' }));
    expect(await screen.findByText(/No city in your photos matches/)).toBeInTheDocument();
  });

  it('explains that it can’t find trips when no photo has a location', async () => {
    fakeServer({
      'GET /api/home': { confirmed: null, suggestion: null, hasPositions: false, photoCount: 40 },
    });
    renderInbox();
    expect(
      await screen.findByRole('heading', { name: 'Fibotrail can’t find trips yet' }),
    ).toBeInTheDocument();
    expect(screen.getByText(/None of your 40 photos has a location\./)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Use this location' })).not.toBeInTheDocument();
  });

  it('shows the plain message when the home location can’t be saved', async () => {
    fakeServer({
      'GET /api/home': suggested,
      'PUT /api/home': status(400, {
        detail: "That position isn't on the map. Choose a place, then try again.",
      }),
    });
    renderInbox();
    const use = await screen.findByRole('button', { name: 'Use this location' });
    await waitFor(() => expect(use).toBeEnabled());
    await userEvent.click(use);
    expect(await screen.findByRole('alert')).toHaveTextContent("That position isn't on the map.");
  });

  it('isn’t shown before the first scan', async () => {
    fakeServer();
    renderInbox();
    expect(
      await screen.findByRole('heading', { name: 'No possible trips right now' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Where is home?' })).not.toBeInTheDocument();
  });
});

describe('the home location in Settings', () => {
  it('shows the confirmed home and lets you change it', async () => {
    const server = fakeServer({
      'GET /api/home': { ...suggested, confirmed: vancouver },
      'PUT /api/home': { home: orlando, trips: 3, newSuggestions: 1 },
      'GET /api/home/places': [orlando],
    });
    render(
      <MemoryRouter initialEntries={['/settings']}>
        <App />
      </MemoryRouter>,
    );
    expect(await screen.findByText(`Home is ${vancouver.name}.`)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Change' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Choose another' }));
    await userEvent.click(await screen.findByRole('button', { name: orlando.name }));
    server.set('GET /api/home', { ...suggested, confirmed: orlando });
    await userEvent.click(screen.getByRole('button', { name: 'Use this location' }));

    expect(
      await screen.findByText(`Home is ${orlando.name}. We found 3 possible trips.`),
    ).toBeInTheDocument();
  });
});

describe('foundText', () => {
  it('counts trips in plain words', () => {
    expect(foundText({ home: vancouver, trips: 1, newSuggestions: 1 })).toBe(
      'Home is Vancouver, British Columbia, Canada. We found 1 possible trip.',
    );
    expect(foundText({ home: vancouver, trips: 0, newSuggestions: 0 })).toBe(
      'Home is Vancouver, British Columbia, Canada. Fibotrail found no trips in your photos yet.',
    );
  });
});
