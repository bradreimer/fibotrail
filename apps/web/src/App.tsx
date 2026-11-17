import { Navigate, Route, Routes } from 'react-router-dom';

import { InboxProvider } from './api/inbox';
import { ScanProvider } from './api/scan';
import { SessionProvider } from './api/session';
import { CollectionScreen } from './screens/CollectionScreen';
import { CollectionsScreen } from './screens/CollectionsScreen';
import { ResortScreen } from './screens/ResortScreen';
import { StampBookScreen } from './screens/StampBookScreen';
import { InboxScreen } from './screens/InboxScreen';
import { MapScreen } from './screens/MapScreen';
import { SettingsScreen } from './screens/SettingsScreen';
import { StatsContinentScreen } from './screens/StatsContinentScreen';
import { StatsCountryScreen } from './screens/StatsCountryScreen';
import { StatsScreen } from './screens/StatsScreen';
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
              <Route path="/collections" element={<CollectionsScreen />} />
              <Route path="/collections/:id" element={<CollectionScreen />} />
              <Route path="/collections/:id/resorts/:resort" element={<ResortScreen />} />
              <Route path="/collections/:id/stamps" element={<StampBookScreen />} />
              <Route path="/stats" element={<StatsScreen />} />
              <Route path="/stats/continents/:continent" element={<StatsContinentScreen />} />
              <Route path="/stats/countries/:id" element={<StatsCountryScreen />} />
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
