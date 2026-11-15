# Roadmap

Version 1 is built in six phases. A one-week spike comes first, because every later phase depends
on what Immich actually returns. The source for this plan is the
[design and branding document](knowledge/design-and-branding.md#roadmap-and-repository).

## Version 1 phases

| # | Phase | Scope | Gate to pass |
| --- | --- | --- | --- |
| 1 | Spike | Check Immich fields, keys, and time zones | Immich fields confirmed |
| 2 | Foundation | Repository and CI, Docker image, scanner, database | None |
| 3 | Trips | Detector, inbox, library, and trip page | Replay test finds your known trips |
| 4 | Maps | Route maps, life map, and boundaries | None |
| 5 | Collections | Disney parks, other sets, and stats | None |
| 6 | Release | PWA polish, accessibility, docs, v1.0 | Version 1.0 published |

The next phase starts only when the gate of the phase before it is met.

## Definition of done for version 1

- [ ] A fresh install connects to Immich, scans, and shows trip suggestions without manual setup
      beyond the API key.
- [ ] Adding or ignoring a suggestion works on phone and iPad, and the choice survives a rescan.
- [ ] The trip page, life map, Disney collection, and Stats screen all work with real data.
- [ ] The app passes the accessibility checks in the design and branding document.
- [ ] The README explains installation, API key permissions, and backup in under five minutes of
      reading.

## After version 1

Ideas for later releases, in rough order of value:

- Import real GPS tracks, for example from Dawarich or GPX files, for exact routes.
- A read-only login for a second household member.
- National parks and World Heritage collections.
- A yearly recap page, such as "Your year in trips."
- Home Assistant sensors that expose counts, such as Disney parks visited.
