# Release

Tracking issue: [#24](https://github.com/bradreimer/fibotrail/issues/24).

## Purpose

This spec defines what must be true before Fibotrail version 0.1 is published: the accessibility
checks pass, the README lets a stranger install and back up the app, and a `v0.1.0` tag produces a
versioned image for amd64 and arm64 on GHCR.

It covers MVP step 3d in the [roadmap](../roadmap.md#mvp-version-01). Step 3d is the gate
"version 0.1 published".

## User story

As a self-hoster with an Immich server, I want to install a tagged Fibotrail release by following
the README, so that I can run version 0.1 without reading the source.

## Goals

- A new reader can install, connect to Immich, and back up in under five minutes of reading.
- The accessibility rules in the
  [design and branding document](../knowledge/design-and-branding.md#accessibility) are checked
  by tests where a test can check them, and by a written checklist where it can't.
- A release is one tag. No manual build or push.

## Non-goals

- Screen-reader and text-size checks beyond the checklist. Version 1.0 revisits them.
- Docker Hub publishing or an App Store listing.
- Release notes automation.
- Upgrade and migration guides. Version 0.1 has no earlier release to upgrade from.

## Requirements

### Accessibility checks

The design document sets three rules. Each gets a check.

| Rule | Check | Kind |
| --- | --- | --- |
| Text and key controls meet WCAG 2.2 AA contrast (4.5:1 for body text, 3:1 for large text and controls) | A unit test computes the contrast ratio of every text and control color pair in the light and dark design tokens, and fails below the threshold | Automated |
| Every photo has alt text built from its date and place | A component test renders each screen that shows photos (inbox card, trip card, trip page timeline, photo viewer) and fails when an image has no `alt` | Automated |
| Controls are reachable by keyboard and have accessible names | A component test fails when a button, link, or input has no accessible name. The checklist covers tab order by hand | Automated and manual |
| The app works with a screen reader | A VoiceOver run-through of the five main screens on an iPhone | Manual |
| The app honors the system text size | The app at the largest iOS text size, with no clipped text on the five main screens | Manual |
| The app honors reduce-motion | A test confirms every animation is disabled under `prefers-reduced-motion: reduce` | Automated |

- The manual checks live in a checklist in `docs/knowledge/`, in the same format as the ADR-007
  run-through, with a date and a pass or fail for each line.
- A failed check is fixed or recorded as a known gap in the checklist before the tag.

### README

The README keeps its documentation and contributing sections and adds these, in this order:

1. **What Fibotrail is.** Two sentences, and the statement that Immich is never written to.
2. **Install.** The prerequisites (a running Immich, Docker), the steps from
   `docker/compose.example.yml`, and the first-run password.
3. **Configuration.** Every variable in `.env.example`, with its default and what it does.
4. **API key permissions.** The four read-only permissions: `asset.read`, `asset.view`,
   `album.read`, and `asset.statistics`, and a statement that no write permission is needed. It
   names the four requests Fibotrail makes, from
   [ADR-008](../adr/ADR-008-a-generated-immich-client-limited-to-allowed-reads.md).
5. **Backup.** Everything Fibotrail stores is in the `/data` volume. The README gives the commands
   to copy it, and states that the Immich library itself isn't in it and doesn't need Fibotrail's
   backup.
6. **Privacy.** Fibotrail makes no outside request by default.
7. **Updating and resetting.** Pulling a new image, and `fibotrail reset-password`.

- The README links to the docs map, and no section duplicates a spec.
- Every command in the README is one that has been run.

### Release workflow

- Pushing a tag that matches `v*.*.*` builds `docker/Dockerfile` for linux/amd64 and linux/arm64
  and pushes `ghcr.io/bradreimer/fibotrail:<version>` (without the `v`) and `:latest`.
- `:latest` means the newest release, and only a release tag pushes it. Every commit on `main` that
  passes CI pushes `:edge` and `:sha-<commit>` instead, for testing. `:edge` can be broken, and
  production runs `:latest` or a pinned `:<version>`.
- A tag build runs only when CI passed on the tagged commit.
- The existing publish on every `main` commit keeps working.
- The image carries the version, so the app can show it. `/healthz` or the settings screen reports
  it.
- The GHCR package is public, so a stranger can pull it without signing in.

### Gates before the tag

The tag is created only when these are met. They need the maintainer's library and phone.

- The review gate is met: at least 8 of the first 10 High-confidence suggestions are accepted, and
  at most 1 of 3 to 5 remembered trips is missing
  ([trip detection](trip-detection.md#review-gate)).
- A full scan of the real library finishes, and the Immich access log shows only the four allowed
  requests ([immich scanning](immich-scanning.md)).
- The ADR-007 run-through passes on an iPhone for the app shell, inbox, library, and trip page.

### Small checks

- The generated Immich client's license is checked, and a note is added if it's required, as
  [ADR-008](../adr/ADR-008-a-generated-immich-client-limited-to-allowed-reads.md) asks.
- The name `fibotrail` is confirmed free on GHCR and Docker Hub, or the result is recorded. The
  App Store check is the maintainer's.

## Acceptance criteria

- `./scripts/check.sh` runs the contrast, alt text, accessible name, and reduce-motion checks, and
  each fails when its rule is broken.
- The accessibility checklist exists, with a date and a result for every line.
- The README has the seven sections above, and a test fails when one is missing.
- On a machine with Docker and an Immich server, following the README from the top gives a
  working sign-in page and a first scan.
- The README's API key section matches the allowed requests list in the code.
- Pushing `v0.1.0` publishes `:0.1.0` and `:latest` for amd64 and arm64, and `docker pull` works
  without signing in.
- A commit on `main` moves `:edge` and leaves `:latest` where it was.
- The three gates before the tag are recorded as met in [status.md](../status.md).

## Open questions

- Where the version shows in the app: `/healthz` only, or the settings screen too. The default is
  `/healthz` only.
- Whether the checklist for manual accessibility checks needs an iPad pass as well as an iPhone
  pass. The default is iPhone only.
