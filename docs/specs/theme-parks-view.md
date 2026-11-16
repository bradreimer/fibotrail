# Theme parks view

Tracking issue: [#64](https://github.com/bradreimer/fibotrail/issues/64).

## Purpose

This spec defines the Theme parks view, a view on the [Stats screen](stats-screen.md). It answers
"how many days have I spent in Universal Studios Florida?" with the days and trips of every park,
grouped by collection, such as Disney parks and Universal parks.

It is a self-contained view, per
[ADR-014](../adr/ADR-014-stats-views-are-self-contained-modules.md): removing it means deleting its
folders, its registry lines, and this spec. It reads the park days that the stats library already
holds, so it can't disagree with the [collections](collections.md) screens. The counting rule is in
the collections spec, and this spec doesn't repeat it.

## User story

As a person who visits Disney and Universal theme parks often, I want to see the days and trips for
each park on the Stats screen, so that I can answer "how many days have I spent in Universal Studios
Florida?" at a glance.

## Goals

- One glance gives the days and trips of every park, as text.
- A park nobody visited shows 0 days and says so in words, not by color alone.
- The numbers equal the collection screens' numbers, for every park.
- With no collection files, the view says so, and the rest of the Stats screen is unchanged.

## Non-goals

- Stamps, boundaries, and maps. The Collections screens show them.
- Group totals as tiles in the Stats summary row. The totals stay in this view until real data shows
  they earn the space.
- Filters by year.

## Requirements

### What is shown

- One section for each collection, headed with its name and its days across its parks, such as
  "Disney parks · 18 days".
- Under each heading, one row for each open place, most days first and then by name. A row reads the
  park name, its days, and its trips, such as "Universal Studios Florida, 6 days, 2 trips", and the
  date of the first visit. A place that no trip visited reads "0 days · Not visited yet".
- Planned places aren't listed.
- The numbers are the park days of the [collections spec](collections.md#what-counts). The view
  doesn't compute them.

### Interaction

- Each row with trips links to its collection, so the park's trips and dates are one tap away.

### API and modules

- Server: `Fibotrail.Stats/Views/ThemeParks/`, a pure function from the stats library to the groups.
- Route: `GET /api/stats/theme-parks`. It returns an empty list when no collection file exists.
- Web: `apps/web/src/stats/views/theme-parks/`, registered in the Stats slot.
- The view has no table, migration, or setting, per ADR-014.

## Acceptance criteria

- A park with 3 days and 1 trip reads "3 days, 1 trip".
- A park nobody visited reads "0 days" and "Not visited yet".
- For every park, the view and the collection detail give the same days. A server test checks this.
- A suggested, later, or ignored trip adds no days.
- With the view removed from both registries, the Stats screen still shows its tiles and continents.
- With no collection files, the view shows a plain line, not an error.

## Open questions

1. **Should the group totals become tiles in the summary row?** The default is no, until real data
   shows they earn the space.
