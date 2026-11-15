import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';

import { apiSend } from '../api/client';
import { useInboxBadge } from '../api/inbox';
import { useScan } from '../api/scan';
import type { components } from '../api/schema';
import { useSession } from '../api/session';
import { useApi } from '../api/useApi';
import { HomeSetup, type SetHomeResponse } from '../home/HomeSetup';
import { AddTripSheet } from '../inbox/AddTripSheet';
import { SuggestionCard, type Suggestion } from '../inbox/SuggestionCard';
import { EmptyState } from '../states/EmptyState';
import { ErrorState } from '../states/ErrorState';
import { Skeleton } from '../states/Skeleton';
import { countOf } from '../trips/dates';

type DecisionRequest = components['schemas']['DecisionRequest'];

/** What detection found, in the words of the voice and tone table. */
export function foundText(result: SetHomeResponse): string {
  return result.trips === 0
    ? `Home is ${result.home.name}. Fibotrail found no trips in your photos yet.`
    : `Home is ${result.home.name}. We found ${countOf(result.trips, 'possible trip')}.`;
}

export function InboxScreen() {
  const session = useSession();
  const scan = useScan();
  const badge = useInboxBadge();
  const [version, setVersion] = useState(0);
  const scanState = scan.status?.state;
  // Ask again when a scan ends, because the first scan is what makes a home suggestion possible.
  const home = useApi('/api/home', undefined, `${scanState}:${version}`);
  const inbox = useApi('/api/inbox', undefined, `${scanState}:${version}`);
  const [tab, setTab] = useState<'inbox' | 'ignored'>('inbox');
  const [adding, setAdding] = useState<Suggestion | null>(null);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const focusAt = useRef<number | null>(null);
  const headings = useRef<(HTMLHeadingElement | null)[]>([]);
  const emptyHeading = useRef<HTMLDivElement>(null);

  const cards = tab === 'inbox' ? inbox.data?.suggestions : inbox.data?.ignored;

  // After an action, the next card takes the place of the old one, and focus moves to it.
  useEffect(() => {
    if (focusAt.current === null || !cards) return;
    const target =
      headings.current[Math.min(focusAt.current, cards.length - 1)] ?? emptyHeading.current;
    target?.focus();
    focusAt.current = null;
  }, [cards]);

  async function decide(suggestion: Suggestion, request: DecisionRequest, index: number) {
    if (!session) return false;
    setBusy(true);
    setProblem(null);
    try {
      await apiSend('POST', `/api/trips/${suggestion.id}/decision`, session.csrfToken, request);
      focusAt.current = index;
      setVersion((current) => current + 1);
      badge.refresh();
      return true;
    } catch (error) {
      setProblem(error instanceof Error ? error.message : 'That didn’t work. Try again.');
      return false;
    } finally {
      setBusy(false);
    }
  }

  if ((home.loading && !home.data) || (inbox.loading && !inbox.data)) {
    return <Skeleton shape="card" count={2} label="Loading your inbox" />;
  }
  const error = home.error ?? inbox.error;
  if (error) {
    return (
      <ErrorState
        message={error}
        onRetry={() => {
          home.reload();
          inbox.reload();
        }}
      />
    );
  }
  if (!home.data || !inbox.data) return null;

  // Before a home location is confirmed, nothing is detected, so the home step comes first.
  if (home.data.photoCount > 0 && !home.data.confirmed) {
    return (
      <HomeSetup
        onConfirmed={() => {
          setVersion((current) => current + 1);
          badge.refresh();
        }}
      />
    );
  }

  const scanning = scanState === 'running';
  const index = cards ? cards.findIndex((card) => card.id === adding?.id) : -1;

  return (
    <div className="inbox">
      <div className="tabs" role="tablist" aria-label="Inbox">
        <button
          type="button"
          role="tab"
          id="tab-inbox"
          className="tabs__tab"
          aria-selected={tab === 'inbox'}
          aria-controls="inbox-panel"
          onClick={() => setTab('inbox')}
        >
          Suggestions
        </button>
        <button
          type="button"
          role="tab"
          id="tab-ignored"
          className="tabs__tab"
          aria-selected={tab === 'ignored'}
          aria-controls="inbox-panel"
          onClick={() => setTab('ignored')}
        >
          Ignored
        </button>
      </div>

      <div
        className="inbox__list"
        role="tabpanel"
        id="inbox-panel"
        aria-labelledby={tab === 'inbox' ? 'tab-inbox' : 'tab-ignored'}
      >
        {problem && !adding && <p role="alert">{problem}</p>}

        {cards!.length === 0 && (
          <div ref={emptyHeading} tabIndex={-1}>
            {tab === 'ignored' ? (
              <EmptyState
                title="Nothing ignored"
                message="A suggestion you mark Not a trip waits here, in case you change your mind."
              />
            ) : home.data.photoCount === 0 && scanning ? (
              <EmptyState
                title="No trips yet"
                message="No trips yet. Fibotrail is still scanning your library."
              />
            ) : (
              <EmptyState
                title="No possible trips right now"
                message={
                  home.data.photoCount === 0
                    ? 'No possible trips right now. Fibotrail suggests trips after it scans your Immich library.'
                    : 'No possible trips right now. Scan again after your next trip.'
                }
                action={
                  <button
                    type="button"
                    className="button"
                    disabled={!session || scanning}
                    onClick={() => void scan.start()}
                  >
                    Scan now
                  </button>
                }
              />
            )}
          </div>
        )}

        {cards!.map((suggestion, position) => (
          <SuggestionCard
            key={suggestion.id}
            suggestion={suggestion}
            headingRef={(element) => {
              headings.current[position] = element;
            }}
            onSwipeRight={tab === 'inbox' ? () => setAdding(suggestion) : undefined}
            onSwipeLeft={
              tab === 'inbox'
                ? () => void decide(suggestion, { action: 'ignore' }, position)
                : undefined
            }
            actions={
              tab === 'ignored' ? (
                <button
                  type="button"
                  className="button button--secondary"
                  disabled={busy}
                  onClick={() => void decide(suggestion, { action: 'restore' }, position)}
                >
                  Restore
                </button>
              ) : (
                <>
                  <button
                    type="button"
                    className="button"
                    disabled={busy}
                    onClick={() => setAdding(suggestion)}
                  >
                    Add trip
                  </button>
                  <button
                    type="button"
                    className="button button--secondary"
                    disabled={busy}
                    onClick={() => void decide(suggestion, { action: 'ignore' }, position)}
                  >
                    Not a trip
                  </button>
                  {suggestion.status === 'suggested' && (
                    <button
                      type="button"
                      className="button button--text"
                      disabled={busy}
                      onClick={() => void decide(suggestion, { action: 'later' }, position)}
                    >
                      Later
                    </button>
                  )}
                </>
              )
            }
          />
        ))}

        {tab === 'inbox' && cards!.length > 0 && (
          <p className="settings__hint">
            Added trips are in <Link to="/trips">Trips</Link>.
          </p>
        )}
      </div>

      {adding && (
        <AddTripSheet
          suggestion={adding}
          busy={busy}
          problem={problem}
          onCancel={() => {
            setAdding(null);
            setProblem(null);
          }}
          onConfirm={(name, coverPhotoId) =>
            void decide(adding, { action: 'add', name, coverPhotoId }, Math.max(index, 0)).then(
              (done) => done && setAdding(null),
            )
          }
        />
      )}
    </div>
  );
}
