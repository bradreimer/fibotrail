import type { ReactNode } from 'react';

interface EmptyStateProps {
  title: string;
  message: string;
  /** The one next step: a link or a button. */
  action?: ReactNode;
}

/** The empty state: a message and one next step. */
export function EmptyState({ title, message, action }: EmptyStateProps) {
  return (
    <section className="state">
      <h2 className="state__title">{title}</h2>
      <p>{message}</p>
      {action}
    </section>
  );
}
