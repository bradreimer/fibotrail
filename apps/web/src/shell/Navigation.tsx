import { NavLink } from 'react-router-dom';

import { useInboxBadge } from '../api/inbox';
import { countOf } from '../trips/dates';
import { DESTINATIONS } from './destinations';
import type { NavigationMode } from './useNavigationMode';

/** The tab bar on a phone, and the sidebar on wider screens. Each item has an icon and a label. */
export function Navigation({ mode }: { mode: NavigationMode }) {
  const badge = useInboxBadge();
  return (
    <nav className="nav" aria-label="Main">
      <ul className="nav__list">
        {DESTINATIONS.map((destination) => (
          <li key={destination.path} className="nav__item">
            <NavLink
              to={destination.path}
              className="nav__link"
              draggable={false}
              aria-label={
                destination.path === '/inbox' && badge.count > 0
                  ? `Inbox, ${countOf(badge.count, 'possible trip')}`
                  : undefined
              }
            >
              {destination.icon}
              {/* The rail shows icons only. The label stays for screen readers. */}
              <span className={mode === 'rail' ? 'visually-hidden' : undefined}>
                {destination.label}
              </span>
              {destination.path === '/inbox' && badge.count > 0 && (
                <span className="nav__badge" aria-hidden="true">
                  {badge.count}
                </span>
              )}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
