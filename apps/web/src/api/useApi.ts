import { useCallback, useEffect, useState } from 'react';

import { ApiError, apiGet, type GetResponse } from './client';
import type { paths } from './schema';

interface Loaded<T> {
  /** The answer, or null while it loads or after it failed. */
  data: T | null;
  /** Why the request failed, in plain words. */
  error: string | null;
  /** The status of a failed answer, such as 404, or 0 when the server couldn't be reached. */
  errorStatus: number | null;
  loading: boolean;
  /** Asks again. A screen calls this from its Try again button. */
  reload: () => void;
}

/**
 * Reads one API route when the screen opens, and again when the address or `refreshKey` changes.
 * It gives a screen what the loading, error, and loaded states need.
 */
export function useApi<P extends keyof paths & string>(
  path: P | null,
  actualPath?: string,
  refreshKey?: unknown,
): Loaded<GetResponse<P>> {
  const [state, setState] = useState<{
    data: GetResponse<P> | null;
    error: string | null;
    errorStatus: number | null;
  }>({ data: null, error: null, errorStatus: null });
  const [loadedFor, setLoadedFor] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);
  const address = path === null ? null : (actualPath ?? path);

  useEffect(() => {
    if (address === null) return;
    let current = true;
    apiGet(address as P)
      .then((data) => {
        if (current) {
          setState({ data, error: null, errorStatus: null });
          setLoadedFor(address);
        }
      })
      .catch((error: unknown) => {
        if (current) {
          setState({
            data: null,
            error: error instanceof Error ? error.message : 'Something went wrong. Try again.',
            errorStatus: error instanceof ApiError ? error.status : null,
          });
          setLoadedFor(address);
        }
      });
    return () => {
      current = false;
    };
  }, [address, attempt, refreshKey]);

  const reload = useCallback(() => {
    setLoadedFor(null);
    setAttempt((count) => count + 1);
  }, []);

  // An answer for another address is never shown: a new address starts in the loading state.
  const fresh = loadedFor === address;
  return {
    data: fresh ? state.data : null,
    error: fresh ? state.error : null,
    errorStatus: fresh ? state.errorStatus : null,
    loading: address !== null && !fresh,
    reload,
  };
}
