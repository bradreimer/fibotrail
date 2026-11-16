# Stats screen

Tracking issue: [#54](https://github.com/bradreimer/fibotrail/issues/54).

## Purpose

This spec defines the Stats screen, the "exploration analytics" feature of the
[design document](../knowledge/design-and-branding.md#6-exploration-analytics) and part of phase 5
of the [roadmap](../roadmap.md). It turns the trips you've added into numbers and charts, so you
can relive your travel and answer questions such as these:

- How many trips have I taken?
- How many countries have I visited, and how many on each continent?
- How many times have I been to Sweden, and how many days have I spent there?

The screen works like a set of nested folders: tap a continent to see its countries, and tap a
country to see its trips and days.

The screen is a **host**. Its charts are self-contained **views**, per
[ADR-014](../adr/ADR-014-stats-views-are-self-contained-modules.md), so each one can be added or
removed on its own. This spec covers the host, the shared stats library, the drill-down, and the
first three views. Other views have their own specs:

| View | Slot | Spec | Work item |
| --- | --- | --- | --- |
| Trips per year | Stats screen | This spec | [#54](https://github.com/bradreimer/fibotrail/issues/54) |
| Trip length | Stats screen | This spec | [#54](https://github.com/bradreimer/fibotrail/issues/54) |
| Records | Stats screen | This spec | [#54](https://github.com/bradreimer/fibotrail/issues/54) |
| Season wheel | Country page | [season-wheel.md](season-wheel.md) | [#58](https://github.com/bradreimer/fibotrail/issues/58) |
| Fibonacci spiral of trips | Stats screen | [trip-spiral.md](trip-spiral.md) | [#59](https://github.com/bradreimer/fibotrail/issues/59) |
| Travel clock | Stats screen | [travel-clock.md](travel-clock.md) | [#66](https://github.com/bradreimer/fibotrail/issues/66) |
| Theme parks | Stats screen | [theme-parks-view.md](theme-parks-view.md) | [#64](https://github.com/bradreimer/fibotrail/issues/64) |
| Distance rings | Stats screen | [distance-rings.md](distance-rings.md) | [#67](https://github.com/bradreimer/fibotrail/issues/67) |

## User story

As a person with years of trips in Fibotrail, I want a Stats screen that answers questions about
my travel in numbers and charts, so that I can relive my trips and see where, when, and how often I
went.

## Goals

- Each question above is answered in at most two taps from the Stats screen.
- Every number comes from the trips you've added, and nothing from a suggestion.
- Every number uses the same rules as the trip page and the life map, so the same question gets
  the same answer on every screen.
- Adding or removing a view changes nothing outside the view, its registry line, and its spec.
- Every chart has a text alternative and never relies on color alone.

## Non-goals

- Collections such as Disney parks. They have their own spec, [collections.md](collections.md). Their
  days appear on this screen in the [Theme parks view](theme-parks-view.md).
- Regions, and a count of them. Only Canada and the USA have regions, per
  [ADR-015](../adr/ADR-015-cities-are-merged-places-and-theme-parks-keep-their-own-identity.md).
- Filters by year or by collection.
- Sharing or exporting the numbers.
- A yearly recap page and Home Assistant sensors. They come after version 1.

## Requirements

### The stats library

- The stats library is the one shared input of every view, per ADR-014. It is built on the server,
  once per request, from the database and `Fibotrail.Places`, and it is read-only.
- It holds the added trips with their ID, name, start and end dates, days, stops, and distance, and
  for each local day of each trip, the countries of its positioned away photos with their
  latitudes. It also holds the home location, which no route returns.
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
- A country always shows both numbers, such as "3 trips · 19 days".

### Continents

- Each country belongs to one continent: Africa, Antarctica, Asia, Europe, North America, Oceania,
  or South America. The bundled Natural Earth outlines
  ([ADR-009](../adr/ADR-009-a-bundled-natural-earth-base-map.md)) have no continent, so a small
  table that maps each country `id` to a continent ships in `Fibotrail.Places`, with a test that
  every outline has a continent.
- A transcontinental country, such as Russia or Turkey, belongs to the one continent that Natural
  Earth's `CONTINENT` field gives it.
- The number of countries on a continent is the number of outlines with that continent.

### The Stats screen

From top to bottom:

1. **Summary tiles.** Trips, countries (with the share of the 236 bundled outlines that have an
   `id`, such as "12 / 236 · 5.1%"), continents (such as "4 / 7"), cities (merged by the rule of
   [ADR-015](../adr/ADR-015-cities-are-merged-places-and-theme-parks-keep-their-own-identity.md), without
   theme parks), days away, and distance in kilometers. The tiles use the stat tile pattern of the design document, with the 34 px size of
   the [trip page](trip-page.md). They are part of the host, not a view, so the screen is never
   empty.
2. **Continents.** A row of cards that scrolls sideways. Each card shows a progress ring with the
   continent's outline inside, the continent name, and the countries visited out of its total, such
   as "Europe · 4 / 44". Continents with no visits come last, with a Mist ring.
3. **Views in the Stats slot**, in registry order. Each view has a heading and its own loading,
   empty, and error states, so one failing view doesn't affect the others.

### The continent page

- Tapping a continent card opens a page with the continent's name, its countries visited out of
  its total, and a list of its visited countries.
- Each row shows the country name and "3 trips · 19 days".
- The list is sorted by days, then by trips, then by name.

### The country page

- Tapping a country opens a page with the country name, the continent as the back button, the first
  visit date, and two tiles: trips and days.
- **Views in the country slot**, in registry order. Each one receives the country `id`.
- **Trips.** The added trips with a stop in the country, newest first. Each opens its trip page.
- When any of those trips has photos with no position, a line says so, such as "Days count only
  photos with a location."

### The first three views

- **Trips per year.** A bar chart in Teal, with the current year in Gold, each bar labeled with its
  count. Tapping a bar lists that year's trips. Route: `GET /api/stats/trips-per-year`.
- **Trip length.** A histogram of trips by days, in five bins: 2, 3, 4 to 5, 6 to 8, and 9 or more.
  Route: `GET /api/stats/trip-length`.
- **Records.** The longest trip, the farthest trip from home, the trip with the most countries, the
  most-visited country (by trips, then by days), and the longest gap between two trips. Each record
  opens its trip or country. Route: `GET /api/stats/records`.

### API

- Every route needs a session.
- `GET /api/stats/summary` returns the summary tiles and the continents with their visited and total
  counts. It belongs to the host.
- `GET /api/stats/continents/{continent}` returns the visited countries of that continent with trips
  and days.
- `GET /api/stats/countries/{id}` returns one country's name, continent, first visit, trips, and
  days.
- Each view has its own route and result type, per ADR-014.
- The OpenAPI document and the TypeScript types are updated, per
  [web-app-development.md](../knowledge/web-app-development.md).

### Navigation

- Stats becomes a destination, per the four destinations of the design document and
  [ADR-007](../adr/ADR-007-native-ios-feel-through-a-progressive-web-app.md). The
  [app shell spec](app-shell.md) is updated in the same change.

## Acceptance criteria

- With three added trips to Sweden (5 days in July 2019, 7 days from December 27, 2022 to January 2,
  2023, and 7 days in August 2024), the Sweden row reads "3 trips · 19 days".
- A trip with days in Denmark and Sweden counts once for each country. A day with photos in both
  counts as a day in each.
- A suggested or ignored trip changes no number on the screen.
- A day with photos but no position adds to days away and to no country.
- The total of the continent cards' visited counts equals the countries tile.
- Every outline in the bundled data has a continent, and a test fails if one doesn't.
- With every view removed from both registries, `./scripts/check.sh` passes, and the Stats screen
  shows the summary tiles and continents.
- A test fails when anything outside a registry imports a view's folder.
- Every chart has a text alternative that a screen reader reads.
- The screen passes the ADR-007 run-through on an iPhone.

## Open questions

1. **Should a country with no positioned photos on a trip still count that trip's days?** The
   default is no, because a day can't be placed without a position.
