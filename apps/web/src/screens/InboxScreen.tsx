import { Link } from 'react-router-dom';

import { EmptyState } from '../states/EmptyState';

export function InboxScreen() {
  return (
    <EmptyState
      title="No suggestions yet"
      message="Fibotrail suggests trips after it scans your Immich library."
      action={
        <Link className="button" to="/settings">
          Open Settings
        </Link>
      }
    />
  );
}
