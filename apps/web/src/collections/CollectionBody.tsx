import { useState } from 'react';
import { Link } from 'react-router-dom';

import { useApi } from '../api/useApi';
import type { components } from '../api/schema';
import { CollapsibleSection } from '../map/CollapsibleSection';
import { ErrorState } from '../states/ErrorState';
import { countOf } from '../trips/dates';
import { tripsText } from '../screens/CollectionScreen';
import { ParkList } from './ParkList';

type Detail = components['schemas']['CollectionDetail'];

/** One row for each resort, most trips first and then by name, with the note about shared trips. */
export function ResortList({ id, detail }: { id: string; detail: Detail }) {
  const base = `/collections/${encodeURIComponent(id)}`;
  const resorts = [...detail.resorts].sort(
    (a, b) => b.trips - a.trips || a.name.localeCompare(b.name),
  );
  return (
    <>
      <ul className="collections">
        {resorts.map((resort) => (
          <li key={resort.name}>
            <Link
              className="collection-card"
              to={`${base}/resorts/${encodeURIComponent(resort.name)}`}
            >
              <span className="collection-card__name">{resort.name}</span>
              <span className="collection-card__count">{tripsText(resort.trips)}</span>
            </Link>
          </li>
        ))}
      </ul>
      {resorts.length > 1 && (
        <p className="stats-page__note">A trip to two resorts counts once for each.</p>
      )}
    </>
  );
}

/** The resorts of an open collection. Each opens to its parks, a second level. */
function ResortSections({ id, detail }: { id: string; detail: Detail }) {
  const [open, setOpen] = useState<ReadonlySet<string>>(new Set());
  const resorts = [...detail.resorts].sort(
    (a, b) => b.trips - a.trips || a.name.localeCompare(b.name),
  );
  return (
    <>
      <div className="collections">
        {resorts.map((resort) => (
          <CollapsibleSection
            key={resort.name}
            id={`resort-${id}-${resort.name.replace(/\W+/g, '-')}`}
            level={4}
            title={resort.name}
            count={tripsText(resort.trips)}
            open={open.has(resort.name)}
            onToggle={(isOpen) =>
              setOpen((before) => {
                const next = new Set(before);
                if (isOpen) next.add(resort.name);
                else next.delete(resort.name);
                return next;
              })
            }
          >
            {open.has(resort.name) && <ParkList places={detail.places} resort={resort.name} />}
          </CollapsibleSection>
        ))}
      </div>
      {resorts.length > 1 && (
        <p className="stats-page__note">A trip to two resorts counts once for each.</p>
      )}
    </>
  );
}

/** What an open collection shows: its totals, its resorts, and the way to the stamp book. */
export function CollectionBody({ id }: { id: string }) {
  const collection = useApi('/api/collections/{id}', `/api/collections/${encodeURIComponent(id)}`);

  if (collection.loading && !collection.data) {
    return (
      <div role="status" aria-busy="true">
        <span className="visually-hidden">Loading the collection</span>
        <span className="skeleton skeleton--card" aria-hidden="true" />
      </div>
    );
  }
  if (collection.error)
    return <ErrorState message={collection.error} onRetry={collection.reload} />;
  const detail = collection.data;
  if (!detail) return null;

  const { summary } = detail;
  return (
    <div className="collection-body">
      <p className="stats-page__lead">
        {summary.visited} / {summary.total} parks · {countOf(summary.totalDays, 'day')} ·{' '}
        {countOf(summary.trips, 'trip')}
      </p>
      <ResortSections id={id} detail={detail} />
      <p>
        <Link className="button" to={`/collections/${encodeURIComponent(id)}/stamps`}>
          Stamp book
        </Link>
      </p>
    </div>
  );
}
