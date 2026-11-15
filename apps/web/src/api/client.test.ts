import { afterEach, describe, expect, it, vi } from 'vitest';

import { ApiError, apiGet, apiSend, navigation } from './client';

afterEach(() => vi.restoreAllMocks());

function answer(status: number, body?: unknown) {
  const fetchMock = vi.fn<typeof fetch>(
    async () => new Response(body === undefined ? null : JSON.stringify(body), { status }),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('the API client', () => {
  it('returns the JSON answer of a route', async () => {
    answer(200, { csrfToken: 'abc' });
    await expect(apiGet('/api/session')).resolves.toEqual({ csrfToken: 'abc' });
  });

  it('opens the sign-in screen when the session has ended', async () => {
    answer(401);
    const goToSignIn = vi.spyOn(navigation, 'goToSignIn').mockImplementation(() => undefined);
    await expect(apiGet('/api/session')).rejects.toBeInstanceOf(ApiError);
    expect(goToSignIn).toHaveBeenCalledOnce();
  });

  it('reports the plain message the server sent, never a status code alone', async () => {
    answer(502, { detail: "Can't reach Immich. Check IMMICH_URL in your .env file." });
    await expect(apiGet('/api/session')).rejects.toThrow("Can't reach Immich.");
  });

  it('says so in plain words when the server is unreachable', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));
    await expect(apiGet('/api/session')).rejects.toThrow("Can't reach Fibotrail.");
  });

  it('sends the anti-forgery token with every change', async () => {
    const fetchMock = answer(204);
    await apiSend('POST', '/api/scan', 'token-1');
    const init = fetchMock.mock.calls[0]![1]!;
    expect(init.method).toBe('POST');
    expect((init.headers as Record<string, string>)['X-CSRF-Token']).toBe('token-1');
  });
});
