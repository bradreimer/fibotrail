import { Link } from 'react-router-dom';

interface ErrorStateProps {
  /** A plain explanation with the next step, never an error code alone. */
  message: string;
  onRetry?: () => void;
  /** Offers Settings when that's where the fix is. */
  showSettings?: boolean;
}

/** The error state: a plain explanation and a button to retry or open Settings. */
export function ErrorState({ message, onRetry, showSettings = false }: ErrorStateProps) {
  return (
    <section className="state state--error" role="alert">
      <p>{message}</p>
      {onRetry && (
        <button type="button" className="button" onClick={onRetry}>
          Try again
        </button>
      )}
      {showSettings && (
        <Link className={onRetry ? 'button button--secondary' : 'button'} to="/settings">
          Open Settings
        </Link>
      )}
    </section>
  );
}
