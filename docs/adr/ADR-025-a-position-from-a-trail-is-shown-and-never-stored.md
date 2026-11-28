# ADR-025: A position from a trail is shown, and never stored

## Status

Accepted

## Context

[ADR-024](ADR-024-a-gps-trail-is-a-kept-file-that-belongs-to-a-trips-decision.md) keeps a trip's
GPS trails. Photo alignment, in the [GPS trails spec](../specs/gps-trails.md#photo-alignment),
places each trip photo on a trail by its time. For a photo with no position of its own, that gives
the only position Fibotrail has for it.

Three things hang on what Fibotrail does with that position:

- **It's an estimate.** It depends on an offset that is estimated, or set by you, and the offset
  can change at any time. A photo taken in a car between two logged points is placed on the
  straight line between them.
- **Detection reads positions.** Stops, the route, distance, parks, the main destination, and the
  life map all come from photo positions
  ([trip-detection.md](../specs/trip-detection.md),
  [ADR-013](ADR-013-park-days-are-derived-from-stored-photo-positions.md)). A position fed into
  them would change a trip's numbers each time an offset moved.
- **Immich is read-only** ([ADR-001](ADR-001-immich-is-read-only.md)), so the position can't go
  back to the photo.

## Decision

**A position from a trail is computed when the trip page asks for it, shown on the trip page and
its viewer, and never stored.**

```
trail file ──▶ points ──┐
                        ├──▶ alignment (on request) ──▶ dots on the trip map, the viewer's small map
trip photos ──▶ times ──┘        ▲
                                 └── offset: yours (stored on the trail row) or the estimate (not stored)
```

- The only new stored value is the offset you choose, on the trail row. It's a decision, so an
  estimate never replaces it.
- The estimate isn't stored. It's worked out from the trip's photos on each request, so it follows
  the photos after a rescan.
- A trail position never reaches the `photo` table, stops, distance, parks, the life map, trip
  detection, the MCP server, or Immich.
- A photo with no position of its own and a trail position is labeled "Estimated from the GPS
  trail" wherever it's shown.

## Alternatives considered

- **Store the position on the photo row.** Every screen would pick it up for free. Set aside
  because the `photo` table is detected data from Immich, rebuilt by a scan, and because a stored
  estimate would silently change stops and parks.
- **Feed estimated positions into detection.** Camera-only trips would get stops and a route. Set
  aside for this phase: it changes trip numbers each time an offset moves, and needs its own rules
  for how far to trust an estimate. It can come later, with its own ADR.
- **Store the estimate.** Faster page loads. Set aside because it goes stale when a rescan adds
  photos, and the estimate takes well under a second for a week of trail and 2,000 photos.

## Consequences

- Adding a trail, or changing its offset, never changes a trip's stops, distance, parks, or
  numbers.
- The trip page asks the server to align photos on every load. The server keeps the last 8 trail
  files it parsed in memory, so the cost is the estimate, not the file.
- Camera-only trips still have no stops from a trail. A later work item can decide to use trail
  positions there.
