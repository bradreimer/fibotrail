# Trip spiral

Tracking issue: [#59](https://github.com/bradreimer/fibotrail/issues/59).

## Purpose

This spec defines the trip spiral, a view on the [Stats screen](stats-screen.md). It places every
added trip along a Fibonacci spiral in date order, so your whole travel history fits in one
picture. The oldest trip sits at the center and the newest at the edge, like the rings of a shell.

The spiral is drawn through Fibonacci squares, which ties the visual to the app's name and to its
spacing scale (3, 5, 8, 13, 21, 34, 55).

It is a self-contained view, per
[ADR-014](../adr/ADR-014-stats-views-are-self-contained-modules.md): removing it means deleting its
folders, its two registry lines, and this spec.

## User story

As a person with years of trips, I want every trip placed along a Fibonacci spiral in date order,
sized by days and colored by continent, so that I can see my whole travel history in one picture
and tap back into any trip.

## Goals

- One picture shows every added trip, in order.
- Any trip is two taps away: tap its dot, then open it.
- The picture works without color, through the trip card and the text alternative.

## Non-goals

- Filters by year.
- Trips that aren't added.
- Exact dates on the spiral. The year labels and the trip card give the dates.

## Requirements

### The spiral

- The spiral is made of quarter circles in Fibonacci squares of sizes 1, 1, 2, 3, 5, 8, 13, 21, 34,
  and 55, each square added clockwise around the ones before it. The faint squares are drawn behind
  the spiral in a color darker than Mist.
- The spiral stands tall on a phone and fits the width of the screen less 21 px on each side.
- Trips are placed in date order, oldest first, at equal distances along the spiral, from 7 percent
  to 98.5 percent of its length. Equal spacing keeps dots apart; the inner turns are short, so the
  oldest trips sit close to the center.
- With one trip, its dot sits at the start. With no trips, the view shows its empty state.

### Dots

- Each dot's radius is 3.5 px plus 1.4 times the square root of the trip's days, so a 4-day trip is
  6.3 px and a 10-day trip is 7.9 px.
- Each dot is colored by its trip's continent, from the stats library and the continent table of
  the [Stats screen](stats-screen.md#continents). The selected dot has a Paper ring.
- Year labels sit outside the spiral at the first trip of 2012, 2016, 2020, 2024, and every fourth
  year after, when those years have trips.

### Interaction

- Tapping a dot selects its trip. The tap area of each dot is at least 28 px across. Where dots are
  closer than that, the arrows are the reliable way to select.
- The trip card under the spiral shows "Trip 18 of 24", the trip name, its continent, and its days.
  **Earlier** and **Later** buttons, each 44 points, step through the trips. Tapping the name opens
  the trip page. The card announces changes to a screen reader.
- By default the newest trip is selected.
- A row of continent chips, with **All** first, fades the dots of other continents to 18 percent.
  Only continents with trips appear.

### Motion

- When the view first appears, the spiral draws itself from the center over 1,597 ms, and the dots
  appear one by one in date order, 55 ms apart, starting at 600 ms.
- With reduce motion on, the spiral and dots appear at once.

### Text alternative

- The spiral has a text alternative such as "24 trips from 2012 to 2026, placed along a spiral from
  the oldest at the center to the newest at the edge. Selected: Anaheim, May 2026, 4 days."

### API and modules

- Server: `Fibotrail.Stats/Views/TripSpiral/`, a pure function from the stats library to the added
  trips in date order, each with ID, name, start date, days, and continent.
- Route: `GET /api/stats/trip-spiral`.
- Web: `apps/web/src/stats/views/trip-spiral/`, registered in the Stats slot. The spiral geometry is
  a pure function in that folder, with its own tests.
- The view has no table, migration, or setting, per ADR-014.

## Acceptance criteria

- With 24 added trips, 24 dots appear, the oldest nearest the center and the newest nearest the
  edge.
- The distances along the spiral between neighboring dots are equal, to within 1 px.
- A 10-day trip's dot is larger than a 4-day trip's dot.
- Tapping **Europe** fades every non-European dot, and tapping **All** restores them.
- **Earlier** on the first trip and **Later** on the last trip do nothing.
- With reduce motion on, nothing animates.
- With the view removed from both registries, the Stats screen still shows its tiles and continents.

## Open questions

1. **How does the spiral handle hundreds of trips?** At about 60 trips the dots on the outer turn
   start to touch. A larger library may need smaller dots or one more Fibonacci square.
2. **Is continent the right color key?** Year bands are the alternative, which would show eras of
   travel instead of places.
