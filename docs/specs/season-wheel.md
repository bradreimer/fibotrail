# Season wheel

Tracking issue: [#58](https://github.com/bradreimer/fibotrail/issues/58).

## Purpose

This spec defines the season wheel, a view on the country page of the
[Stats screen](stats-screen.md). It answers "have I seen this country in every season?" with a ring
of the 12 months, shaded by the days you spent there, and the four seasons around the outside.

It is a self-contained view, per
[ADR-014](../adr/ADR-014-stats-views-are-self-contained-modules.md): removing it means deleting its
folders, its two registry lines, and this spec.

## User story

As a person who returns to the same countries, I want a wheel of the 12 months that shows the days
I spent in a country and the seasons I've seen there, so that I can tell at a glance whether I've
seen Sweden in every season.

## Goals

- One glance answers "which seasons have I seen here?".
- A season means the same weather in both hemispheres.
- The wheel reads well without color, through its marks and its text alternative.

## Non-goals

- Seasons for cities, such as London. That waits for
  [#48](https://github.com/bradreimer/fibotrail/issues/48).
- Seasons on the Stats screen or the continent page. This view lives on the country page only.
- Other season definitions, such as astronomical seasons or wet and dry seasons.

## Requirements

### Seasons

- Seasons are meteorological, by the local date of the day:

  | Season | Northern hemisphere | Southern hemisphere |
  | --- | --- | --- |
  | Winter | December, January, February | June, July, August |
  | Spring | March, April, May | September, October, November |
  | Summer | June, July, August | December, January, February |
  | Autumn | September, October, November | March, April, May |

- The hemisphere comes from the latitude of the photos that put the day in the country, from the
  stats library. A latitude of 0 or more is northern. A country that spans the equator, such as
  Brazil, can have days in both hemispheres, and each day uses its own latitude.
- The days of a country are the days of the [Stats screen](stats-screen.md#what-counts) rule.
- A season is **seen** when at least one of the country's days falls in it.

### The wheel

- A ring cut into 12 months, January at the top, clockwise, with each month's initial inside the
  ring.
- Each month slice is shaded by the country's days in that month, from Mist (none) to Teal (most).
  A legend under the wheel gives the range, such as "0 days" to "7 days".
- The four seasons are arcs around the outside, each labeled with its name. A seen season is a solid
  Gold arc. A season not yet seen is a dashed Mist arc. In the southern hemisphere the arcs sit on
  that hemisphere's months.
- The center shows the seasons seen, such as "2 of 4", with the caption "seasons seen", or "every
  season" when all four are seen.
- A line over the wheel names the hemisphere. A country with days in both says "Both hemispheres",
  and each month slice uses the hemisphere of most of its days.

### The season rows

- Under the wheel, one row per season: a check mark when seen, the season name, its months (such as
  "Jun–Aug"), and its days, or "Not yet".
- Each row is a button of at least 44 points. Tapping it highlights that season's months and arc,
  dims the rest, and shows the season's days in the center. Tapping it again clears the highlight.

### Text alternative

- The wheel has a text alternative that lists each season with its days, such as "Sweden, 2 of 4
  seasons seen. Winter: 7 days. Spring: not yet. Summer: 12 days. Autumn: not yet."

### API and modules

- Server: `Fibotrail.Stats/Views/SeasonWheel/`, a pure function from the stats library and a
  country `id` to the days per month, the hemisphere per month, and the days and seen state of each
  season.
- Route: `GET /api/stats/countries/{id}/season-wheel`.
- Web: `apps/web/src/stats/views/season-wheel/`, registered in the country slot.
- The view has no table, migration, or setting, per ADR-014.

## Acceptance criteria

- With added trips to Sweden of 5 days in July 2019, 7 days from December 27, 2022 to January 2,
  2023, and 7 days in August 2024, the wheel shows July 5, August 7, December 5, and January 2, and
  the center reads "2 of 4". Winter has 7 days and summer has 12.
- A day in Sydney in January counts as summer. A day in Stockholm in January counts as winter.
- A country with days in all four seasons shows "every season".
- Tapping **Summer** dims every month outside June to August, and tapping it again clears the dim.
- A screen reader reads the text alternative and each season row.
- With the view removed from both registries, the country page still shows its tiles and trips.

## Open questions

1. **Does a single day make a season seen?** The default is yes. A minimum, such as 2 days, would
   stop a layover day from counting, but photos on the way already don't count.
