# ADR-011: Visits are derived from added trips on the server

## Status

Accepted

## Context

The life map ([spec](../specs/life-map.md)) fills in the countries you've visited. The design
document's data model has a `place` table and a `visit` table that link trips to places. Nothing in
version 0.1 reads them, so they don't exist.

Three facts shape the choice:

- Each trip stores its stops (position, place name, photo count) as JSON, per
  [ADR-010](ADR-010-stops-and-routes-are-stored-with-the-trip.md). Stops already leave out photos
  that are [on the way](../specs/on-the-way.md).
- The country outlines are a bundled file, per
  [ADR-009](ADR-009-a-bundled-natural-earth-base-map.md), and today only the web app has them.
- A library has tens of added trips, not thousands. The maintainer's has 35 suggestions. Matching a
  few hundred stops to 177 country outlines is fast.

The Stats screen and the collections of phase 5 need the same counts, and need them on the server,
because the rules for them must be tested with plain data and no browser.

## Decision

The server works out the visited countries from the stored stops of added trips, each time the Map
screen asks, using the bundled country outlines. No `place` or `visit` table is added for countries.

- **Where.** A new C# project, `Fibotrail.Places`, holds pure functions: outlines and stop positions
  in, visited countries out. It has a test project, and it needs no database or running Immich.
- **The outlines.** The same Natural Earth country outlines as ADR-009, built into the server as an
  embedded resource by a script from the `world-atlas` package, so the web app and the server share
  one source.
- **What counts.** Only added trips. A country is visited when an added trip has a stop in it.
- **The API.** `GET /api/map` returns the countries with their trips and first visits, each added
  trip's route, and the totals. The browser draws what it's given and matches nothing itself.

When a feature needs stored matches, such as a place with its own boundary in a collection, or the
`place` and `visit` tables for regions and cities, that feature adds them with its own ADR.

## Alternatives considered

- **Match in the browser.** The server would send the stops of every added trip, and the browser
  would test them against the outlines it already has. It needs no new server code. Set aside
  because the Stats screen and collections need the same numbers on the server, so the rule would
  end up written twice.
- **Store `place` and `visit` rows now.** This is the design document's model. Set aside for
  countries because it adds a migration, rebuild logic, and a place to go stale when a trip changes,
  for data that is cheap to derive. It stays the plan for regions, cities, and collections.
- **Use Immich's country text.** The names are free text and differ between versions, and a stop on
  a coast has no match. Set aside in favor of positions and outlines.

## Consequences

- Visits are always current: adding, ignoring, or changing a trip changes the map on the next
  request, with nothing to rebuild.
- The outlines now ship in two places, the web bundle and the server. One script makes both from
  the same package, and a test compares their country counts.
- The work is repeated on every request. With tens of trips that is a few milliseconds. If a
  library grows to thousands, cache the result and clear it when a trip's status or stops change.
- A stop at 1:50 million detail can fall just outside every outline on a coast. The spec gives a
  nearest-country rule for that, and the 25 km value is a starting point that real trips may change.
- Regions and cities will need their own boundary data and their own decision. This ADR doesn't
  settle them.
