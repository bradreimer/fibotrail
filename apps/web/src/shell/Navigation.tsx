import { NavLink } from 'react-router-dom';

import { DESTINATIONS } from './destinations';
import type { NavigationMode } from './useNavigationMode';

/** The tab bar on a phone, and the sidebar on wider screens. Each item has an icon and a label. */
export function Navigation({ mode }: { mode: NavigationMode }) {
  return (
    <nav className="nav" aria-label="Main">
      <ul className="nav__list">
        {DESTINATIONS.map((destination) => (
          <li key={destination.path} className="nav__item">
            <NavLink to={destination.path} className="nav__link" draggable={false}>
              {destination.icon}
              {/* The rail shows icons only. The label stays for screen readers. */}
              <span className={mode === 'rail' ? 'visually-hidden' : undefined}>
                {destination.label}
              </span>
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
