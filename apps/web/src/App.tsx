import { Navigate, Route, Routes } from 'react-router-dom';

import { InboxProvider } from './api/inbox';
import { ScanProvider } from './api/scan';
import { SessionProvider } from './api/session';
import { InboxScreen } from './screens/InboxScreen';
import { MapScreen } from './screens/MapScreen';
import { SettingsScreen } from './screens/SettingsScreen';
import { TripScreen } from './screens/TripScreen';
import { TripsScreen } from './screens/TripsScreen';
import { AppShell } from './shell/AppShell';

export function App() {
  return (
    <SessionProvider>
      <ScanProvider>
        <InboxProvider>
          <Routes>
            <Route element={<AppShell />}>
              <Route index element={<Navigate to="/trips" replace />} />
              <Route path="/trips" element={<TripsScreen />} />
              <Route path="/trips/:id" element={<TripScreen />} />
              <Route path="/map" element={<MapScreen />} />
              <Route path="/inbox" element={<InboxScreen />} />
              <Route path="/settings" element={<SettingsScreen />} />
              <Route path="*" element={<Navigate to="/trips" replace />} />
            </Route>
          </Routes>
        </InboxProvider>
      </ScanProvider>
    </SessionProvider>
  );
}
