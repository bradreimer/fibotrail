# Architecture decision records

An architecture decision record (ADR) captures one significant architectural choice: what was
decided, why, and what follows from it. Write an ADR when a choice affects the direction of the
project or would be expensive to reverse.

## Rules

- One decision per file, named `ADR-NNN-short-title.md`, with a three-digit number and a
  lowercase, hyphenated title.
- Numbers run in order with no gaps. The next ADR takes the next number.
- The first line is `# ADR-NNN: Title`, and the number matches the file name.
- Every ADR has these sections, in this order: Status, Context, Decision, Alternatives
  considered, Consequences.
- Status is one of `Proposed`, `Accepted`, `Deprecated`, or `Superseded by ADR-NNN`.
- Don't rewrite an accepted ADR to change the decision. Write a new ADR and mark the old one
  superseded.
- List every ADR in the index below.

## Template

```markdown
# ADR-NNN: Title

## Status

Proposed

## Context

What situation forces a decision?

## Decision

What did we decide?

## Alternatives considered

What else did we weigh, and why did we set it aside?

## Consequences

What becomes easier, what becomes harder, and what must stay true?
```

## Index

| ADR | Decision | Status |
| --- | --- | --- |
| [ADR-001](ADR-001-immich-is-read-only.md) | Immich is read-only, and Fibotrail keeps its own database | Accepted |
| [ADR-002](ADR-002-decisions-are-stored-separately-from-detected-data.md) | Decisions are stored separately from detected data | Accepted |
| [ADR-003](ADR-003-collections-are-data-files.md) | Collections are data files | Accepted |
| [ADR-004](ADR-004-technology-stack.md) | C# backend and TypeScript web UI | Accepted |
| [ADR-005](ADR-005-secrets-come-from-the-environment.md) | Secrets come from the environment, starting with `.env` | Accepted |
| [ADR-006](ADR-006-one-container-on-the-immich-network.md) | One container on the Immich network | Accepted |
| [ADR-007](ADR-007-native-ios-feel-through-a-progressive-web-app.md) | Native iOS feel through a progressive web app | Accepted |
| [ADR-008](ADR-008-a-generated-immich-client-limited-to-allowed-reads.md) | A generated Immich client, limited to allowed reads and tested with recorded responses | Accepted |
| [ADR-009](ADR-009-a-bundled-natural-earth-base-map.md) | A bundled Natural Earth outline is the base map | Accepted |
| [ADR-010](ADR-010-stops-and-routes-are-stored-with-the-trip.md) | Stops and routes are stored with the trip, and decisions carry their dates | Accepted |
| [ADR-011](ADR-011-visits-are-derived-from-added-trips-on-the-server.md) | Visits are derived from added trips on the server | Accepted |
| [ADR-012](ADR-012-detail-regions-are-bundled-outlines-for-a-short-list-of-countries.md) | Detail regions are bundled outlines for a short list of countries | Accepted |
| [ADR-013](ADR-013-park-days-are-derived-from-stored-photo-positions.md) | Park days are derived from stored photo positions | Accepted |
| [ADR-014](ADR-014-stats-views-are-self-contained-modules.md) | Stats views are self-contained modules, joined only by their routes and a registry | Superseded by ADR-019 |
| [ADR-015](ADR-015-cities-are-merged-places-and-theme-parks-keep-their-own-identity.md) | Cities are merged places, and theme parks keep their own identity | Accepted |
| [ADR-016](ADR-016-a-join-is-a-decision-over-a-range-of-days.md) | A join is a decision over a range of days | Accepted |
| [ADR-017](ADR-017-a-manual-trip-is-a-decision-that-detection-builds-from-photos.md) | A manual trip is a decision that detection builds from photos | Accepted |
| [ADR-018](ADR-018-an-mcp-server-answers-questions-with-coarse-read-only-data.md) | An MCP server answers questions with coarse, read-only data | Accepted |
| [ADR-019](ADR-019-insights-replaces-the-stats-and-collections-tabs.md) | Insights replaces the Stats and Collections tabs | Accepted |
| [ADR-020](ADR-020-trip-page-maps-use-the-1-10-million-outline.md) | Trip page maps use the 1:10 million outline | Accepted |
| [ADR-021](ADR-021-a-pinned-photo-is-a-decision-that-detection-reads-as-a-day.md) | A pinned photo is a decision that detection reads as a day | Accepted |
| [ADR-022](ADR-022-games-are-plug-in-modules-behind-a-games-api.md) | Games are plug-in modules behind a games API | Accepted |
