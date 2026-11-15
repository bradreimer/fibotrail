import { useEffect, useRef } from 'react';
import { Outlet, useLocation } from 'react-router-dom';

import { useScan } from '../api/scan';
import { ScanBanner, ScanFailedBanner } from '../states/ScanBanner';
import { destinationFor } from './destinations';
import { InstallHint } from './InstallHint';
import { Navigation } from './Navigation';
import { useNavigationMode } from './useNavigationMode';
import { useScrollMemory } from './useScrollMemory';

/**
 * The frame around every screen: a fixed header, the navigation, and one content area that
 * scrolls. See docs/specs/app-shell.md.
 */
export function AppShell() {
  const mode = useNavigationMode();
  const { pathname } = useLocation();
  const scan = useScan();
  const main = useRef<HTMLElement>(null);
  useScrollMemory(main);

  const title = destinationFor(pathname)?.label ?? 'Fibotrail';
  useEffect(() => {
    document.title = title === 'Fibotrail' ? title : `${title} · Fibotrail`;
  }, [title]);

  return (
    <div className="app" data-nav={mode}>
      <header className="app__header">
        <h1 className="app__title">{title}</h1>
      </header>
      <div className="app__banner">
        {scan.status?.state === 'running' && (
          <ScanBanner done={scan.status.done} total={scan.status.total} />
        )}
        {scan.status?.state === 'failed' && scan.status.message && (
          <ScanFailedBanner message={scan.status.message} onRetry={() => void scan.start()} />
        )}
      </div>
      <main className="app__main" ref={main} tabIndex={-1}>
        <div className="screen" key={pathname}>
          <Outlet />
        </div>
      </main>
      <Navigation mode={mode} />
      <InstallHint />
    </div>
  );
}
