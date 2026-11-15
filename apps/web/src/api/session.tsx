import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';

import { apiGet, type GetResponse } from './client';

export type Session = GetResponse<'/api/session'>;

const SessionContext = createContext<Session | null>(null);

/** Loads the session once. With no session, the API client opens the sign-in screen. */
export function SessionProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);

  useEffect(() => {
    let current = true;
    apiGet('/api/session')
      .then((loaded) => {
        if (current) setSession(loaded);
      })
      .catch(() => {
        // The shell still shows. Each screen reports its own failed request.
      });
    return () => {
      current = false;
    };
  }, []);

  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}

/** The session, or null while it loads. */
export function useSession(): Session | null {
  return useContext(SessionContext);
}
