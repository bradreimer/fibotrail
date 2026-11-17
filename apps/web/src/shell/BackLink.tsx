import { Link } from 'react-router-dom';

/** A link to the parent screen, with a back arrow. It's the first thing on a detail page. */
export function BackLink({ to, children }: { to: string; children: string }) {
  return (
    <Link className="back-link" to={to}>
      <svg className="back-link__arrow" viewBox="0 0 12 20" aria-hidden="true" focusable="false">
        <path
          d="M10 2 2 10l8 8"
          fill="none"
          stroke="currentColor"
          strokeWidth="2.5"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </svg>
      {children}
    </Link>
  );
}
