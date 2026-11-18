import { useState } from 'react';
import { Link } from 'react-router-dom';

import { useApi } from '../api/useApi';
import { CollectionBody } from '../insights/CollectionBody';
import { InsightsTotals } from '../insights/InsightsTotals';
import { CollapsibleSection } from '../map/CollapsibleSection';
import { ErrorState } from '../states/ErrorState';
import { tripsText } from './CollectionScreen';

/**
 * The Insights screen: the six totals, then one card for each collection, led by the trips taken
 * to its places. See docs/specs/insights.md.
 */
export function InsightsScreen() {
  const summary = useApi('/api/insights/summary');
  const collections = useApi('/api/collections');
  const [open, setOpen] = useState<ReadonlySet<string>>(new Set());

  if ((summary.loading && !summary.data) || (collections.loading && !collections.data)) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading your insights</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  const error = summary.error ?? collections.error;
  if (error) {
    return (
      <ErrorState
        message={error}
        onRetry={() => {
          summary.reload();
          collections.reload();
        }}
      />
    );
  }
  if (!summary.data || !collections.data) return null;

  const noTrips = summary.data.trips === 0;
  return (
    <div className="insights-page">
      {noTrips && (
        <p className="insights-page__lead">
          No trips yet. Add a trip, and the numbers fill in. <Link to="/inbox">Open the inbox</Link>
        </p>
      )}
      <InsightsTotals summary={summary.data} />
      <section aria-labelledby="insights-collections" className="insights-section">
        <h2 className="insights-section__title" id="insights-collections">
          Collections
        </h2>
        {collections.data.length === 0 ? (
          <p className="insights-page__lead">
            No collections yet. A collection is a data file in the collections folder.
          </p>
        ) : (
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
        )}
      </section>
    </div>
  );
}
