import { vi } from 'vitest';

type Answer = unknown | ((init?: RequestInit) => unknown);

/** A request the app made, as the stand-in server saw it. */
export interface SeenRequest {
  method: string;
  path: string;
  headers: Record<string, string>;
  body: unknown;
}

/** An answer with a status other than 200. */
export function status(code: number, body?: unknown) {
  return { __status: code, body };
}

const DEFAULTS: Record<string, Answer> = {
  'GET /api/session': { csrfToken: 'token-1' },
  'GET /api/scan': {
    state: 'never',
    done: 0,
    total: null,
    message: null,
    finishedAt: null,
    photoCount: 0,
  },
  'GET /api/home': { confirmed: null, suggestion: null, hasPositions: false, photoCount: 0 },
  'GET /api/inbox': { suggestions: [], ignored: [], count: 0 },
  'GET /api/inbox/count': { count: 0 },
  'GET /api/mcp/status': { enabled: false, tools: [] },
  'GET /api/immich': { connected: true, version: '3.4.1', message: null, warning: null },
};

/**
 * Stands in for the server. Each route is "METHOD /path" with a fixed answer or a function. A
 * route that isn't listed answers 404, so a test notices a request it didn't expect.
 */
export function fakeServer(routes: Record<string, Answer> = {}) {
  const table = { ...DEFAULTS, ...routes };
  const requests: SeenRequest[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    const path = String(input);
    requests.push({
      method,
      path,
      headers: (init?.headers ?? {}) as Record<string, string>,
      body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined,
    });
    const route = table[`${method} ${path}`] ?? table[`${method} ${path.split('?')[0]}`];
    if (route === undefined) {
      return new Response(JSON.stringify({ detail: `No route for ${method} ${path}` }), {
        status: 404,
      });
    }
    const answer =
      typeof route === 'function' ? (route as (i?: RequestInit) => unknown)(init) : route;
    if (answer && typeof answer === 'object' && '__status' in answer) {
      const { __status, body } = answer as { __status: number; body?: unknown };
      return new Response(body === undefined ? null : JSON.stringify(body), { status: __status });
    }
    return new Response(JSON.stringify(answer), { status: 200 });
  });
  vi.stubGlobal('fetch', fetchMock);
  return {
    requests,
    /** Changes the answer of one route, as the server's state changes. */
    set(route: string, answer: Answer) {
      table[route] = answer;
    },
    sent(method: string, path: string) {
      return requests.filter((request) => request.method === method && request.path === path);
    },
  };
}
