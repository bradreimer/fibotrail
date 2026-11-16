# Travel clock

Tracking issue: [#66](https://github.com/bradreimer/fibotrail/issues/66).

## Purpose

This spec defines the travel clock, a view on the [Stats screen](stats-screen.md). It answers
"when in the year do I travel?" with a wheel of the 12 months, where each month's bar grows with
the days you spent away in that month, across every year.

The [season wheel](season-wheel.md) shows one country. The travel clock shows every added trip, so
it reveals a rhythm, such as a habit of July trips or a gap every spring.

It is a self-contained view, per
[ADR-012](../adr/ADR-012-stats-views-are-self-contained-modules.md): removing it means deleting its
folders, its two registry lines, and this spec.

## User story

As a person with years of trips, I want a 12-month wheel that shows when in the year I travel, so
that I can see my travel rhythm, such as a habit of July trips or a gap every spring.

## Goals

- One glance shows the busiest and the quietest months.
- Each month's trips are one tap away.
- The wheel reads well without color, through its labels and its text alternative.

## Non-goals

- Seasons and hemispheres. The season wheel covers them for each country.
- Filters by year or country.
- A comparison between years.

## Requirements

### What counts

- **Days away in a month** is the number of local days of added trips that fall in that month, in
  any year. Every day of a trip counts, with or without a positioned photo, as on the trip page.
- **Trips in a month** is the number of added trips with at least one day in that month. A trip from
  December 27 to January 2 counts for both months.

### The clock

- A wheel of 12 wedges, January at the top, clockwise, with each month's three-letter name outside
  the wheel.
- Each wedge is a bar that grows from an inner circle outward. Its length is in proportion to the
  month's value, and the busiest month reaches the outer edge. A month with no value shows only a
  thin Mist line.
- The bars are Teal. The selected month is Gold.
- A switch over the wheel, with **Days** and **Trips**, chooses the value. **Days** is the default.
- The center shows the busiest month and its value, such as "July · 41 days", or the selected month
  when one is selected.

### Interaction

- Tapping a wedge selects its month. The tap area of each wedge is at least 44 points deep.
- A card under the wheel shows the selected month, its days and trips, such as "July · 41 days ·
  6 trips", and the trips of that month, newest first. Each trip opens its trip page.
- **Earlier** and **Later** buttons, each 44 points, step through the months, from December to
  January and around. The card announces changes to a screen reader.
- By default the busiest month is selected.

### Motion

- When the view first appears, the bars grow from the center over 610 ms, January first, each
  starting 34 ms after the one before.
- With reduce motion on, the bars appear at once.

### Text alternative

- The wheel has a text alternative that lists each month with its value, such as "Days away by
  month. January: 12. February: 5. … July: 41, the busiest."

### API and modules

- Server: `Fibotrail.Stats/Views/TravelClock/`, a pure function from the stats library to the days
  away and the trips (ID, name, dates) for each month.
- Route: `GET /api/stats/travel-clock`.
- Web: `apps/web/src/stats/views/travel-clock/`, registered in the Stats slot.
- The view has no table, migration, or setting, per ADR-012.

## Acceptance criteria

- A trip from July 12 to 16 adds 5 days to July and 1 trip to July.
- A trip from December 27 to January 2 adds 5 days to December, 2 days to January, and 1 trip to
  each.
- The busiest month's bar reaches the outer edge, and it is selected when the view opens.
- Switching to **Trips** changes every bar and the center to trip counts.
- **Later** on December selects January.
- With reduce motion on, nothing animates.
- With the view removed from both registries, the Stats screen still shows its tiles and continents.

## Open questions

1. **Should the clock weigh each year the same?** Today a year with many trips outweighs a quiet
   one. An average per year would show habit rather than volume.
