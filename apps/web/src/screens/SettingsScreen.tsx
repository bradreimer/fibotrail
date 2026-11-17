import { useState } from 'react';

import { useScan, type ScanStatus } from '../api/scan';
import { useSession } from '../api/session';
import { useApi } from '../api/useApi';
import { HomeSetup, type SetHomeResponse } from '../home/HomeSetup';
import { Skeleton } from '../states/Skeleton';
import { foundText } from './InboxScreen';

/** A plain link, not the Buy Me a Coffee widget script, so the Content-Security-Policy stays 'self'. */
const SUPPORT_URL = 'https://www.buymeacoffee.com/bradreimer';

const count = new Intl.NumberFormat('en-US');
const dateTime = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' });

/** The time and result of the last scan, as one sentence. */
export function lastScanText(status: ScanStatus): string {
  const photos = `${count.format(status.photoCount)} ${status.photoCount === 1 ? 'photo' : 'photos'}`;
  const when = status.finishedAt ? dateTime.format(new Date(status.finishedAt)) : null;
  switch (status.state) {
    case 'never':
      return 'Fibotrail hasn’t scanned your library yet.';
    case 'running':
      return `A scan is running. Fibotrail knows ${photos} so far.`;
    case 'failed':
      return `The last scan stopped on ${when}. Fibotrail knows ${photos} so far.`;
    default:
      return `The last scan finished on ${when}. Fibotrail knows ${photos}.`;
  }
}

export function SettingsScreen() {
  const session = useSession();
  const scan = useScan();
  const immich = useApi('/api/immich');
  const home = useApi('/api/home', undefined, scan.status?.state);
  const [changingHome, setChangingHome] = useState(false);
  const [homeResult, setHomeResult] = useState<SetHomeResponse | null>(null);

  return (
    <div className="settings">
      <section className="settings__section" aria-labelledby="settings-immich">
        <h2 className="state__title" id="settings-immich">
          Immich
        </h2>
        {immich.loading && <Skeleton count={1} label="Checking the connection to Immich" />}
        {immich.error && (
          <p className="settings__status" role="alert">
            <span className="settings__dot settings__dot--off" aria-hidden="true" />
            {immich.error}
          </p>
        )}
        {immich.data && (
          <p className="settings__status" role="status">
            <span
              className={`settings__dot${immich.data.connected ? '' : ' settings__dot--off'}`}
              aria-hidden="true"
            />
            {immich.data.connected
              ? `Connected to Immich ${immich.data.version}.`
              : immich.data.message}
          </p>
        )}
        {immich.data?.warning && <p className="settings__hint">{immich.data.warning}</p>}
        {(immich.error || immich.data?.connected === false) && (
          <button type="button" className="button button--secondary" onClick={immich.reload}>
            Check again
          </button>
        )}
        <p className="settings__hint">
          Fibotrail only reads from Immich. It never changes your photos.
        </p>
      </section>

      <section className="settings__section" aria-labelledby="settings-scan">
        <h2 className="state__title" id="settings-scan">
          Library scan
        </h2>
        {scan.status ? (
          <p>{lastScanText(scan.status)}</p>
        ) : (
          <Skeleton count={1} label="Loading the scan status" />
        )}
        <button
          type="button"
          className="button"
          disabled={!session || !scan.status || scan.status.state === 'running'}
          onClick={() => void scan.start()}
        >
          Scan now
        </button>
        {scan.startProblem && <p role="alert">{scan.startProblem}</p>}
        <p className="settings__hint">
          The first scan reads your whole library. Later scans read only what changed.
        </p>
      </section>

      <section className="settings__section" aria-labelledby="settings-home">
        <h2 className="state__title" id="settings-home">
          Home location
        </h2>
        {home.loading && !home.data && <Skeleton count={1} label="Loading your home location" />}
        {home.error && <p role="alert">{home.error}</p>}
        {home.data && !changingHome && (
          <>
            <p>
              {home.data.confirmed
                ? `Home is ${home.data.confirmed.name}.`
                : home.data.photoCount === 0
                  ? 'Fibotrail suggests a home location after the first scan.'
                  : 'No home location is confirmed yet.'}
            </p>
            {homeResult && <p role="status">{foundText(homeResult)}</p>}
            {home.data.photoCount > 0 && (
              <button
                type="button"
                className="button button--secondary"
                onClick={() => setChangingHome(true)}
              >
                {home.data.confirmed ? 'Change' : 'Choose a home location'}
              </button>
            )}
          </>
        )}
        {changingHome && (
          <HomeSetup
            onConfirmed={(result) => {
              setHomeResult(result);
              setChangingHome(false);
              home.reload();
            }}
          />
        )}
        <p className="settings__hint">
          Fibotrail finds trips by how far from home your photos were taken. A new home location
          looks for every trip again, and your choices stay.
        </p>
      </section>

      <section className="settings__section" aria-labelledby="settings-support">
        <h2 className="state__title" id="settings-support">
          Support
        </h2>
        <a
          className="button button--secondary"
          href={SUPPORT_URL}
          target="_blank"
          rel="noopener noreferrer"
        >
          Give Fibs and Hermann a treat
        </a>
        <p className="settings__hint">
          Opens Buy Me a Coffee in a new tab. Fibotrail sends nothing about your library.
        </p>
      </section>

      <section className="settings__section" aria-labelledby="settings-account">
        <h2 className="state__title" id="settings-account">
          Account
        </h2>
        {session ? (
          // A plain form post: the server ends the session and answers with the sign-in screen.
          <form method="post" action="/sign-out">
            <input type="hidden" name="csrf" value={session.csrfToken} />
            <button type="submit" className="button button--secondary">
              Sign out
            </button>
          </form>
        ) : (
          <Skeleton count={1} label="Loading your account" />
        )}
        <p className="settings__hint">
          Signing out ends your session on this device. Your trips stay as they are.
        </p>
      </section>
    </div>
  );
}
