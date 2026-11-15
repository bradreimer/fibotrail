import { useScan, type ScanStatus } from '../api/scan';
import { useSession } from '../api/session';
import { useApi } from '../api/useApi';
import { Skeleton } from '../states/Skeleton';

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
