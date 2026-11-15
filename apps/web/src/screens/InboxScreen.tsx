import { useState } from 'react';
import { Link } from 'react-router-dom';

import { useScan } from '../api/scan';
import { useApi } from '../api/useApi';
import { HomeSetup, type SetHomeResponse } from '../home/HomeSetup';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { Skeleton } from '../states/Skeleton';

const count = new Intl.NumberFormat('en-US');

/** What detection found, in the words of the voice and tone table. */
export function foundText(result: SetHomeResponse): string {
  if (result.trips === 0) {
    return `Home is ${result.home.name}. Fibotrail found no trips in your photos yet.`;
  }
  const trips = `${count.format(result.trips)} possible ${result.trips === 1 ? 'trip' : 'trips'}`;
  return `Home is ${result.home.name}. We found ${trips}.`;
}

export function InboxScreen() {
  const scan = useScan();
  // Ask again when a scan ends, because the first scan is what makes a home suggestion possible.
  const home = useApi('/api/home', undefined, scan.status?.state);
  const [confirmed, setConfirmed] = useState<SetHomeResponse | null>(null);

  if (home.loading && !home.data) return <Skeleton count={3} label="Loading your inbox" />;
  if (home.error) return <ErrorState message={home.error} onRetry={home.reload} />;
  if (!home.data) return null;

  if (home.data.photoCount === 0) {
    return (
      <EmptyState
        title="No suggestions yet"
        message={
          scan.status?.state === 'running'
            ? 'Fibotrail is still scanning your library.'
            : 'Fibotrail suggests trips after it scans your Immich library.'
        }
        action={
          <Link className="button" to="/settings">
            Open Settings
          </Link>
        }
      />
    );
  }

  if (!home.data.confirmed && !confirmed) return <HomeSetup onConfirmed={setConfirmed} />;

  return (
    <EmptyState
      title="Home is set"
      message={
        confirmed
          ? foundText(confirmed)
          : `Home is ${home.data.confirmed!.name}. Fibotrail looks for trips after every scan.`
      }
      action={
        <Link className="button" to="/settings">
          Open Settings
        </Link>
      }
    />
  );
}
