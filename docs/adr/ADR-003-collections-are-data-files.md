# ADR-003: Collections are data files

## Status

Accepted

## Context

Collections, such as "Disney parks," are what make Fibotrail feel like a stamp book. People will
want collections the project never thought of, and the list of places in a collection changes
over time, for example when a new park opens.

Some places sit side by side. Disneyland Park and Disney California Adventure are close enough
that two circles around their centers would overlap.

## Decision

A collection is a data file, not code.

- Each collection is one YAML file in the `collections/` folder. It lists places and a matching
  rule (`min_photos` and `buffer_m`).
- Each place has a boundary polygon in a GeoJSON file. Fibotrail matches a photo to a place when
  the photo's position falls inside the boundary, allowing the buffer.
- A place counts as visited when the number of matching photos in a trip reaches `min_photos`.
- Adding a collection needs no code change.

## Alternatives considered

- **Define collections in code.** This allows custom logic for each collection, but every new
  collection would need a release.
- **Match places with a center point and a radius.** This is simpler to author, but neighboring
  places overlap and photos would match the wrong place.

## Consequences

- Anyone can add a collection by writing a YAML file and its boundaries.
- The collection file format is a public contract. A change to it needs a spec.
- Boundary data comes from OpenStreetMap and Natural Earth, so the app must show the required
  attribution.
- The app builds a spatial index of every boundary at startup.
