import { describe, expect, it } from 'vitest';

import {
  TOKEN_PLACEHOLDER,
  bridgeConfig,
  claudeCodeCommand,
  isHomeAddress,
  serverAddress,
  urlConfig,
} from './connect';
import { EXAMPLES, LIMITS, exampleGroups, toolsOf } from './examples';

describe('the server address', () => {
  it('is the origin with /mcp, and a trailing slash is dropped', () => {
    expect(serverAddress('https://trips.example.com')).toBe('https://trips.example.com/mcp');
    expect(serverAddress('https://trips.example.com/')).toBe('https://trips.example.com/mcp');
  });
});

describe('a home address', () => {
  it.each([
    'http://localhost:11235',
    'http://192.168.1.20:11235',
    'https://192.168.1.20',
    'https://10.0.0.5',
    'https://172.20.1.1',
    'https://fibotrail',
    'https://fibotrail.local',
    'http://trips.example.com',
    'not a url',
  ])('%s is reachable only at home', (origin) => {
    expect(isHomeAddress(origin)).toBe(true);
  });

  it.each(['https://trips.example.com', 'https://172.32.0.1', 'https://8.8.8.8'])(
    '%s is reachable from the internet',
    (origin) => {
      expect(isHomeAddress(origin)).toBe(false);
    },
  );
});

describe('the connection text', () => {
  const address = 'https://trips.example.com/mcp';

  it('puts the address and a token placeholder in the Claude Code command', () => {
    const command = claudeCodeCommand(address);
    expect(command).toContain(`--transport http fibotrail ${address}`);
    expect(command).toContain(`Bearer ${TOKEN_PLACEHOLDER}`);
  });

  it('is valid JSON for the two settings files, with the placeholder and never a real token', () => {
    const bridge = JSON.parse(bridgeConfig(address)) as {
      mcpServers: { fibotrail: { command: string; args: string[] } };
    };
    expect(bridge.mcpServers.fibotrail.command).toBe('npx');
    expect(bridge.mcpServers.fibotrail.args).toEqual([
      'mcp-remote',
      address,
      '--header',
      `Authorization: Bearer ${TOKEN_PLACEHOLDER}`,
    ]);

    const url = JSON.parse(urlConfig(address)) as {
      mcpServers: { fibotrail: { url: string; headers: Record<string, string> } };
    };
    expect(url.mcpServers.fibotrail.url).toBe(address);
    expect(url.mcpServers.fibotrail.headers.Authorization).toBe(`Bearer ${TOKEN_PLACEHOLDER}`);
  });
});

describe('the example questions', () => {
  it('lead with the Disney question', () => {
    expect(EXAMPLES[0]?.question).toBe('How many times have I been to a Disney park?');
    expect(toolsOf(EXAMPLES[0]!)).toEqual(['collection_visits']);
  });

  it('have unique ids, and every one names a tool, a question, and what the answer holds', () => {
    expect(new Set(EXAMPLES.map((example) => example.id)).size).toBe(EXAMPLES.length);
    for (const example of EXAMPLES) {
      expect(example.question).toMatch(/\?$|\.$/);
      expect(example.answer.length).toBeGreaterThan(10);
      expect(example.calls.length).toBeGreaterThan(0);
    }
  });

  it('only call tools that the server has', () => {
    const tools = [
      'list_collections',
      'collection_visits',
      'list_countries',
      'country_summary',
      'list_trips',
      'get_trip',
      'days_away',
    ];
    for (const example of EXAMPLES) {
      for (const tool of toolsOf(example)) expect(tools).toContain(tool);
    }
  });

  it('are grouped in the order they first appear, and say what they cannot answer', () => {
    expect(exampleGroups().map((group) => group.name)).toEqual([
      'Collections',
      'Countries',
      'Trips and time',
    ]);
    expect(LIMITS.length).toBeGreaterThan(0);
  });
});
