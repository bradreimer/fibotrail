# ADR-001: Immich is read-only, and Fibotrail keeps its own database

## Status

Accepted

## Context

Fibotrail builds trips, places, collections, and statistics from the photos in an Immich library.
That library is personal and hard to replace. The app is a guest in it.

The design and branding document sets "Immich stays untouched" as a design principle: Fibotrail
never edits, moves, or deletes your photos.

## Decision

Fibotrail only reads from Immich. It never writes to Immich.

- Fibotrail uses a dedicated Immich API key that has read permissions only: `asset.read`,
  `asset.view`, and `album.read`.
- Everything Fibotrail creates lives in its own database in the `/data` volume: trips, places,
  visits, collections, decisions, and settings.
- Fibotrail stores photo IDs, timestamps, coordinates, and place names. It doesn't copy original
  photos.
- The browser talks only to the Fibotrail server. The server fetches thumbnails on demand, so the
  browser never sees the Immich key.

## Alternatives considered

- **Store trips in Immich as albums or tags.** This would make trips visible in Immich, but it
  needs write access to the library and makes Immich responsible for Fibotrail's state.
- **Copy photos into Fibotrail.** This would remove the runtime dependency on Immich, but it
  doubles storage and makes Fibotrail a second photo library to back up.

## Consequences

- Removing Fibotrail removes nothing from Immich.
- A leaked or misused API key can't change the library.
- Detected data can be rebuilt from Immich at any time, so a damaged database is recoverable.
- Trips aren't visible inside Immich. A person opens Fibotrail to see them.
- Any code path that sends a write request to Immich is a defect against this ADR.
