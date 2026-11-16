# Distance rings

Tracking issue: [#67](https://github.com/bradreimer/fibotrail/issues/67).

## Purpose

This spec defines the distance rings, a view on the [Stats screen](stats-screen.md). It places
each added trip on rings around your home, like a radar screen: how far out a dot sits is how far
the trip took you, and its direction is the direction you went.

It answers "how far do I roam?" and "which directions have I never gone?". The trip page's
"farthest from home" tile gives one trip's distance. This view puts every trip on one picture.

It is a self-contained view, per
[ADR-014](../adr/ADR-014-stats-views-are-self-contained-modules.md): removing it means deleting its
folders, its two registry lines, and this spec.

## User story

As a person with years of trips, I want each trip placed on rings around my home by how far and in
which direction I went, so that I can see how far I roam and which directions I've never gone.

## Goals

- One glance shows the spread of near and far trips, and the empty directions.
- Each trip is one tap away.
- The home location never leaves the server.

## Non-goals

- A map background. The rings are an abstract chart, not a map, so the directions are true but the
  shapes of countries are not shown.
- Distance traveled within a trip. The trip page has it.
- Filters by year or continent.

## Requirements

### What counts

- Each added trip has one point: its **farthest stop**, the stop with the greatest great-circle
  distance from the home location. This is the same stop as the trip page's "farthest from home"
  tile.
- The **distance** is that great-circle distance in kilometers.
- The **direction** is the initial great-circle bearing from home to that stop, from 0 to 360
  degrees, with 0 as north.
- The home location is added to the stats library, as the one shared fact this view needs. The
  route returns distances and bearings only, never the home coordinates.

### The rings

- Home is a small Paper dot at the center. North is up, and the letters N, E, S, and W sit outside
  the outer ring.
- Rings mark 100, 500, 1,000, 5,000, and 10,000 km, each labeled on its north side. The outer edge
  is 20,000 km, about half the earth's circumference.
- Distance from the center uses a logarithmic scale from 50 km (the home radius) to 20,000 km, so
  near trips don't crowd the center. A trip at 1,000 km sits at about half the radius.
- The rings are Mist lines on the view's card.

### Dots

- Each trip is a Teal dot at its direction and scaled distance. Its radius is 3.5 px plus 1.4 times
  the square root of the trip's days, the same as the [trip spiral](trip-spiral.md).
- The selected dot is Gold with a Paper ring.

### Bands

- Under the rings, a row of five bands gives the trip count in each: under 500 km, 500 to 1,000 km,
  1,000 to 5,000 km, 5,000 to 10,000 km, and over 10,000 km, such as "Under 500 km · 6 trips".
- Tapping a band fades the dots outside it to 18 percent. Tapping it again clears the fade. Each band
  is a button of at least 44 points.

### Interaction

- Tapping a dot selects its trip. The tap area of each dot is at least 28 px across.
- A card under the rings shows the trip name, its distance and direction in words, and its days,
  such as "Sydney, January 2023 · 15,620 km southwest · 10 days". The direction uses eight compass
  points. Tapping the name opens the trip page.
- **Nearer** and **Farther** buttons, each 44 points, step through the trips by distance. The card
  announces changes to a screen reader.
- By default the farthest trip is selected.

### Motion

- When the view first appears, the rings fade in from the center, and the dots move out from home to
  their places over 610 ms.
- With reduce motion on, everything appears at once.

### Text alternative

- The rings have a text alternative that gives the bands and the farthest trip, such as "24 trips by
  distance from home. Under 500 km: 6. 500 to 1,000 km: 3. 1,000 to 5,000 km: 8. 5,000 to 10,000 km:
  5. Over 10,000 km: 2. Farthest: Sydney, January 2023, 15,620 km southwest."

### API and modules

- Server: `Fibotrail.Stats/Views/DistanceRings/`, a pure function from the stats library to each
  added trip's ID, name, dates, days, distance, and bearing.
- Route: `GET /api/stats/distance-rings`.
- Web: `apps/web/src/stats/views/distance-rings/`, registered in the Stats slot. The scale and the
  compass words are pure functions in that folder, with their own tests.
- The view has no table, migration, or setting, per ADR-014.

## Acceptance criteria

- A trip whose farthest stop is due north of home sits straight up from the center, and its card
  says "north".
- A trip at 1,000 km sits on the 1,000 km ring, and one at 100 km on the 100 km ring.
- A trip across the antimeridian, such as from Hawaii to Japan, has the bearing of the shorter great
  circle.
- The route's response contains no home coordinates.
- The band counts add up to the number of added trips.
- With reduce motion on, nothing animates.
- With the view removed from both registries, the Stats screen still shows its tiles and continents.

## Open questions

1. **Is the farthest stop the right point?** The main destination is the alternative. It would show
   where trips were about, rather than how far they reached.
2. **Should the outer edge shrink to the farthest trip?** A library of short trips leaves the outer
   rings empty. A fixed edge keeps libraries comparable and shows how much of the world is left.
