import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';

import { apiGet } from './client';
import { useScan } from './scan';

interface InboxBadge {
  /** New suggestions. Trips set aside with Later aren't counted. */
  count: number;
  /** Counts again. A screen calls this after a decision. */
  refresh: () => void;
}

const InboxContext = createContext<InboxBadge>({ count: 0, refresh: () => undefined });

/** Keeps the count for the badge on the Inbox destination. */
export function InboxProvider({ children }: { children: ReactNode }) {
  const scan = useScan();
  const [count, setCount] = useState(0);
  const scanState = scan.status?.state;

  const refresh = useCallback(() => {
    apiGet('/api/inbox/count').then(
      (answer) => setCount(answer.count),
      () => {
        // The badge keeps what it had.
      },
    );
  }, []);

  // Count when the app opens, and again when a scan ends, because detection runs then.
  useEffect(() => {
    refresh();
  }, [refresh, scanState]);

  const value = useMemo(() => ({ count, refresh }), [count, refresh]);
  return <InboxContext.Provider value={value}>{children}</InboxContext.Provider>;
}

export function useInboxBadge(): InboxBadge {
  return useContext(InboxContext);
}
