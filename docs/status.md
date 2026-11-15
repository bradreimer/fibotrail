# Current status

## Current milestone

Phase 1, the spike. See [roadmap.md](roadmap.md).

## Completed

- [#1](https://github.com/bradreimer/fibotrail/pull/1) Design and branding document.
- [#2](https://github.com/bradreimer/fibotrail/issues/2) Project organization: instructions,
  work item templates, ADRs, specs, knowledge, and the tests that enforce the layout.
- [#3](https://github.com/bradreimer/fibotrail/issues/3) Project guidelines, the decisions on
  the stack, secrets, deployment, and the iOS experience, and the quality gates spec.

## In progress

Nothing yet.

## Next

The MVP plan is in [roadmap.md](roadmap.md#mvp-version-01). Its seven specs are in
[specs/](specs/README.md), with work items [#5](https://github.com/bradreimer/fibotrail/issues/5) through [#11](https://github.com/bradreimer/fibotrail/issues/11).

- Run the spike, [#12](https://github.com/bradreimer/fibotrail/issues/12): export sample metadata and confirm the date, GPS, and place
  fields.
- For the spike, create a read-only Immich API key with `asset.read`, `asset.view`, and `album.read`.
- List five to ten known trips with rough dates, for the replay test.
- Decide the base map source, and record it as an ADR.
- Answer the remaining open questions in the
  [design and branding document](knowledge/design-and-branding.md#risks-open-questions-and-next-steps).
  The server language is settled: see [ADR-004](adr/ADR-004-technology-stack.md).
- Produce the logo and icon files in SVG and PNG.
- Check that the name is free on GHCR, Docker Hub, and the App Store.
