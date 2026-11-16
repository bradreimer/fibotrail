# Current status

## Current milestone

Version 0.2.0 was tagged and published on 2026-11-16: the Map (phase 4, countries only), hiding a
photo from a trip's map, and the new navigation. The release workflow's second run succeeded:
`ghcr.io/bradreimer/fibotrail:0.2.0` and `:latest` are the same image, for linux/amd64 and
linux/arm64, and `:0.1.0` is unchanged. See [roadmap.md](roadmap.md) and
[the release](https://github.com/bradreimer/fibotrail/releases/tag/v0.2.0).

**What wasn't recorded before the tag:** the maintainer checked the Map in a desktop browser, where
it looked right after the antimeridian fix. The Map on a phone and an iPad, the top bar buttons and
the sidebar, and the **Hide** checkbox on a device weren't reported. The ADR-007 table lines for
version 0.1 and the per-line results of the accessibility checklist are also still unrecorded.

## Completed

- [#61](https://github.com/bradreimer/fibotrail/issues/61) Trips with no location. Rule 3b finds
  runs of 3 or more days with 30 or more photos a day and no position, which catches March 2006,
  March 2007, and March 2012. On the maintainer's library it adds about 29 Low confidence
  suggestions, and how many are real isn't known yet.

- Image publishing. After CI passes on a push to `main`,
  `.github/workflows/publish-image.yml` builds `docker/Dockerfile` for amd64 and arm64 and pushes
  `ghcr.io/bradreimer/fibotrail:edge` and `:sha-<commit>`, for testing. `:latest` is for releases
  only. The first run was on 2026-11-15, before `edge` replaced `latest`, so `:latest` is a stale
  build of `main` until `v0.1.0` is tagged. The package
  starts private; make it public in the package settings to pull without signing in.
- [#1](https://github.com/bradreimer/fibotrail/pull/1) Design and branding document.
- [#2](https://github.com/bradreimer/fibotrail/issues/2) Project organization: instructions,
  work item templates, ADRs, specs, knowledge, and the tests that enforce the layout.
- [#3](https://github.com/bradreimer/fibotrail/issues/3) Project guidelines, the decisions on
  the stack, secrets, deployment, and the iOS experience, and the quality gates spec.
- [#12](https://github.com/bradreimer/fibotrail/issues/12) The spike. The gate "Immich fields
  confirmed" is met. The findings are in
  [immich-api-findings.md](knowledge/immich-api-findings.md), from Immich 3.4.1 on 2026-11-15,
  with a read-only key.
- [#5](https://github.com/bradreimer/fibotrail/issues/5) First run and sign-in. The .NET
  solution, the SQLite database, Argon2id password setup, sessions, the sign-in delay,
  `fibotrail reset-password`, the Dockerfile, and an example compose file. The setup and sign-in
  screens stay plain HTML from the server, so they work before a session exists.
- [#6](https://github.com/bradreimer/fibotrail/issues/6) The app shell. The React and Vite
  project in `apps/web`, the design tokens, self-hosted Fraunces and Inter, the three
  destinations, the four screen states, the web app manifest, icons, launch images, and the
  service worker. The server serves the built app, and the Docker image includes it. The UI
  checks run in `./scripts/check.sh`. How to work on it is in
  [web-app-development.md](knowledge/web-app-development.md).
- [#7](https://github.com/bradreimer/fibotrail/issues/7) Immich scanning. The generated Immich
  client with its four allowed reads, the connection check, the paged scanner that resumes from
  its cursor and reads only changes after the first run, the `photo` and `scan_run` tables, the
  thumbnail proxy and its cache, **Scan now**, and the progress banner. See
  [ADR-008](adr/ADR-008-a-generated-immich-client-limited-to-allowed-reads.md).
- [#8](https://github.com/bradreimer/fibotrail/issues/8) Trip detection. The rules as pure
  functions in `Fibotrail.Detection`, the home location step with its map, the `trip`,
  `trip_photo`, `decision`, and `setting` tables, reconciliation that keeps decisions across
  rescans, and the `fibotrail replay` command. See
  [ADR-009](adr/ADR-009-a-bundled-natural-earth-base-map.md) and
  [ADR-010](adr/ADR-010-stops-and-routes-are-stored-with-the-trip.md).
- [#9](https://github.com/bradreimer/fibotrail/issues/9) The trip inbox. Suggestion cards with
  a photo strip and a route preview, **Add trip** with a sheet for the name and the cover,
  **Not a trip**, **Later**, swipe on touch screens, the **Ignored** tab with **Restore**, and
  the badge on the Inbox destination.
- [#10](https://github.com/bradreimer/fibotrail/issues/10) The trip library. A grid of trip
  cards with a cover photo, the name, the dates, the day count, and the main destination, newest
  first, in 1, 2, or 3 columns by width, with the loading, empty, and error states.
- [#11](https://github.com/bradreimer/fibotrail/issues/11) The trip page. The header, the
  route map with pins and the stop sheet, the five stat tiles, the photo timeline by local day,
  the photo viewer with swipe, arrow keys, and **Open in Immich**, and **Rename or change
  cover**.

- [#31](https://github.com/bradreimer/fibotrail/issues/31) The main destination by days. A trip's
  main place is the city where the most days were spent, not the most photos. See the
  [main destination spec](specs/main-destination.md). A choice of names
  ([#30](https://github.com/bradreimer/fibotrail/issues/30)) was set aside on 2026-11-16: a name
  can already be typed, and a better first guess matters more.
- [#28](https://github.com/bradreimer/fibotrail/issues/28) The data folder check. A `/data` bind
  mount that the container can't write to now stops the server with one line that names the fix,
  not a repeating SQLite stack trace.
- [#36](https://github.com/bradreimer/fibotrail/issues/36) The README's backup and restore steps,
  fixed after running them on a real container. Builds of `main` report `edge-<commit>` on
  `/healthz`.
- [#38](https://github.com/bradreimer/fibotrail/issues/38) On an iPhone, the Add trip sheet was
  under the tab bar and **Add** was hidden. The cause was `-webkit-overflow-scrolling: touch` on the
  scroll area. It's removed, with a test, and the maintainer confirmed on the phone that the Add
  sheet, the photo viewer, and the map overlays now cover the tab bar.
- The review gate of step 3a, met on 2026-11-16 on the maintainer's library of 71,318 photos: 10 of
  the first 10 High-confidence suggestions accepted, and 5 of 5 remembered trips found. It replaced
  the replay test as the gate. See the [trip detection spec](specs/trip-detection.md#review-gate).
- The first real scan, on 2026-11-15. A full scan of the 71,318 photos finished, and detection
  found 34 suggestions. The Immich access log was checked for an incremental scan and for browsing
  thumbnails: only `GET /api/server/version`, `POST /api/search/metadata`, and
  `GET /api/assets/{id}/thumbnail` appeared, with no other path and no write method. The first full
  scan ran before logging was on, so `GET /api/assets/statistics` (read only at the start of a full
  scan) wasn't seen.

- Version 0.1.0, tagged on 2026-11-16 at `d7e0970`. The `v0.1.0` tag ran the release workflow for the
  first time. It checked the tag, the commit on `main`, and its CI run, then pushed `:0.1.0` and
  `:latest`. The image carries `FIBOTRAIL_VERSION=0.1.0`, the OCI version label, and runs as user
  1654. The maintainer's run of the manual accessibility checklist on an iPhone passed, apart from
  the tile shapes in the trip timeline
  ([#41](https://github.com/bradreimer/fibotrail/issues/41), fixed and confirmed).

- Version 0.2.0, tagged on 2026-11-16 at `d17f13c`. It adds the Map
  ([#45](https://github.com/bradreimer/fibotrail/issues/45), with the server side in
  [#46](https://github.com/bradreimer/fibotrail/issues/46) and
  [ADR-011](adr/ADR-011-visits-are-derived-from-added-trips-on-the-server.md)), hiding a photo from
  a trip's map ([#35](https://github.com/bradreimer/fibotrail/issues/35), see
  [on-the-way.md](specs/on-the-way.md)), and the navigation change
  ([#47](https://github.com/bradreimer/fibotrail/issues/47)): Trips and Map in the tab bar, Inbox and
  Settings in the top bar under 600 px, and all four in the sidebar from 600 px. The first look at the
  Map found outlines that cross the antimeridian covering the sea and matching stops to Russia
  ([#55](https://github.com/bradreimer/fibotrail/issues/55)), fixed before the tag. The image
  carries `FIBOTRAIL_VERSION=0.2.0` and runs as user 1654. On the maintainer's upgrade, Fibotrail
  copied the database to `fibotrail-backup-<time>.db` before applying the new table.

- [#60](https://github.com/bradreimer/fibotrail/issues/60) Measuring the on-the-way stop rule, the
  first step of [#32](https://github.com/bradreimer/fibotrail/issues/32). `fibotrail stops` reports
  which stops of each added trip the candidate rule flags, and why, and changes nothing. It was
  merged on 2026-11-16 and is tested on made-up trips only. It hasn't been run on the maintainer's
  library yet, and the rule's thresholds stay untuned until it is. See
  [on-the-way.md](specs/on-the-way.md#measuring-the-rule).

## In progress

Nothing is in progress.

## Next

The plan is in [roadmap.md](roadmap.md). Specs are in [specs/](specs/README.md).

- Record what hasn't been checked on devices: the Map, the top bar buttons, the sidebar, and **Hide**
  on a phone and an iPad, and the rest of the ADR-007 table for
  [#6](https://github.com/bradreimer/fibotrail/issues/6),
  [#9](https://github.com/bradreimer/fibotrail/issues/9),
  [#10](https://github.com/bradreimer/fibotrail/issues/10), and
  [#11](https://github.com/bradreimer/fibotrail/issues/11). A failed line is fixed in a patch
  release, or recorded as a known gap. Also record the per-line results of the
  [accessibility checklist](knowledge/accessibility-checklist.md).
- Zoom the life map to regions and cities
  ([#48](https://github.com/bradreimer/fibotrail/issues/48)). It needs boundary data below country
  level and a decision on how cities are named and counted, so it starts with an ADR.
- Make the switches look like iOS switches: **Hide from map** in the photo viewer
  ([#52](https://github.com/bradreimer/fibotrail/issues/52)) and **Show routes** on the life map,
  and keep the photo viewer dark in dark mode
  ([#51](https://github.com/bradreimer/fibotrail/issues/51)). Built and checked in a desktop browser
  at phone size, not yet on a phone.
- Suggest stops that are on the way ([#32](https://github.com/bradreimer/fibotrail/issues/32)), the
  second part of the idea in [on-the-way.md](specs/on-the-way.md). Its rule needs real examples
  first, which `fibotrail stops` ([#60](https://github.com/bradreimer/fibotrail/issues/60)) produces.
- Phase 5: collections, such as Disney parks, and the Stats screen. The tab bar then gains
  Collections and Stats, per ADR-007.
- Answer the open questions in the [life map spec](specs/life-map.md) with real use: whether the
  home country should always fill in, and whether 25 km is the right distance for a stop just off a
  coast.
- Answer the remaining open questions in the
  [design and branding document](knowledge/design-and-branding.md#risks-open-questions-and-next-steps).
  The server language is settled: see [ADR-004](adr/ADR-004-technology-stack.md).
- Review the app icon. The icon and launch images in `apps/web/public` are drawn from the logo
  construction by `npm run icons`. A designed logo can replace the drawing in that script.
