import type { paths } from './schema';

/** The answer type of a GET route, taken from the server's OpenAPI document. */
export type GetResponse<P extends keyof paths> = paths[P] extends {
  get: { responses: { 200: { content: { 'application/json': infer T } } } };
}
  ? T
  : never;

/** A request that the server answered with an error, or couldn't answer at all. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

export const SIGN_IN_PATH = '/sign-in';

/** Sends the browser to the sign-in screen. A test replaces this. */
export const navigation = {
  goToSignIn(): void {
    window.location.assign(SIGN_IN_PATH);
  },
};

async function send(path: string, init?: RequestInit): Promise<Response> {
  let response: Response;
  try {
    response = await fetch(path, {
      ...init,
      credentials: 'same-origin',
      headers: { Accept: 'application/json', ...init?.headers },
    });
  } catch {
    throw new ApiError(0, "Can't reach Fibotrail. Check your connection, then try again.");
  }
  if (response.status === 401) {
    // The session ended. The sign-in screen is the only way forward.
    navigation.goToSignIn();
    throw new ApiError(401, 'Sign in to continue.');
  }
  if (!response.ok) {
    throw new ApiError(response.status, await problemMessage(response));
  }
  return response;
}

async function problemMessage(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { detail?: string; title?: string };
    if (problem.detail ?? problem.title) return (problem.detail ?? problem.title)!;
  } catch {
    // The answer had no readable body.
  }
  return "Something went wrong, and Fibotrail couldn't finish that. Try again.";
}

/** Reads one API route. The path and the answer type come from the OpenAPI document. */
export async function apiGet<P extends keyof paths & string>(
  path: P,
  query?: string,
): Promise<GetResponse<P>> {
  const response = await send(query ? `${path}?${query}` : path);
  return (await response.json()) as GetResponse<P>;
}

/** Sends a change. Every change carries the anti-forgery token of the session. */
export async function apiSend<T = void>(
  method: 'POST' | 'PUT' | 'DELETE',
  path: string,
  csrfToken: string,
  body?: unknown,
): Promise<T> {
  const response = await send(path, {
    method,
    headers: {
      'X-CSRF-Token': csrfToken,
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  return (response.status === 204 ? undefined : await response.json()) as T;
}

/**
 * Sends a file as the raw request body, with its name in the `name` query parameter. Used for GPS
 * trails, which the server keeps byte for byte. See docs/specs/gps-trails.md.
 */
export async function apiUpload<T>(path: string, csrfToken: string, file: File): Promise<T> {
  const response = await send(`${path}?name=${encodeURIComponent(file.name)}`, {
    method: 'POST',
    headers: { 'X-CSRF-Token': csrfToken, 'Content-Type': 'application/gpx+xml' },
    body: file,
  });
  return (await response.json()) as T;
}
