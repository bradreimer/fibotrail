# ADR-021: A pinned photo is a decision that detection reads as a day

## Status

Accepted

## Context

Detection puts a photo in a trip by its capture time and position. A photo with a wrong time, or
with no position and a day too light to be a burst day (rule 3b), is left out of its trip, and
nothing in Fibotrail can add it. Immich stays read-only per
[ADR-001](ADR-001-immich-is-read-only.md), and the photo table is rebuilt from Immich on a scan, so
the fix can't be written to either. It must survive every rescan, per
[ADR-002](ADR-002-decisions-are-stored-separately-from-detected-data.md).

## Decision

A pin is a row in the `pinned_photo` table: the Immich asset ID, a day, and when you decided.
`fibotrail trip pin <id> <photo>` writes it. The day is the day of the trip nearest the photo's own
day, so a photo that is already inside the trip keeps its day. On every run, before rule 1,
detection reads the photo as taken on that day, with its own time of day and position, using a pure
function in `Fibotrail.Detection`. It never changes the table or the photo row. `fibotrail trip
unpin <photo>` deletes the row.

## Alternatives considered

- **Store the trip's ID.** Trip IDs belong to detected data, which is replaced, so a pin could
  point at a trip that is gone. A day follows the trip through a rescan.
- **Add the photo to the trip after detection.** The trip's stops, route, distance, farthest point,
  and cover would all need to be computed again, in a second code path. Moving the photo first lets
  the same rules do it.
- **Write the day to the photo table.** The scanner rebuilds that table from Immich.
- **A switch in the photo viewer.** Not now. The command is enough for a rare repair.

## Consequences

- A pin moves the photo to a day, so it can join a trip, or change which trip it's in. It does not
  force a trip to exist. If the day is in no trip after the rules run, the photo is in no trip.
- A pinned photo also counts for the days it falls on. Its stop is where its position is, so a
  pinned photo with a position far from the trip adds a stop there.
- If you later change the trip's days, for example with `fibotrail join`, the pin keeps its day.
- Stored per-photo views, such as the photo's date in the viewer, still show the photo's own time.
