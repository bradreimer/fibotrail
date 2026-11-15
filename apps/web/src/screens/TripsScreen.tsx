import { Link } from 'react-router-dom';

import { EmptyState } from '../states/EmptyState';

export function TripsScreen() {
  return (
    <EmptyState
      title="No trips yet"
      message="Trips you add from the inbox appear here."
      action={
        <Link className="button" to="/inbox">
          Open the inbox
        </Link>
      }
    />
  );
}
