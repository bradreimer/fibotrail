# ADR-019: Insights replaces the Stats and Collections tabs

## Status

Accepted

## Context

The tab bar had four destinations: Trips, Map, Collections, and Stats. The Stats screen was a host
for self-contained views, per
[ADR-014](ADR-014-stats-views-are-self-contained-modules.md): six summary tiles, a row of
continents with country pages, Records, a Fibonacci spiral of trips, and a season wheel on each
country page. Each new question meant a new view.

The MCP server ([ADR-018](ADR-018-an-mcp-server-answers-questions-with-coarse-read-only-data.md))
now answers those questions, and any others the stats library can support, in plain language. In
use, only the first row of the Stats screen, the six totals, stayed useful at a glance. The rest of
the tab duplicated what the MCP server answers, and cost code, tests, and a tab.

## Decision

- One **Insights** tab replaces the Collections and Stats tabs. It shows the six totals, then the
  collections. The tab bar holds Trips, Map, and Insights. See
  [insights.md](../specs/insights.md).
- The Stats screen, its continent and country pages, and the Records, Fibonacci spiral, and season
  wheel views are removed, on the web and on the server, with their specs and tests. The view
  registry and view slot of ADR-014 go with them, because no view is left.
- The stats library stays. The totals, the collections, and the MCP server all read it, so the
  same question still gets the same answer everywhere. `StatsLibraryLoader` builds it on the
  server.
- The totals move from `GET /api/stats/summary` to `GET /api/insights/summary`. Every other
  `GET /api/stats/*` route is removed.
- "Collection" stays the name of the data concept: a YAML file of places in `collections/`, per
  [ADR-003](ADR-003-collections-are-data-files.md), served by `GET /api/collections`. "Insights"
  names the screen that shows totals and collections. The collection pages move from
  `/collections/...` to `/insights/...`, and the old addresses redirect.

## Alternatives considered

- **Keep the Stats tab and add a Collections section to it.** It keeps the views that the MCP
  server made redundant, and still needs four tabs or a hidden one.
- **Rename everything named "collection" to "insight".** A collection of Disney parks isn't an
  insight. The data files, the folder, and the API would read worse, and the public file format of
  ADR-003 would change for no gain.
- **Keep the views' server routes for later.** Routes with no caller are dead code that still
  needs tests. The git history keeps them, as it did for the five views removed in
  [#111](https://github.com/bradreimer/fibotrail/issues/111).

## Consequences

- The tab bar has three destinations, which leaves room for one more without crowding a phone.
- A question beyond the six totals is asked through the MCP server, not answered by a chart.
  Someone without an MCP client loses the continent and country breakdowns.
- ADR-014 is superseded. A new chart would need its own decision about where it lives.
- Old `/stats` and `/collections` links and home-screen bookmarks still open a page.
