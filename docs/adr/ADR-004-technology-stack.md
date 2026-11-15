# ADR-004: C# backend and TypeScript web UI

## Status

Accepted

## Context

Fibotrail runs as one Docker container next to Immich, on hardware such as a NAS, a mini PC, or a
Raspberry Pi. It serves a web app for phone and iPad. One person maintains it.

The design and branding document recommended TypeScript for both the server and the UI, and listed
the server language as an open question. The maintainer answered it: the backend is .NET (C#), and
the UI is a web app in TypeScript. This ADR replaces the stack table in the design document.

## Decision

The backend is C# on .NET. The UI is TypeScript.

| Layer | Choice | Why |
| --- | --- | --- |
| Backend | C# on the current .NET long-term support release, with ASP.NET Core minimal APIs | The maintainer's chosen backend stack |
| Database | SQLite in WAL mode, through Entity Framework Core | One file in the data volume, so backup is a file copy |
| Geometry | NetTopologySuite | Distance, clustering, and point-in-polygon in C# |
| Immich client | C# client generated from Immich's OpenAPI file | The Immich API changes between releases |
| Backend tests | xUnit | The default test framework for .NET |
| Backend format and lint | `dotnet format` with .NET analyzers, warnings as errors | One tool that ships with the SDK |
| Web UI | TypeScript, React, and Vite, built as a progressive web app | Fast on phone and iPad, and installs to the home screen |
| Maps | MapLibre GL JS with vector tiles | Open source and needs no account |
| UI tests | Vitest | Runs on the same Vite configuration as the app |
| UI format and lint | Prettier and ESLint | The standard pair for TypeScript |
| Boundaries | Natural Earth countries and regions | Public domain data for the world map |
| Packaging | One multi-architecture image (amd64 and arm64) on GHCR | Runs on a NAS, a mini PC, or a Raspberry Pi |

The backend serves the built UI as static files, so one process and one container serve
everything. The API contract between them is an OpenAPI document that the backend generates and
the UI's TypeScript types are generated from.

## Alternatives considered

- **TypeScript on Node.js for the backend.** The design document's recommendation. Server and UI
  would share types directly. Set aside because the maintainer wants a C# backend. The generated
  API types recover most of the benefit.
- **Python with FastAPI for the backend.** Listed in the design document as workable. Set aside
  for the same reason.
- **Blazor for the UI.** One language for everything. Set aside because the maintainer wants a
  TypeScript UI, and MapLibre GL JS is a JavaScript library.

## Consequences

- The repository has two apps: `apps/server` (a .NET solution) and `apps/web` (a Vite project).
  There is no `packages/` folder.
- The trip-detection rules live in their own C# project with no dependency on ASP.NET Core or on
  the database, so they can be tested with plain data and no running Immich.
- Each C# project has a matching test project.
- The Docker build has two stages: one builds the UI with Node.js, and one builds the backend with
  the .NET SDK. The final image holds the ASP.NET Core runtime only.
- The organization tests keep running on Node.js, which the UI needs anyway.
- `./scripts/check.sh` gains the .NET and UI steps when the first code of each stack arrives. See
  the [quality gates spec](../specs/quality-gates.md).
