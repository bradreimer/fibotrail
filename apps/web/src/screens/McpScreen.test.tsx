import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { expectNoAccessibilityProblems } from '../accessibility/accessibility';
import { EXAMPLES, LIMITS } from '../mcp/examples';
import { fakeServer, status } from '../test/server';
import { McpScreen } from './McpScreen';

const TOOLS = [
  'list_collections',
  'collection_visits',
  'list_countries',
  'country_summary',
  'list_trips',
  'get_trip',
  'days_away',
];

function show() {
  return render(
    <MemoryRouter initialEntries={['/mcp-setup']}>
      <McpScreen />
    </MemoryRouter>,
  );
}

describe('the MCP screen', () => {
  beforeEach(() => {
    vi.stubGlobal('location', new URL('https://trips.example.com/mcp'));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('says the server is off and shows the three steps that turn it on', async () => {
    fakeServer({ 'GET /api/mcp/status': { enabled: false, tools: [] } });
    show();

    expect(await screen.findByRole('heading', { name: 'Turn it on' })).toBeInTheDocument();
    expect(screen.getByText('MCP server: off')).toBeInTheDocument();
    const steps = screen.getAllByRole('listitem').filter((item) => item.closest('ol'));
    expect(steps).toHaveLength(3);
    expect(screen.getByText('openssl rand -hex 32')).toBeInTheDocument();
    expect(screen.getByText('MCP_TOKEN=paste-your-token-here')).toBeInTheDocument();
    expect(screen.getByText('docker compose up -d --force-recreate')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Connect an AI client' })).not.toBeInTheDocument();
  });

  it('shows the address and three ways to connect when the server is on', async () => {
    fakeServer({ 'GET /api/mcp/status': { enabled: true, tools: TOOLS } });
    show();

    expect(
      await screen.findByRole('heading', { name: 'Connect an AI client' }),
    ).toBeInTheDocument();
    expect(screen.getByText('MCP server: on')).toBeInTheDocument();
    expect(screen.getByText('https://trips.example.com/mcp')).toBeInTheDocument();
    for (const name of [
      'Claude Code',
      'Claude Desktop, and apps that start a local command',
      'Any client that takes an address and headers',
    ]) {
      expect(screen.getByText(name)).toBeInTheDocument();
    }
    expect(screen.getByText(/claude mcp add --transport http fibotrail/)).toBeInTheDocument();
    // The token is never filled in. The screen shows only the placeholder.
    expect(document.body.textContent).toContain('YOUR_MCP_TOKEN');
    expect(screen.queryByRole('note')).not.toBeInTheDocument();
  });

  it('warns that a home address is out of reach for a cloud client', async () => {
    vi.stubGlobal('location', new URL('http://192.168.1.20:11235/mcp'));
    fakeServer({ 'GET /api/mcp/status': { enabled: true, tools: TOOLS } });
    show();

    const note = await screen.findByRole('note');
    expect(note).toHaveTextContent('works only on your own network');
    expect(screen.getByText('http://192.168.1.20:11235/mcp')).toBeInTheDocument();
  });

  it('lists every example question with its tools, starting with the Disney question', async () => {
    fakeServer({ 'GET /api/mcp/status': { enabled: true, tools: TOOLS } });
    show();

    const examples = await screen.findByRole('heading', { name: 'Questions to try' });
    const section = examples.closest('section')!;
    for (const example of EXAMPLES) {
      expect(within(section).getByText(example.question)).toBeInTheDocument();
    }
    expect(within(section).getAllByRole('listitem')).toHaveLength(EXAMPLES.length);
    expect(within(section).getAllByText('collection_visits').length).toBeGreaterThan(0);
    expect(EXAMPLES[0]?.question).toBe('How many times have I been to a Disney park?');
  });

  it('says what it cannot answer, and what the client sees', async () => {
    fakeServer({ 'GET /api/mcp/status': { enabled: true, tools: TOOLS } });
    show();

    const limits = (await screen.findByRole('heading', { name: "What it can't answer" })).closest(
      'section',
    )!;
    for (const limit of LIMITS)
      expect(within(limits).getByText(limit.question)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'What your AI client sees' })).toBeInTheDocument();
    expect(
      screen.getByText(/Never photos, coordinates, trip names, or city names/),
    ).toBeInTheDocument();
  });

  it('copies the address and says so', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { ...navigator, clipboard: { writeText } });
    fakeServer({ 'GET /api/mcp/status': { enabled: true, tools: TOOLS } });
    show();

    await userEvent.click(await screen.findByRole('button', { name: /Copy the server address/ }));

    expect(writeText).toHaveBeenCalledWith('https://trips.example.com/mcp');
    expect(await screen.findByText('Copied')).toBeInTheDocument();
  });

  it('copies an example question', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { ...navigator, clipboard: { writeText } });
    fakeServer({ 'GET /api/mcp/status': { enabled: true, tools: TOOLS } });
    show();

    await userEvent.click(
      await screen.findByRole('button', {
        name: 'Copy the question: How many times have I been to a Disney park?',
      }),
    );

    expect(writeText).toHaveBeenCalledWith('How many times have I been to a Disney park?');
  });

  it('says so when copying fails', async () => {
    vi.stubGlobal('navigator', {
      ...navigator,
      clipboard: { writeText: vi.fn().mockRejectedValue(new Error('no')) },
    });
    fakeServer({ 'GET /api/mcp/status': { enabled: true, tools: TOOLS } });
    show();

    await userEvent.click(await screen.findByRole('button', { name: /Copy the server address/ }));

    expect(await screen.findByText(/Couldn't copy/)).toBeInTheDocument();
  });

  it('shows an error with Try again when the status fails', async () => {
    fakeServer({ 'GET /api/mcp/status': status(500, { detail: 'Broken' }) });
    show();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('shows a loading state first', () => {
    fakeServer({ 'GET /api/mcp/status': { enabled: false, tools: [] } });
    show();

    expect(screen.getByText('Loading the MCP screen')).toBeInTheDocument();
  });

  it.each([
    ['off', { enabled: false, tools: [] }],
    ['on', { enabled: true, tools: TOOLS }],
  ])('has no accessibility problems when the server is %s', async (_name, answer) => {
    fakeServer({ 'GET /api/mcp/status': answer });
    const { container } = show();
    await screen.findByRole('heading', { name: 'Questions to try' });

    await expectNoAccessibilityProblems(container);
  });
});
