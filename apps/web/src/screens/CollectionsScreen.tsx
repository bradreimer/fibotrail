import { useState } from 'react';

import { useApi } from '../api/useApi';
import { CollectionBody } from '../collections/CollectionBody';
import { CollapsibleSection } from '../map/CollapsibleSection';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { tripsText } from './CollectionScreen';

/** The collections: one card for each, led by the trips taken to its places. */
export function CollectionsScreen() {
  const collections = useApi('/api/collections');
  const [open, setOpen] = useState<ReadonlySet<string>>(new Set());

  if (collections.loading && !collections.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading your collections</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (collections.error)
    return <ErrorState message={collections.error} onRetry={collections.reload} />;
  if (!collections.data) return null;

  if (collections.data.length === 0) {
    return (
      <EmptyState
        title="No collections yet"
        message="No collections yet. A collection is a data file in the collections folder."
      />
    );
  }

  const noTrips = collections.data.every((item) => item.trips === 0);
  return (
    <div className="stats-screen">
      {noTrips && <p className="stats-page__lead">Add a trip to start your stamp book.</p>}
      <div className="collections">
        {collections.data.map((item) => (
          <CollapsibleSection
            key={item.id}
            id={`collection-${item.id}`}
            title={item.name}
            count={tripsText(item.trips)}
            open={open.has(item.id)}
            onToggle={(isOpen) =>
              setOpen((before) => {
                const next = new Set(before);
                if (isOpen) next.add(item.id);
                else next.delete(item.id);
                return next;
              })
            }
          >
            {open.has(item.id) && <CollectionBody id={item.id} />}
          </CollapsibleSection>
        ))}
      </div>
    </div>
  );
}
