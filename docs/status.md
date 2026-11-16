# Current status

## Current milestone

MVP step 3d, the release. Every work item of the MVP is built: steps 2a, 2b, 3a, 3b, and 3c. The
review gate of step 3a is met (2026-11-16). **One gate isn't met yet:** the ADR-007 run-through on
a real iPhone is partly done, and the manual accessibility checklist hasn't been run. Both need the
maintainer's phone, and are listed under Next. See [roadmap.md](roadmap.md#mvp-version-01).

## Completed

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

## In progress

- [#24](https://github.com/bradreimer/fibotrail/issues/24) The release, step 3d. Done: the
  [release spec](specs/release.md); the README, whose backup, restore, reset-password, and health
  commands were run on a real container on 2026-11-16; the automated accessibility checks (contrast,
  alt text, accessible names, reduce motion, and the focus ring), which found and fixed two
  failures; the generated client's license note (ADR-008); the name check (`fibotrail` is free on
  Docker Hub and the App Store, and `ghcr.io/bradreimer/fibotrail` pulls without signing in); and
  the tag workflow (`.github/workflows/release.yml`). The tag workflow hasn't run on GitHub: its
  first run is the `v0.1.0` tag. Still open: the rest of the iPhone run-through and the manual
  accessibility checklist, then the tag.

## Next

The MVP plan is in [roadmap.md](roadmap.md#mvp-version-01). Its specs are in
[specs/](specs/README.md).

- Finish the ADR-007 run-through on a real iPhone, with the app installed to the home screen. So
  far the Add trip sheet, the photo viewer, and the map overlays are confirmed. The rest of the
  table is open for [#6](https://github.com/bradreimer/fibotrail/issues/6),
  [#9](https://github.com/bradreimer/fibotrail/issues/9),
  [#10](https://github.com/bradreimer/fibotrail/issues/10), and
  [#11](https://github.com/bradreimer/fibotrail/issues/11).
- Run the manual lines of the [accessibility checklist](knowledge/accessibility-checklist.md) on the
  iPhone, including VoiceOver, the largest text size, reduce motion, the Coral badge, and the focus
  ring. A failed line is fixed, or recorded as a known gap, before the tag.
- Tag `v0.1.0` when both are done, and check that the release workflow publishes `:0.1.0` and
  `:latest` for amd64 and arm64.
- The idea of marking airports and layovers is [#32](https://github.com/bradreimer/fibotrail/issues/32).
- Answer the remaining open questions in the
  [design and branding document](knowledge/design-and-branding.md#risks-open-questions-and-next-steps).
  The server language is settled: see [ADR-004](adr/ADR-004-technology-stack.md).
- Review the app icon. The icon and launch images in `apps/web/public` are drawn from the logo
  construction by `npm run icons`. A designed logo can replace the drawing in that script.
