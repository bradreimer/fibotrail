# ADR-024: A GPS trail is a kept file that belongs to a trip's decision

## Status

Accepted

## Context

The [GPS trails spec](../specs/gps-trails.md) lets you add a GPX file from a GPS logger to a trip
and draw it on the trip map. That needs answers to three questions:

1. **Where the trail lives.** A logger such as the Qstarz BL-1000ST records a point every 1 to 5
   seconds, so a day is up to about 86,400 points, and a GPX file is about 130 bytes a point. The
   design wants the original file kept as the source of truth.
2. **What the trail belongs to.** Every detection run rebuilds the `trip` table. A trip row keeps
   its ID while detection matches it, but a trip that detection no longer finds is deleted
   ([ADR-010](ADR-010-stops-and-routes-are-stored-with-the-trip.md)). A trail you added is your
   data, and per [ADR-002](ADR-002-decisions-are-stored-separately-from-detected-data.md) it must
   survive a rescan.
3. **What the browser gets.** The trip page draws with MapLibre. Tens of thousands of points per
   trail are slow to send and to draw on a phone.

## Decision

**The file is kept byte for byte in the data folder, and the database holds what the map needs.**

```
GPX upload ──▶ parse (Fibotrail.Trails) ──▶ split at gaps ──▶ summary + simplified line ──▶ trail row
     │                                                                                  │
     └──────────────────────────▶ /data/trails/<random>.gpx                    decision row (FK)
```

- **The file.** The uploaded bytes are written unchanged to `trails/` in the data folder, under a
  random name, such as `trails/3f2c….gpx`. The name you picked is stored as text only, so it never
  becomes part of a path. The file is the source of truth: a later rule, such as photo alignment,
  reparses it and gets every point.
- **The row.** A `trail` row holds the numbers (start and end in UTC, distance, moving time, point
  count) and the simplified line as JSON. The line is each segment simplified with Douglas–Peucker
  to about 5 m, with the tolerance doubled until the trail has no more than 5,000 points. A trail
  of more than 20,000 points is first thinned to evenly spaced points, because Douglas–Peucker is
  slow on a long, jagged track.
- **What it belongs to.** A trail row points at the trip's **decision**, not at the trip row.
  Detection never deletes a decision, and a decision follows its trip when the fingerprint moves.
  When a trip has no decision yet, adding a trail creates one with no choice in it, as a rename
  does. Removing a trail deletes its row and its file.
- **The rules are pure.** Parsing, gap splitting, the summary, simplification, and the date check
  are pure functions in their own project, `Fibotrail.Trails`, tested with plain data.
- **Privacy.** Trail points stay on the server and the trip page. The MCP server reads no trail
  data ([ADR-023](ADR-023-the-mcp-server-may-return-a-trips-main-city.md)), and the map keeps the
  bundled base map ([ADR-009](ADR-009-a-bundled-natural-earth-base-map.md)).

## Alternatives considered

- **Every point in the database.** A `trail_point` table with one row a point. Set aside because a
  month of logging is millions of rows, the original file would still be wanted as the source of
  truth, and nothing in phase 1 queries single points.
- **Only the parsed data, no file.** Smaller, and one place to back up. Set aside because the
  design keeps the original untouched, and a parser fix could then never be applied to old trails.
- **A trail that belongs to the trip row.** Simplest to query. Set aside because a trip row can be
  deleted by a rescan, which would delete your trail with it.
- **A trail matched to a trip by its dates.** Every trail would show on whichever trip covers its
  time. Set aside because the spec lets you add a trail whose dates don't overlap the trip, after a
  warning, and because a trail between two trips would jump between them.

## Consequences

- The data folder gains a `trails/` folder. A backup of the data folder must include it. A trail
  row whose file is missing still draws, because the line is in the row.
- A trail survives a rescan, a container restart, and a move of the trip's fingerprint. When
  detection stops finding a trip, its decision and its trails stay, unseen, and come back if the
  trip does.
- When detection splits one trip into two, the decision, and so the trail, follows one of them.
  The other trip has no trail. Not known yet: how often this happens with real trips.
- The browser gets at most 5,000 points a trail, about 200 KB of JSON. Not measured yet: how
  MapLibre draws several such trails on an older iPhone.
- An upload is read into memory, up to 50 MB, before it is checked.
