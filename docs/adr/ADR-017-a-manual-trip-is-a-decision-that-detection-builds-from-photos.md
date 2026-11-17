# ADR-017: A manual trip is a decision that detection builds from photos

## Status

Accepted

## Context

Detection needs a position (rules 2 and 3) or a burst of at least 3 heavy days (rule 3b). A trip
photographed with a camera from 2005 has neither: Immich holds 310 photos from 2005-07-31 to
2005-08-06 with no GPS and no city, and Aug 3 has only 14 photos, so rule 3b finds no run. The trip
is real and nothing could add it. Detected trips are rebuilt on every run, so a trip written
straight into the `trip` table would be deleted by the next scan. A manual trip must survive every
rescan, per [ADR-002](ADR-002-decisions-are-stored-separately-from-detected-data.md).

## Decision

A manual trip is a row in the `manual_trip` table with its days, and an optional name, place,
country, and position. `fibotrail trip add` writes the row, and an Added decision with the trip's
fingerprint, so it's in the library as if you had chosen **Add trip**. Detection reads the table on
every run and builds each trip from the photos taken on its days, with a pure function in
`Fibotrail.Detection`. It never changes the table. A detected trip that shares a day with a manual
trip is dropped, and its decision stays. With no photo position in the days, the trip has one stop
at the position you gave, so the map and the statistics place it.

## Alternatives considered

- **Write the trip into the `trip` table.** The next detection run removes it, because detection
  replaces detected data.
- **Add the days to rule 3b as a setting.** It's a rule change for one trip, and it loses the place.
- **Write the position onto the photos.** Immich stays untouched, per
  [ADR-001](ADR-001-immich-is-read-only.md), and the photo table is rebuilt from Immich.
- **A form in the web app.** Not now. The command is enough for a rare repair.

## Consequences

- Manual trips are never rebuilt or deleted by detection. `fibotrail trip remove` deletes the row
  and its decision.
- A manual trip is High confidence, because you decided it.
- To change a manual trip, remove it and add it again.
- A manual trip needs at least one photo in its days. Photos that arrive later join it.
- Stats find a trip's country from its stop position, so a trip with no position adds no country.
