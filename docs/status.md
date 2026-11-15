# Current status

## Current milestone

MVP step 3d, the release. Every work item of the MVP is built: steps 2a, 2b, 3a, 3b, and 3c.
**Two gates aren't met yet.** The gate of step 3a, the replay test on the real library, hasn't
run. Every UI work item still needs the ADR-007 run-through on a real iPhone. Both need the
maintainer's library and phone, and are listed under Next. See
[roadmap.md](roadmap.md#mvp-version-01).

## Completed

- Image publishing. After CI passes on a push to `main`,
  `.github/workflows/publish-image.yml` builds `docker/Dockerfile` for amd64 and arm64 and pushes
  `ghcr.io/bradreimer/fibotrail:latest` and `:sha-<commit>`. Not yet run on GitHub. The package
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

## In progress

- [#24](https://github.com/bradreimer/fibotrail/issues/24) The release, step 3d. The
  [release spec](specs/release.md) and the README are written, and a test keeps the README's
  sections, variables, and permissions in step with the code. The automated accessibility checks are in (contrast, alt text, accessible names, reduce motion, and
  the focus ring), and they found two failures that are fixed: text on Coral was 4.4:1, and the
  Gold focus ring was 2:1 on Paper. The manual checklist is written in
  [accessibility-checklist.md](knowledge/accessibility-checklist.md) and hasn't been run. The tag
  workflow and the license note are still open. The README's commands haven't been run against a
  real Immich yet.

## Next

The MVP plan is in [roadmap.md](roadmap.md#mvp-version-01). Its seven specs are in
[specs/](specs/README.md), with work items [#5](https://github.com/bradreimer/fibotrail/issues/5) through [#11](https://github.com/bradreimer/fibotrail/issues/11).

- Run through the ADR-007 table on a real iPhone, with the app installed to the home screen. Every
  screen was checked in a desktop browser at phone and iPad sizes only, with made-up touch events
  for the swipes. This check is still open for
  [#6](https://github.com/bradreimer/fibotrail/issues/6),
  [#9](https://github.com/bradreimer/fibotrail/issues/9),
  [#10](https://github.com/bradreimer/fibotrail/issues/10), and
  [#11](https://github.com/bradreimer/fibotrail/issues/11).
- Scan the real library once. The scanner was tested against a stand-in for Immich that answers
  in the shapes the spike recorded, and never against a real Immich server. The criteria "a full
  scan of the maintainer's library finishes" and "the Immich access log shows only requests on
  the allowed list" are still open for [#7](https://github.com/bradreimer/fibotrail/issues/7).
- Decide whether the generated Immich client needs a license note. See the last consequence in
  [ADR-008](adr/ADR-008-a-generated-immich-client-limited-to-allowed-reads.md).
- Run the replay test, which is the gate of step 3a. Put five to ten known trips in
  `/data/known-trips.json`, scan the real library, confirm the home location, and run
  `docker exec -it fibotrail fibotrail replay`. The format is in the
  [trip detection spec](specs/trip-detection.md#replay-command). The detector was tested with
  made-up photo sets only. The three open questions in that spec wait for this result.
- Answer the remaining open questions in the
  [design and branding document](knowledge/design-and-branding.md#risks-open-questions-and-next-steps).
  The server language is settled: see [ADR-004](adr/ADR-004-technology-stack.md).
- Review the app icon. The icon and launch images in `apps/web/public` are drawn from the logo
  construction by `npm run icons`. A designed logo can replace the drawing in that script.
- Check that the name is free on GHCR, Docker Hub, and the App Store.
