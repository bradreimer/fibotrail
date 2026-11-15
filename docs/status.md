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

These come from the next steps in the
[design and branding document](knowledge/design-and-branding.md#risks-open-questions-and-next-steps).
None has a work item yet.

- Answer the remaining open questions in the design and branding document. The server language
  is settled: see [ADR-004](adr/ADR-004-technology-stack.md).
- Create a read-only Immich API key with `asset.read`, `asset.view`, and `album.read`.
- Run the week-one spike: export sample metadata and confirm the date, GPS, and place fields.
- Produce the logo and icon files in SVG and PNG.
- Check that the name is free on GHCR, Docker Hub, and the App Store.
- Create the repository structure, CI, and the first Docker image.
