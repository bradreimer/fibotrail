import { Navigate, Route, Routes, useLocation } from 'react-router-dom';

import { InboxProvider } from './api/inbox';
import { ScanProvider } from './api/scan';
import { SessionProvider } from './api/session';
import { CollectionScreen } from './screens/CollectionScreen';
import { ResortScreen } from './screens/ResortScreen';
import { StampBookScreen } from './screens/StampBookScreen';
import { InboxScreen } from './screens/InboxScreen';
import { InsightsScreen } from './screens/InsightsScreen';
import { McpScreen } from './screens/McpScreen';
import { MapScreen } from './screens/MapScreen';
import { SettingsScreen } from './screens/SettingsScreen';
import { TripScreen } from './screens/TripScreen';
import { TripsScreen } from './screens/TripsScreen';
import { AppShell } from './shell/AppShell';

/**
 * The Collections screens moved to Insights, and the Stats screen was removed, in #151. An old link
 * or home-screen bookmark to /collections/... opens the same page under /insights/....
 */
function MovedToInsights() {
  const { pathname, search } = useLocation();
  return <Navigate to={`${pathname.replace(/^\/collections/, '/insights')}${search}`} replace />;
}

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
              <Route path="/insights" element={<InsightsScreen />} />
              <Route path="/insights/:id" element={<CollectionScreen />} />
              <Route path="/insights/:id/resorts/:resort" element={<ResortScreen />} />
              <Route path="/insights/:id/stamps" element={<StampBookScreen />} />
              <Route path="/collections/*" element={<MovedToInsights />} />
              <Route path="/stats/*" element={<Navigate to="/insights" replace />} />
              <Route path="/inbox" element={<InboxScreen />} />
              <Route path="/mcp-setup" element={<McpScreen />} />
              <Route path="/settings" element={<SettingsScreen />} />
              <Route path="*" element={<Navigate to="/trips" replace />} />
            </Route>
          </Routes>
        </InboxProvider>
      </ScanProvider>
    </SessionProvider>
  );
}
