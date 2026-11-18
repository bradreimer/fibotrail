/** The text a person copies to connect an AI client. The token is never part of it. */

/** Shown where the token goes. Fibotrail never knows the token, so it never fills this in. */
export const TOKEN_PLACEHOLDER = 'YOUR_MCP_TOKEN';

/** The address of the MCP server, as this browser reaches Fibotrail. */
export function serverAddress(origin: string): string {
  return `${origin.replace(/\/+$/, '')}/mcp`;
}

/**
 * True when the address is one that only your own network can reach: plain HTTP, `localhost`, a
 * private IP address, or a name with no dot or a `.local` name. A client in the cloud can't use it.
 */
export function isHomeAddress(origin: string): boolean {
  let url: URL;
  try {
    url = new URL(origin);
  } catch {
    return true;
  }
  const host = url.hostname.toLowerCase();
  if (url.protocol !== 'https:') return true;
  if (host === 'localhost' || !host.includes('.') || host.endsWith('.local')) return true;
  const parts = host.split('.').map(Number);
  if (parts.length === 4 && parts.every((part) => Number.isInteger(part))) {
    const [first = -1, second = -1] = parts;
    return (
      first === 10 ||
      first === 127 ||
      (first === 192 && second === 168) ||
      (first === 172 && second >= 16 && second <= 31) ||
      (first === 169 && second === 254)
    );
  }
  return false;
}

/** Claude Code adds a server with one command. */
export function claudeCodeCommand(address: string): string {
  return `claude mcp add --transport http fibotrail ${address} --header "Authorization: Bearer ${TOKEN_PLACEHOLDER}"`;
}

/** For an app that starts a local command, such as Claude Desktop. `mcp-remote` carries the header. */
export function bridgeConfig(address: string): string {
  return JSON.stringify(
    {
      mcpServers: {
        fibotrail: {
          command: 'npx',
          args: ['mcp-remote', address, '--header', `Authorization: Bearer ${TOKEN_PLACEHOLDER}`],
        },
      },
    },
    null,
    2,
  );
}

/** For a client that takes a URL and headers. */
export function urlConfig(address: string): string {
  return JSON.stringify(
    {
      mcpServers: {
        fibotrail: {
          type: 'http',
          url: address,
          headers: { Authorization: `Bearer ${TOKEN_PLACEHOLDER}` },
        },
      },
    },
    null,
    2,
  );
}

/** The commands that turn the server on. */
export const MAKE_TOKEN_COMMAND = 'openssl rand -hex 32';
export const ENV_LINE = 'MCP_TOKEN=paste-your-token-here';
export const RESTART_COMMAND = 'docker compose up -d --force-recreate';
