import { Component, type ReactNode, Suspense } from 'react';

import { type Slot, type StatsView, VIEWS, viewsFor } from './registry';

/** Keeps one failing view from taking the screen down with it. */
class ViewBoundary extends Component<{ title: string; children: ReactNode }, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  render() {
    if (this.state.failed) {
      return (
        <p className="stats-view__error">{this.props.title} couldn't load. Reload the page.</p>
      );
    }
    return this.props.children;
  }
}

/** Renders the views registered for a slot, each under its heading and in its own boundary. */
export function ViewSlot({
  slot,
  countryId,
  views = VIEWS,
}: {
  slot: Slot;
  countryId?: string;
  views?: readonly StatsView[];
}) {
  return (
    <>
      {viewsFor(slot, views).map((view) => {
        const View = view.component;
        const headingId = `stats-view-${view.id}`;
        return (
          <section key={view.id} className="stats-view" aria-labelledby={headingId}>
            <h3 id={headingId} className="stats-view__title">
              {view.title}
            </h3>
            <ViewBoundary title={view.title}>
              <Suspense
                fallback={
                  <div role="status" aria-busy="true">
                    <span className="visually-hidden">Loading {view.title}</span>
                    <span className="skeleton skeleton--card" aria-hidden="true" />
                  </div>
                }
              >
                <View countryId={countryId} />
              </Suspense>
            </ViewBoundary>
          </section>
        );
      })}
    </>
  );
}
