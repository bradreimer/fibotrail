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
- Have I seen Sweden in every season?

The screen works like a set of nested folders: tap a continent to see its countries, and tap a
country to see its trips, days, and seasons.

## User story

As a person with years of trips in Fibotrail, I want a Stats screen that answers questions about
my travel in numbers and charts, so that I can relive my trips and see where, when, and how often I
went.

## Goals

- Each question above is answered in at most two taps from the Stats screen.
- Every number comes from the trips you've added, and nothing from a suggestion.
- Every number uses the same rules as the trip page and the life map, so the same question gets
  the same answer on every screen.
- Every chart has a text alternative and never relies on color alone.

## Non-goals

- Collections such as Disney parks. They are their own phase 5 work, with their own spec.
- Regions and cities, and a count of them
  ([#48](https://github.com/bradreimer/fibotrail/issues/48)). The season drill-down stops at the
  country until #48 decides how cities are named.
- Filters by year or by collection.
- Sharing or exporting the numbers.
- A yearly recap page and Home Assistant sensors. They come after version 1.

## Requirements

### What counts

- Only trips with the status "added" count. Suggested, later, and ignored trips don't.
- A country is visited when an added trip has a stop in it, by the rules of the
  [life map](life-map.md#what-counts-as-visited). Photos on the way don't count
  ([on the way](on-the-way.md)).
- **Trips in a country** is the number of added trips with a stop in that country.
- **Days in a country** is the number of distinct local days, across all added trips, that have at
  least one away photo with a position in that country. This is the rule of the
  [main destination](main-destination.md) applied to countries. A day with photos in two countries
  counts for both.
- Days in a country only count photos with a position. A day with no positioned photo counts for
  the trip's days but for no country. The country page says so when it applies (see below).
- A country always shows both numbers, such as "3 trips · 19 days".

### Seasons

- Seasons are meteorological, by the local date of the day:

  | Season | Northern hemisphere | Southern hemisphere |
  | --- | --- | --- |
  | Winter | December, January, February | June, July, August |
  | Spring | March, April, May | September, October, November |
  | Summer | June, July, August | December, January, February |
  | Autumn | September, October, November | March, April, May |

- The hemisphere comes from the latitude of the photos that put the day in the country. A latitude
  of 0 or more is northern. A country that spans the equator, such as Brazil, can have days in both
  hemispheres, and each day uses its own latitude.
- A country's season is **seen** when at least one of its days falls in that season.
- The country page shows the seasons seen out of 4, such as "2 of 4 seasons".

### Continents

- Each country belongs to one continent: Africa, Antarctica, Asia, Europe, North America, Oceania,
  or South America. The bundled Natural Earth outlines
  ([ADR-009](../adr/ADR-009-a-bundled-natural-earth-base-map.md)) have no continent, so a small
  table that maps each country `id` to a continent ships with the server, with a test that every
  outline has a continent.
- A transcontinental country, such as Russia or Turkey, belongs to the one continent that Natural
  Earth's `CONTINENT` field gives it.
- The number of countries on a continent is the number of outlines with that continent.

### The Stats screen

From top to bottom:

1. **Summary tiles.** Trips, countries (with the share of the 236 bundled outlines that have an
   `id`, such as "12 / 236 · 5.1%"), continents (such as "4 / 7"), days away, and distance in kilometers. The tiles use the
   stat tile pattern of the design document, with the 34 px size of the
   [trip page](trip-page.md).
2. **Continents.** A row of cards that scrolls sideways. Each card shows a progress ring with the
   continent's outline inside, the continent name, and the countries visited out of its total, such
   as "Europe · 4 / 44". Continents with no visits come last, with a Mist ring.
3. **Trips per year.** A bar chart in Teal, with the current year in Gold, each bar labeled with
   its count. Tapping a bar lists that year's trips.
4. **Trip length.** A histogram of trips by days, in five bins: 2, 3, 4 to 5, 6 to 8, and 9 or
   more.
5. **Records.** A list with the longest trip, the farthest trip from home, the trip with the most
   countries, the most-visited country (by trips, then by days), and the longest gap between two
   trips. Each record opens its trip or country.

### The continent page

- Tapping a continent card opens a page with the continent's name, its countries visited out of
  its total, and a list of its visited countries.
- Each row shows the country name, "3 trips · 19 days", and four small season marks.
- The list is sorted by days, then by trips, then by name.

### The country page

- Tapping a country opens a page with the country name, the first visit date, and two tiles:
  trips and days.
- **Season wheel.** A ring cut into 12 months, January at the top, clockwise. Each month slice is
  shaded by the number of days in that country in that month, from Mist (none) to Teal (most). The
  four seasons are arcs around the outside, with a check on each season seen. The center shows the
  seasons seen, such as "2 of 4". In the southern hemisphere, the season arcs move to the months of
  that hemisphere.
- The wheel's text alternative lists each season with its days, such as "Summer: 12 days. Winter:
  7 days. Spring and autumn: not yet."
- **Trips.** The added trips with a stop in the country, newest first, as trip cards. Each card
  opens its trip page.
- When any of those trips has photos with no position, a line says so, such as "Days count only
  photos with a location."

### API

- `GET /api/stats` needs a session. It returns the summary totals, the continents with their
  visited and total counts, the trips per year, the trip length bins, and the records.
- `GET /api/stats/continents/{continent}` returns the visited countries of that continent with
  trips, days, and seasons seen.
- `GET /api/stats/countries/{id}` returns one country's first visit, trips, days, days per month,
  hemisphere per month, seasons seen, and its trips.
- The stats are computed on the server, as pure functions in their own project, so they can be
  tested with plain data.
- The OpenAPI document and the TypeScript types are updated, per
  [web-app-development.md](../knowledge/web-app-development.md).

### Navigation

- Stats becomes a destination, per the four destinations of the design document and
  [ADR-007](../adr/ADR-007-native-ios-feel-through-a-progressive-web-app.md). The
  [app shell spec](app-shell.md) is updated in the same change.

## Acceptance criteria

- With three added trips to Sweden (5 days in July 2019, 7 days in December 2022, and 7 days in
  August 2024), the Sweden row reads "3 trips · 19 days" and the season wheel shows "2 of 4".
- A day in Sydney in January counts as summer. A day in Stockholm in January counts as winter.
- A trip with days in Denmark and Sweden counts once for each country. A day with photos in both
  counts as a day in each.
- A suggested or ignored trip changes no number on the screen.
- A day with photos but no position adds to days away and to no country.
- The total of the continent cards' visited counts equals the countries tile.
- Every outline in the bundled data has a continent, and a test fails if one doesn't.
- Every chart has a text alternative that a screen reader reads.
- The screen passes the ADR-007 run-through on an iPhone.

## Open questions

1. **Which creative visuals come next?** Three were proposed and left out of this spec: a
   Fibonacci spiral with one dot per trip in date order, a travel clock that shows the months of
   all trips on one wheel, and distance rings that place each trip by its distance from home.
2. **Should seasons go down to cities, such as London?** That waits for
   [#48](https://github.com/bradreimer/fibotrail/issues/48).
3. **Should a country with no positioned photos on a trip still count that trip's days?** The
   default is no, because a day can't be placed without a position.
