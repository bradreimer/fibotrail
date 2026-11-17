import { act, render, screen, waitFor } from '@testing-library/react';
import { expectNoAccessibilityProblems } from '../accessibility/accessibility';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { App } from '../App';
import { SCAN_POLL_MS } from '../api/scan';
import { fakeServer, status } from '../test/server';
import { installMatchMedia } from '../test/window';
import { lastScanText } from './SettingsScreen';

const idle = {
  state: 'never',
  done: 0,
  total: null,
  message: null,
  finishedAt: null,
  photoCount: 0,
};
const running = { ...idle, state: 'running', done: 12400, total: 48000, photoCount: 12400 };
const completed = {
  ...idle,
  state: 'completed',
  done: 48000,
  total: 48000,
  finishedAt: '2026-11-15T12:03:00Z',
  photoCount: 47990,
};
const failed = {
  ...running,
  state: 'failed',
  message: "Immich didn't answer in time. Your progress is saved.",
  finishedAt: '2026-11-15T12:03:00Z',
};

/** What the server does on Scan now: the scan runs from then on. */
function startScan(server: ReturnType<typeof fakeServer>) {
  server.set('GET /api/scan', running);
  return running;
}

function renderSettings() {
  return render(
    <MemoryRouter initialEntries={['/settings']}>
      <App />
    </MemoryRouter>,
  );
}

beforeEach(() => installMatchMedia(390));
afterEach(() => vi.useRealTimers());

describe('the Immich connection in Settings', () => {
  it('shows the version when the connection works', async () => {
    fakeServer();
    renderSettings();
    expect(await screen.findByText('Connected to Immich 3.4.1.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Check again' })).not.toBeInTheDocument();
    expectNoAccessibilityProblems();
  });

  it('shows the plain message and a way to check again when Immich is unreachable', async () => {
    const message = "Can't reach Immich. Check IMMICH_URL and IMMICH_API_KEY in your .env file.";
    const server = fakeServer({
      'GET /api/immich': { connected: false, version: null, message, warning: null },
    });
    renderSettings();
    expect(await screen.findByText(message)).toBeInTheDocument();

    server.set('GET /api/immich', {
      connected: true,
      version: '3.4.1',
      message: null,
      warning: null,
    });
    await userEvent.click(screen.getByRole('button', { name: 'Check again' }));
    expect(await screen.findByText('Connected to Immich 3.4.1.')).toBeInTheDocument();
  });

  it('shows a warning for an untested Immich version', async () => {
    const warning = 'Fibotrail was tested with Immich 3.4. This server runs 4.0.2.';
    fakeServer({
      'GET /api/immich': { connected: true, version: '4.0.2', message: null, warning },
    });
    renderSettings();
    expect(await screen.findByText(warning)).toBeInTheDocument();
  });
});

