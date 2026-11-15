# ADR-010: Stops and routes are stored with the trip, and decisions carry their dates

## Status

Accepted

## Context

Trip detection finds a trip's stops and its route. The inbox draws a route preview for every
suggestion, and the trip page draws the route and its pins. The
[roadmap](../roadmap.md#decisions-the-mvp-still-needs) asked where stops and routes are stored.

[ADR-002](ADR-002-decisions-are-stored-separately-from-detected-data.md) keys a decision by the
trip's fingerprint, a hash of the start date and the main place. The
[trip detection spec](../specs/trip-detection.md) asked, as an open question, whether that is
stable enough: when a photo from the day before a trip is uploaded late, the start date moves and
the fingerprint changes.

## Decision

**Stops and routes are computed when a trip is detected and stored with the trip.**

- The `trip` row holds its stops as JSON, in route order: the position, the place name, the first
  and last photo time, and the photo count of each stop. The route is the line through the stops
  in that order, so it needs no column of its own.
- Each `trip_photo` row holds the number of the stop where the photo was taken, or nothing for a
  photo with no position.
- The distance, the farthest point from home, and the day count are stored as columns.

**A decision carries the dates of its trip, and follows the trip when the fingerprint changes.**

- The `decision` table has one row for each trip you decided something about: the status, the
  name you gave, the cover you chose, and the trip's start and end dates at that time.
- Detection matches a detected trip to a stored trip, and to a decision, by fingerprint first.
  When no fingerprint matches, it matches the one whose dates cover more than half of the
  detected trip's days.
- When a decision is matched by dates, its fingerprint and dates are updated to the trip's new
  ones. Its status, name, and cover never change. This is the only write that detection makes to
  the `decision` table.
- The `trip` row keeps a copy of the status, the name, and the cover, so that a list of trips
  reads one table. The copy is written from the decision on every detection run.

## Alternatives considered

- **Compute stops and routes on request.** No stored copy to keep in step. Set aside because the
  inbox would run the stop rule for every suggestion on every visit, over every photo of each
  trip.
- **A `stop` table.** Stops could be queried on their own. Set aside because nothing in the MVP
  asks for stops except through their trip, and detected data is replaced as a whole anyway.
- **Match by fingerprint only.** The simplest rule. Set aside because one late photo would turn
  an ignored trip back into a suggestion, which breaks the promise of ADR-002.
- **Match by date overlap only.** No fingerprint needed. Set aside because two short trips close
  together could swap decisions, and a fingerprint match is exact when nothing moved.

## Consequences

- The inbox and the trip page read stops and routes with no computation.
- Stops are as old as the last detection run. A change to the stop radius shows after the next
  run.
- Deleting every `trip` and `trip_photo` row loses nothing. The next detection run rebuilds them
  and applies the decisions again. A test does exactly this.
- A decision can attach to the wrong trip in one case: two trips whose dates overlap by more than
  half, with different main places, where one of them disappears. This hasn't been seen, and the
  replay test on the real library is where it would show.
- A decision for a trip that detection no longer finds stays in the table. It applies again if
  the trip comes back.
