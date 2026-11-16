# ADR-013: Park days are derived from stored photo positions

## Status

Accepted

## Context

The [collections spec](../specs/collections.md) counts the days you spent in each theme park, such
as "How many days in Universal Studios Florida?" A park day is a distinct local day with at least
one photo inside the park's boundary, on a trip that has at least 3 photos there.

That rule needs the position and the local day of every photo. What the app stores today isn't
enough:

- A trip's stops hold a center, a place name, and a photo count
  ([ADR-010](ADR-010-stops-and-routes-are-stored-with-the-trip.md)). A stop groups photos within the
  stop radius, so two neighboring parks can end up in one stop, and the photos' own positions are
  gone.
- The `trip_photo` table holds only the photo ID and the stop index.
- Asking Immich for every photo of every trip on each request would be slow, and it would make the
  Collections screen depend on Immich being up.

[ADR-011](ADR-011-visits-are-derived-from-added-trips-on-the-server.md) derives visits on each
request and stores no `visit` table. [ADR-003](ADR-003-collections-are-data-files.md) says a
collection is a data file and that Fibotrail matches photos to a boundary. This ADR settles where
the photo positions come from and what is stored.

Not known yet: how many photos an added trip holds on average, and how long matching takes on a
real library. The maintainer's library has 35 suggested trips. A trip is at least a few hundred
photos, so the total is in the tens of thousands.

## Decision

Store each trip photo's position and local day. Derive park days from them on the server, on each
request. Store no park, visit, or day table.

- **What is stored.** `trip_photo` gains three nullable columns: `latitude`, `longitude`, and
  `local_day`. Detection fills them from the photo it already holds, with the local day from the
  photo's local time, per [trip detection](../specs/trip-detection.md#rules). A photo with no
  position stores a local day and no position.
- **What is derived.** The park lookup and the day count are pure functions in the detection
  project. They take the photos of added trips and the boundaries, and return each park's days,
  trips, and first visit. Photos on the way and photos of trips that aren't added are left out
  before matching.
- **One caller for the numbers.** Collections and the Stats Theme parks view call the same
  function, so they can't disagree.
- **Cheap matching.** The boundaries get a bounding-box index at startup, per ADR-003. A photo is
  tested against polygons only when it falls in a bounding box, so most photos cost one lookup.
- **Existing trips.** The migration adds the columns empty. The next detection run fills them in.
  Until then, the screens say that park days need a rescan. The server copies the database before
  it applies the migration, as it did for the Map.

## Alternatives considered

- **Store a `park_day` table.** Detection would write one row per park and day. Reads would be
  trivial. Set aside because every boundary fix, new park, or photo marked on the way would need a
  rebuild, and a stale table would give two answers to one question. It stays the plan if
  measurement shows the work is too slow.
- **Derive from stops only.** This needs no migration. Set aside because a stop is a cluster, so
  neighboring parks merge into one stop, and the rule can't count days from a stop's first and last
  time.
- **Ask Immich on each request.** This stores nothing new. Set aside because it makes every
  request slow, ties the screen to Immich being reachable, and puts the whole library on the
  network path for a stats page.
- **Store the matched park on each photo.** This is quick to read. Set aside because the match
  would go stale whenever a boundary file changes.

## Consequences

- A boundary change shows on the next request, with no rescan. A new collection file works the
  same way.
- The database holds a position for each trip photo. These are already in Immich, and the data
  stays on the device, so ADR-001 holds: nothing is sent out and nothing is written to Immich.
  The backup guidance in the README covers the new columns without a change.
- Each request matches every photo of every added trip. With tens of thousands of photos and a
  bounding-box index this should take a few tens of milliseconds, but that is an estimate. The
  first implementation measures it on the maintainer's library and records the result here. If it
  is too slow, cache the result and clear it when a trip's status, a photo's mark, or a
  collection file changes.
- The `trip_photo` table grows by about 20 bytes per row. For 100,000 photos that is about 2 MB.
- Another stats view that needs photo positions, such as the season wheel, can read the same
  columns. Each view still follows its own spec.
- Anything that rewrites `trip_photo` rows, such as detection and marking a photo on the way, must
  keep the three columns. A test covers both.
