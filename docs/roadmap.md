# Roadmap

This document states what gets built and in what order. Version 1 is built in six phases. The
first three phases produce the MVP, version 0.1: the smallest Fibotrail worth running. A one-week
spike comes first, because every later phase depends on what Immich actually returns.

The source for the version 1 phases is the
[design and branding document](knowledge/design-and-branding.md#roadmap-and-repository). The MVP
plan below was added on 2026-11-15. It moves the trip route map from phase 4 into phase 3, because
the trip page needs it.

## Version 1 phases

| # | Phase | Scope | Gate to pass |
| --- | --- | --- | --- |
| 1 | Spike | Check Immich fields, keys, and time zones | Immich fields confirmed |
| 2 | Foundation | Repository and CI, Docker image, scanner, database | None |
| 3 | Trips | Detector, inbox, library, and trip page with its route map | Replay test finds your known trips, then version 0.1 published |
| 4 | Maps | Life map and boundaries | None |
| 5 | Collections | Disney parks, other sets, and stats | None |
| 6 | Release | PWA polish, accessibility, docs, v1.0 | Version 1.0 published |

The next phase starts only when the gate of the phase before it is met.

## MVP: version 0.1

The MVP proves one thing: Fibotrail can find your real trips in your Immich library, and you
accept most High-confidence suggestions without edits. It covers phases 1 through 3.

Think of it as the frame of a house. The foundation, the walls, and one finished room (trips) come
first. The life map, collections, and stats are rooms added later on the same foundation.

### User stories

Each story becomes part of a spec in [specs/](specs/README.md), and each spec gets work items in
[GitHub Issues](https://github.com/bradreimer/fibotrail/issues). None exists yet.

| ID | As a user, I can… | Planned spec | Step |
| --- | --- | --- | --- |
| U1 | Run Fibotrail in Docker next to Immich and set a password on first run | `first-run-and-sign-in` | 2a |
| U2 | Connect to Immich with a read-only API key, and my library is never changed | `immich-scanning` | 2b |
| U3 | Confirm my home location so Fibotrail knows when I'm away | `trip-detection` | 3a |
| U4 | Scan my library and see progress while the app stays usable | `immich-scanning` | 2b |
| U5 | Review suggested trips in an inbox and choose **Add trip**, **Not a trip**, or **Later** | `trip-inbox` | 3b |
| U6 | Trust that my choices survive every rescan | `trip-detection` and `trip-inbox` | 3a, 3b |
| U7 | Browse my added trips as cards with a cover photo, dates, and place | `trip-library` | 3c |
| U8 | Open a trip and see a route map, key numbers, and photos grouped by day | `trip-page` | 3c |
| U9 | Tap a photo to view it and open the original in Immich | `trip-page` | 3c |
| U10 | Do all of this comfortably on my phone or iPad | `app-shell` | 2a |

### Steps

```text
Phase 1      Phase 2                    Phase 3
1 Spike -◆-> 2a Skeleton -> 2b Scanner -> 3a Detector -◆-> 3b Inbox -> 3c Trips -> 3d Release -◆
         |                                            |                                       |
   Immich fields                              Replay test finds                         Version 0.1
     confirmed                                your known trips                           published
```

Each diamond is a gate. The next step starts only when its criterion is met.

| Step | What gets built | Done when |
| --- | --- | --- |
| 1 Spike | A throwaway script that pages through `POST /api/search/metadata` with a read-only key and saves a sample. A knowledge document that records the field names for capture time, time zone, GPS, and place names, how to request only changed photos, the share of photos with no GPS, and the tested Immich version | **Gate:** Immich fields confirmed |
| 2a Skeleton | The .NET solution in `apps/server` and the Vite project in `apps/web`, with the checks from the [quality gates spec](specs/quality-gates.md). SQLite through Entity Framework Core, with migrations at startup. First-run password and sign-in per [ADR-005](adr/ADR-005-secrets-come-from-the-environment.md). The app shell with the design tokens, self-hosted fonts, tab bar, and the behaviors in [ADR-007](adr/ADR-007-native-ios-feel-through-a-progressive-web-app.md). The two-stage Docker image, `/healthz`, and an example compose file | `docker compose up` shows the sign-in page, the app installs to an iPhone home screen, and CI is green |
| 2b Scanner | The Immich client generated from the OpenAPI file. A connection check with plain error messages. A timeout for each Immich request, set by `IMMICH_TIMEOUT_SECONDS` (30 by default), with a retry and a plain message when Immich is slow. A paged scanner that resumes from a cursor and is incremental after the first run. The `photo` and `scan_run` tables. A **Scan now** button and the progress banner. The thumbnail proxy and cache | A full scan of your library finishes, a second scan reads only new photos, and no request writes to Immich |
| 3a Detector | Detection steps 1 through 10 of the design document, as pure functions in their own C# project, with default settings. The home location suggestion. Stops, route, name, confidence, and fingerprint. Reconciliation with decisions per [ADR-002](adr/ADR-002-decisions-are-stored-separately-from-detected-data.md). The `trip`, `trip_photo`, `decision`, and `setting` tables. A replay command that compares the output with a list of your known trips | **Gate:** the replay test finds your known trips, and a rescan never undoes a decision |
| 3b Inbox | Suggestion cards with dates, places, photo count, a photo strip, and a route preview. **Add trip**, **Not a trip**, and **Later**, with swipe on touch screens. The **Ignored** tab with restore. Rename and cover photo selection when you add a trip. The inbox badge | You can clear your inbox on a phone, and a rescan brings nothing back |
| 3c Trips | The trip library grid, sorted by date. The trip page: header, route map, stat tiles (days, photos, stops, distance, farthest from home), and the photo timeline by day. The photo viewer with swipe and an **Open in Immich** link built from `IMMICH_EXTERNAL_URL`, the address your browser uses, which can differ from the `IMMICH_URL` the container uses | The trip library and trip page work with your real data on phone and iPad |
| 3d Release | The accessibility checks from the design document. The README sections for installation, API key permissions, and backup. The `v0.1.0` image for amd64 and arm64 on GHCR | **Gate:** version 0.1 published |

### Deferred until after the MVP

| Item | Why it can wait |
| --- | --- |
| Life map and world map fill-in (phase 4) | Needs the `place` and `visit` tables, which nothing in the MVP reads |
| Collections, including Disney parks (phase 5) | Depends on confirmed trips and on boundary data |
| Stats page (phase 5) | The trip page tiles cover the numbers that matter first |
| Merge, split, change dates, and hand-made trips | Corrections matter once detection is proven |
| Library filters by year, country, and collection | Sorting by date is enough for a first library |
| Scheduled scans and detection rule settings | A manual scan with the default rules is enough to test detection |
| Signature animations and `/metrics` | Polish for version 1 |
| Day trips | Off by default in the design document |

### Decisions the MVP still needs

Each one is recorded as an ADR when it's made.

| Decision | Recommendation | Needed by |
| --- | --- | --- |
| Base map source | Bundle a Natural Earth outline as the default, with an optional tile address in Settings. This keeps the "no outside calls" promise, and the life map can reuse it | 3b |
| Where stops and routes are stored | Compute them when a trip is detected and store them with the trip, so the trip page doesn't recompute them | 3a |
| How CI tests the Immich contract | Use recorded responses from the spike for the MVP, and add a test against a real Immich for version 1 | 2b |

### Definition of done for the MVP

- [ ] A fresh install connects to Immich, scans, and shows trip suggestions with no setup beyond
      the `.env` file, the password, and the home location.
- [ ] Adding or ignoring a suggestion works on phone and iPad, and the choice survives a rescan.
- [ ] The trip library and trip page work with your real data.
- [ ] The replay test finds your known trips.
- [ ] Fibotrail makes no write request to Immich and no outside request by default.
- [ ] The app passes the iPhone run-through in ADR-007.

### Risks

| Risk | Effect on the MVP | Response |
| --- | --- | --- |
| Immich returns different fields than expected | The scanner and detector need rework | Step 1 confirms the fields before anything is built on them |
| Many photos have no GPS | Trips are missed or look thin | Attach photos by date, and report the count from the spike |
| Detection misses or splits known trips | You lose trust in the inbox | Tune the defaults against the replay test before the inbox is built |
| An Immich request hangs | A scan stalls with no message | Every request has a timeout, and the scan resumes from the cursor |
| The first scan is slow on a large library | Poor first impression | Scan in batches, show progress, and resume from the cursor |

### What the MVP needs from the maintainer

1. A read-only Immich API key in `.env` and the Immich version, for the spike.
   `IMMICH_EXTERNAL_URL` in `.env` is needed by step 3c.
2. A list of five to ten known trips with rough dates, for the replay test.
3. Where Immich runs (for example, Unraid, TrueNAS, or plain Docker), so the compose example
   matches.

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
