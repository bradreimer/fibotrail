# Specs

A spec states what a capability must do, before anyone builds it. Write a spec when a change
affects user-facing behavior, workflow design, or cross-cutting architecture.

## Rules

- One capability per file, named `short-title.md` in lowercase with hyphens.
- The first line is `# Title`. The next paragraph is a `Tracking issue:` line that links to the
  GitHub Issue that tracks the implementation.
- Every spec has these sections, in this order: Purpose, User story, Goals, Non-goals,
  Requirements, Acceptance criteria, Open questions.
- A spec with no tracking issue is dead weight. Open the issue that implements it, or remove the
  spec.
- Keep a spec in sync with what's implemented.
- List every spec in the index below.

## New ideas belong in GitHub Issues

An idea that isn't scoped yet belongs in a
[GitHub Issue](https://github.com/bradreimer/fibotrail/issues) with the Feature Request template,
not in a new file here. Write a spec only when there's a concrete capability to scope for
implementation.

## Template

```markdown
# Title

Tracking issue: [#123](https://github.com/bradreimer/fibotrail/issues/123).

## Purpose

Why does this capability exist?

## User story

As a <person>, I want <capability>, so that <benefit>.

## Goals

## Non-goals

## Requirements

## Acceptance criteria

## Open questions

None.
```

## Index

| Spec | Capability |
| --- | --- |
| [project-organization.md](project-organization.md) | Where every file belongs, and the tests that enforce it |
| [quality-gates.md](quality-gates.md) | The checks every check-in must pass, and how they're run |
| [first-run-and-sign-in.md](first-run-and-sign-in.md) | Starting the container, setting a password on first run, and signing in |
| [app-shell.md](app-shell.md) | Home-screen installation, navigation, design tokens, and the four screen states |
| [immich-scanning.md](immich-scanning.md) | Reading the Immich library with a read-only key, scan progress, and the thumbnail proxy |
| [trip-detection.md](trip-detection.md) | The home location, the rules that turn photos into trips, and decisions that survive rescans |
| [trip-inbox.md](trip-inbox.md) | Reviewing suggested trips with Add trip, Not a trip, and Later |
| [trip-library.md](trip-library.md) | The grid of cards for the trips you've added |
| [trip-page.md](trip-page.md) | One trip's route map, stat tiles, photo timeline, and photo viewer |
| [life-map.md](life-map.md) | The Map screen: a world map of visited countries, a summary, first visits, and routes |
| [main-destination.md](main-destination.md) | How a trip's main destination is chosen: the city with the most days |
| [on-the-way.md](on-the-way.md) | Photos and stops that belong to a trip but not to its map: hide a photo, and a set-aside idea for suggested stops |
| [collections.md](collections.md) | Collections of places, such as Disney and Universal parks, with a stamp, days, and trips for each place |
| [insights.md](insights.md) | The Insights screen: six travel totals above the collections, and the stats library behind them |
| [games.md](games.md) | The Games tab, the games API, results, stats, and sharing |
| [timeline-game.md](timeline-game.md) | The Timeline game: put five of your photos in date order |
| [slide-game.md](slide-game.md) | The Slide game: slide the tiles of a trip photo back into place |
| [pin-game.md](pin-game.md) | The Pin game: tap the map where you took one of your photos |
| [fetch-game.md](fetch-game.md) | The Fetch game: fly a schnauzer along one of your trips and catch its photos |
| [gps-trails.md](gps-trails.md) | Adding a GPX trail from a GPS logger to a trip, and drawing it on the trip map |
| [stamps-game.md](stamps-game.md) | The Stamps game: place one stamp in each row, column, and trip, with no two touching |
| [daily-set.md](daily-set.md) | The day's games as one set: a progress ring, Share today, and a shared end screen |
| [mcp-server.md](mcp-server.md) | A read-only MCP server that answers questions about your travel with coarse data and a token |
| [release.md](release.md) | The accessibility checks, README, and tagged image for version 0.1 |
