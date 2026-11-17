import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';

import { apiGet, apiSend, type GetResponse } from './client';
import { useSession } from './session';

export type ScanStatus = GetResponse<'/api/scan'>;

/** How often the banner asks for progress while a scan runs. */
export const SCAN_POLL_MS = 2000;

interface Scan {
  /** Where the scan stands, or null before the first answer. */
  status: ScanStatus | null;
  /** Starts a scan, or resumes one that failed. */
  start: () => Promise<void>;
  /** Rereads every photo from Immich, so it sees a location edited there. */
  rescan: () => Promise<void>;
  /** Why the scan couldn't be started. */
  startProblem: string | null;
}

const ScanContext = createContext<Scan>({
  status: null,
  start: async () => undefined,
  rescan: async () => undefined,
  startProblem: null,
});

/** Follows the scan on the server. It asks again every two seconds while a scan runs. */
export function ScanProvider({ children }: { children: ReactNode }) {
  const session = useSession();
  const [status, setStatus] = useState<ScanStatus | null>(null);
  const [startProblem, setStartProblem] = useState<string | null>(null);
  const running = status?.state === 'running';

  const refresh = useCallback(
    () =>
      apiGet('/api/scan').then(setStatus, () => {
        // The banner keeps what it had. The next poll, or the next screen, tries again.
      }),
    [],
  );

  // Ask once when the app opens, then every two seconds for as long as a scan runs.
  useEffect(() => {
    void refresh();
    if (!running) return;
    const timer = setInterval(() => void refresh(), SCAN_POLL_MS);
    return () => clearInterval(timer);
  }, [running, refresh]);

  const begin = useCallback(
    async (path: '/api/scan' | '/api/rescan') => {
      if (!session) return;
      setStartProblem(null);
      try {
        setStatus(await apiSend<ScanStatus>('POST', path, session.csrfToken));
      } catch (error) {
        setStartProblem(error instanceof Error ? error.message : 'The scan could not start.');
        await refresh();
      }
    },
    [session, refresh],
  );
  const start = useCallback(() => begin('/api/scan'), [begin]);
  const rescan = useCallback(() => begin('/api/rescan'), [begin]);

  const value = useMemo(
    () => ({ status, start, rescan, startProblem }),
    [status, start, rescan, startProblem],
  );
  return <ScanContext.Provider value={value}>{children}</ScanContext.Provider>;
}

export function useScan(): Scan {
  return useContext(ScanContext);
}