describe('Scan now', () => {
  it('starts a scan with the anti-forgery token, shows the banner, and disables the button', async () => {
    const server = fakeServer({ 'POST /api/scan': () => startScan(server) });
    renderSettings();
    const button = await screen.findByRole('button', { name: 'Scan now' });
    await waitFor(() => expect(button).toBeEnabled());

    await userEvent.click(button);

    expect(server.sent('POST', '/api/scan')[0]!.headers['X-CSRF-Token']).toBe('token-1');
    expect(
      await screen.findByText('Scanning your library: 12,400 of 48,000 photos.'),
    ).toBeInTheDocument();
    expect(button).toBeDisabled();
  });

  it('Rescan everything posts to the rescan route and shows the banner', async () => {
    const server = fakeServer({ 'POST /api/rescan': () => startScan(server) });
    renderSettings();
    const button = await screen.findByRole('button', { name: 'Rescan everything' });
    await waitFor(() => expect(button).toBeEnabled());

    await userEvent.click(button);

    expect(server.sent('POST', '/api/rescan')[0]!.headers['X-CSRF-Token']).toBe('token-1');
    expect(
      await screen.findByText('Scanning your library: 12,400 of 48,000 photos.'),
    ).toBeInTheDocument();
    expect(button).toBeDisabled();
  });

  it('is disabled while a scan runs, and the rest of the app stays usable', async () => {
    fakeServer({ 'GET /api/scan': running });
    renderSettings();
    await screen.findByText('Scanning your library: 12,400 of 48,000 photos.');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Scan now' })).toBeDisabled());

    await userEvent.click(screen.getByRole('link', { name: 'Trips' }));
    expect(screen.getByRole('heading', { level: 1, name: 'Trips' })).toBeInTheDocument();
    expect(screen.getByText('Scanning your library: 12,400 of 48,000 photos.')).toBeInTheDocument();
  });

  it('follows the progress every two seconds, and the banner goes away when the scan completes', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const server = fakeServer({ 'GET /api/scan': running });
    renderSettings();
    expect(
      await screen.findByText('Scanning your library: 12,400 of 48,000 photos.'),
    ).toBeInTheDocument();

    server.set('GET /api/scan', { ...running, done: 30000 });
    await act(() => vi.advanceTimersByTimeAsync(SCAN_POLL_MS));
    expect(screen.getByText('Scanning your library: 30,000 of 48,000 photos.')).toBeInTheDocument();

    server.set('GET /api/scan', completed);
    await act(() => vi.advanceTimersByTimeAsync(SCAN_POLL_MS));
    expect(screen.queryByText(/Scanning your library/)).not.toBeInTheDocument();
    expect(
      screen.getByText(/The last scan finished on .* Fibotrail knows 47,990 photos\./),
    ).toBeInTheDocument();

    const polls = server.sent('GET', '/api/scan').length;
    await act(() => vi.advanceTimersByTimeAsync(SCAN_POLL_MS * 3));
    expect(server.sent('GET', '/api/scan')).toHaveLength(polls);
  });

  it('shows why a scan stopped, and Try again resumes it', async () => {
    const server = fakeServer({
      'GET /api/scan': failed,
      'POST /api/scan': () => startScan(server),
    });
    renderSettings();
    const alert = await screen.findByText("Immich didn't answer in time. Your progress is saved.");
    expect(alert.closest('[role="alert"]')).not.toBeNull();
    await screen.findByRole('button', { name: 'Sign out' });

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(server.sent('POST', '/api/scan')).toHaveLength(1);
    expect(await screen.findByText(/Scanning your library/)).toBeInTheDocument();
  });

  it('says so when a scan is already running', async () => {
    fakeServer({ 'POST /api/scan': status(409, { detail: 'A scan is already running.' }) });
    renderSettings();
    const button = await screen.findByRole('button', { name: 'Scan now' });
    await waitFor(() => expect(button).toBeEnabled());
    await userEvent.click(button);
    expect(await screen.findByText('A scan is already running.')).toBeInTheDocument();
  });
});

describe('the support link in Settings', () => {
  it('opens Buy Me a Coffee in a new tab', async () => {
    fakeServer();
    renderSettings();
    const link = await screen.findByRole('link', { name: 'Give Fibs and Hermann a treat' });
    expect(link).toHaveAttribute('href', 'https://www.buymeacoffee.com/bradreimer');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });
});

describe('lastScanText', () => {
  it('covers each state', () => {
    expect(lastScanText(idle as never)).toBe('Fibotrail hasn’t scanned your library yet.');
    expect(lastScanText(running as never)).toBe(
      'A scan is running. Fibotrail knows 12,400 photos so far.',
    );
    expect(lastScanText(failed as never)).toMatch(
      /^The last scan stopped on .*12,400 photos so far\.$/,
    );
    expect(lastScanText({ ...completed, photoCount: 1 } as never)).toMatch(
      /Fibotrail knows 1 photo\.$/,
    );
  });
});
