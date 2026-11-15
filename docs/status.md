# Current status

## Current milestone

MVP step 2b, the scanner. Step 2a, the skeleton, is built. See
[roadmap.md](roadmap.md#mvp-version-01).

## Completed

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

## In progress

Nothing is in progress.

## Next

The MVP plan is in [roadmap.md](roadmap.md#mvp-version-01). Its seven specs are in
[specs/](specs/README.md), with work items [#5](https://github.com/bradreimer/fibotrail/issues/5) through [#11](https://github.com/bradreimer/fibotrail/issues/11).

- Run through the ADR-007 table on a real iPhone, with the app installed to the home screen. The
  app shell was checked in a desktop browser at phone and iPad sizes only, so this check is still
  open for [#6](https://github.com/bradreimer/fibotrail/issues/6).
- List five to ten known trips with rough dates, for the replay test.
- Decide the base map source, and record it as an ADR.
- Answer the remaining open questions in the
  [design and branding document](knowledge/design-and-branding.md#risks-open-questions-and-next-steps).
  The server language is settled: see [ADR-004](adr/ADR-004-technology-stack.md).
- Review the app icon. The icon and launch images in `apps/web/public` are drawn from the logo
  construction by `npm run icons`. A designed logo can replace the drawing in that script.
- Check that the name is free on GHCR, Docker Hub, and the App Store.
