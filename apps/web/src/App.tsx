import { Navigate, Route, Routes } from 'react-router-dom';

import { ScanProvider } from './api/scan';
import { SessionProvider } from './api/session';
import { InboxScreen } from './screens/InboxScreen';
import { SettingsScreen } from './screens/SettingsScreen';
import { TripsScreen } from './screens/TripsScreen';
import { AppShell } from './shell/AppShell';

export function App() {
  return (
    <SessionProvider>
      <ScanProvider>
        <Routes>
          <Route element={<AppShell />}>
            <Route index element={<Navigate to="/trips" replace />} />
            <Route path="/trips" element={<TripsScreen />} />
            <Route path="/inbox" element={<InboxScreen />} />
            <Route path="/settings" element={<SettingsScreen />} />
            <Route path="*" element={<Navigate to="/trips" replace />} />
          </Route>
        </Routes>
      </ScanProvider>
    </SessionProvider>
  );
}
