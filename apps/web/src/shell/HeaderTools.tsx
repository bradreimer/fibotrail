import { NavLink } from 'react-router-dom';

import { useInboxBadge } from '../api/inbox';
import { countOf } from '../trips/dates';
import { TOOLS } from './destinations';

/**
 * The Inbox, MCP, and Settings as icon buttons in the top bar. They're here only when the navigation is
 * a tab bar, under 600 px: the tab bar holds the screens you browse, and these are the tools. On
 * wider screens the sidebar holds all four. See ADR-007 and docs/specs/life-map.md.
 */
export function HeaderTools() {
  const badge = useInboxBadge();
  return (
    <nav className="app__tools" aria-label="Inbox, MCP, and settings">
      <ul className="app__tools-list">
        {TOOLS.map((tool) => (
          <li key={tool.path}>
            <NavLink
              to={tool.path}
              className="app__tool"
              draggable={false}
              aria-label={
                tool.path === '/inbox' && badge.count > 0
                  ? `Inbox, ${countOf(badge.count, 'possible trip')}`
                  : tool.label
              }
            >
              {tool.icon}
              {tool.path === '/inbox' && badge.count > 0 && (
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
