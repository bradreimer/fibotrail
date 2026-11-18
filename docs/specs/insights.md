# Insights

Tracking issue: [#151](https://github.com/bradreimer/fibotrail/issues/151).

## Purpose

This spec defines the Insights screen. It shows six travel totals at a glance, and then your
collections, such as Disney parks. It replaces two tabs: Collections and Stats.

The Stats screen answered a fixed list of questions with hand-built charts. The
[MCP server](mcp-server.md) now answers those questions, and any others, in plain language. Only the
first row of the Stats screen, the six totals, stayed useful at a glance. So the totals move to the
top of the Collections screen, that screen is renamed Insights, and the Stats tab is removed.
[ADR-019](../adr/ADR-019-insights-replaces-the-stats-and-collections-tabs.md) records the decision.

Think of the Insights screen as a dashboard, and the MCP server as the person you ask when the
dashboard doesn't show the answer.

```text
Added trips ─▶ Stats library ─┬─▶ Totals ──────────┐
                              ├─▶ Collections ─────┴─▶ Insights screen
                              └─▶ MCP server
```

This spec covers the totals, the stats library they come from, the screen, and the old addresses.
Collections themselves, their data files, and their pages are in [collections.md](collections.md).

## User story

As a person who asks questions about my travel through the MCP server, I want one Insights tab
that shows my travel totals above my collections, so that the tab bar holds only what I still use.

## Goals

- The six totals and every collection are on one screen, one tap from the tab bar.
- The tab bar has three destinations: Trips, Map, and Insights.
- Every number comes from the trips you've added, and nothing from a suggestion.
- Every number uses the same rules as the trip page, the life map, and the MCP server, so the same
  question gets the same answer everywhere.
- An old link or home-screen bookmark to a Collections or Stats address still opens a page.

## Non-goals

- Continent pages, country pages, and charts. The MCP server answers those questions. The
  continents, the country pages, Records, the Fibonacci spiral of trips, and the season wheel were
  removed in [#151](https://github.com/bradreimer/fibotrail/issues/151). The git history keeps them.
- Renaming the collection data format, the `collections/` folder, or `GET /api/collections`. A
  collection is still a collection. "Insights" names the screen that shows them.
- Filters by year or by collection.
- Sharing or exporting the numbers.

## Requirements

### The stats library

- The stats library is the one shared input of the totals, the collections, and the MCP server. It
  is built on the server, once per request, from the database and `Fibotrail.Places`, and it is
  read-only. `StatsLibraryLoader` in `Fibotrail.Server/Stats/` builds it. The library itself is the
  `Fibotrail.Stats` project, as pure functions over plain data.
- It holds the added trips with their ID, name, start and end dates, days, stops, and distance, and
  for each local day of each trip, the countries of its positioned away photos.
- Only trips with the status "added" are in it. Suggested, later, and ignored trips aren't.

### What counts

- A country is visited when an added trip has a stop in it, by the rules of the
  [life map](life-map.md#what-counts-as-visited). Photos on the way don't count
  ([on the way](on-the-way.md)).
- **Trips in a country** is the number of added trips with a stop in that country.
- **Days in a country** is the number of distinct local days, across all added trips, that have at
  least one away photo with a position in that country. This is the rule of the
  [main destination](main-destination.md) applied to countries. A day with photos in two countries
  counts for both.
- A day with no positioned photo counts for the trip's days but for no country.

### Continents

- Each country belongs to one continent: Africa, Antarctica, Asia, Europe, North America, Oceania,
  or South America. The bundled Natural Earth outlines
  ([ADR-009](../adr/ADR-009-a-bundled-natural-earth-base-map.md)) have no continent, so a small
  table that maps each country `id` to a continent ships in `Fibotrail.Places`, with a test that
  every outline has a continent.
- A transcontinental country, such as Russia or Turkey, belongs to the one continent that Natural
  Earth's `CONTINENT` field gives it.

### The Insights screen

From top to bottom:

1. **A lead line, only with no added trip.** "No trips yet. Add a trip, and the numbers fill in."
   with a link to the inbox.
2. **Totals.** Six tiles: trips; countries, with the share of the bundled outlines that have an
   `id`, such as "12 / 236 · 5.1%"; cities, merged by the rule of
   [ADR-015](../adr/ADR-015-cities-are-merged-places-and-theme-parks-keep-their-own-identity.md),
   without theme parks; continents, such as "4 / 7"; days away; and distance in kilometers. They
   use the stat tile pattern of the design document, the same as the [trip page](trip-page.md).
   With no added trip, every tile reads 0.
3. **Collections.** A "Collections" heading, then one closed card for each collection, led by its
   trips, as [collections.md](collections.md#insights-screens) describes. With no collection file,
   a line says "No collections yet. A collection is a data file in the collections folder."

- While either the totals or the collections load, the screen shows a skeleton. When either fails,
  the screen shows the error state with **Try again**, which reloads both.
- The totals have a hidden heading, "Your travel in numbers", so a screen reader announces them as
  a region.

### Addresses

| Address | Page |
| --- | --- |
| `/insights` | The Insights screen |
| `/insights/{id}` | A collection |
| `/insights/{id}/resorts/{resort}` | A resort of a collection |
| `/insights/{id}/stamps` | A collection's stamp book |

- An address that starts with `/collections` opens the same page under `/insights`, so
  `/collections/disney-parks/stamps` opens `/insights/disney-parks/stamps`.
- An address that starts with `/stats` opens `/insights`.

### API

- Every route needs a session.
- `GET /api/insights/summary` returns the six totals and `outlineCountries`, the number of bundled
  outlines that have an `id`.
- The `GET /api/stats/*` routes are removed.
- The collections keep `GET /api/collections` and `GET /api/collections/{id}`.
- The OpenAPI document and the TypeScript types are updated, per
  [web-app-development.md](../knowledge/web-app-development.md).

### Navigation

- Insights is the third destination, after Trips and Map, with the bar-chart icon. Collections and
  Stats are no longer destinations. The [app shell spec](app-shell.md) is updated in the same
  change.

## Acceptance criteria

- The tab bar holds Trips, Map, and Insights. There is no Stats tab and no Collections tab.
- The Insights screen shows the six totals, then one card for each collection.
- With no added trip, the totals read 0, the lead line links to the inbox, and the collections
  still show.
- A suggested or ignored trip changes no number on the screen.
- A day with photos but no position adds to days away and to no country.
- Every outline in the bundled data has a continent, and a test fails if one doesn't.
- `/stats`, `/stats/countries/752`, `/collections`, and `/collections/{id}/stamps` open the
  matching Insights address.
- `GET /api/insights/summary` returns the totals, and every `GET /api/stats/*` route returns 404.
- The countries and days of the totals match the MCP server's `list_countries` and `days_away`.
- The screen passes the ADR-007 run-through on an iPhone.

## Open questions

1. **Should a country with no positioned photos on a trip still count that trip's days?** The
   default is no, because a day can't be placed without a position.
