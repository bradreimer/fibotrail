# ADR-004: Technology stack

## Status

Proposed

## Context

Fibotrail runs as one Docker container next to Immich, on hardware such as a NAS, a mini PC, or a
Raspberry Pi. It serves a web app for phone and iPad. One person maintains it.

The design and branding document recommends a stack and lists the server language as an open
question. This ADR stays proposed until that question is answered.

## Decision

Use the stack that the design and branding document recommends.

| Layer | Choice | Why |
| --- | --- | --- |
| Server | TypeScript on Node.js with Fastify | One language for server and UI, with shared types |
| Web UI | React and Vite, installable as a progressive web app | Fast on phone and iPad, and installs to the home screen |
| Maps | MapLibre GL JS with vector tiles | Open source and needs no account |
| Geometry | Turf.js | Distance, clustering, and point-in-polygon in code |
| Database | SQLite in WAL mode with Drizzle ORM | One file in the data volume, so backup is a file copy |
| Boundaries | Natural Earth countries and regions | Public domain data for the world map |
| Packaging | Multi-architecture image (amd64 and arm64) on GHCR | Runs on a NAS, a mini PC, or a Raspberry Pi |

## Alternatives considered

- **Python with FastAPI for the server.** This would also work. It splits the project into two
  languages, so the server and UI can't share types.

## Consequences

- The repository is one workspace with `apps/server`, `apps/web`, `packages/core`, and
  `packages/immich-client`.
- The trip-detection rules live in `packages/core` as pure functions, so they can be tested with
  plain data and no running Immich.
- The Immich client is generated from Immich's OpenAPI file.
- The organization tests already run on Node.js, so they need no change if this ADR is accepted.
