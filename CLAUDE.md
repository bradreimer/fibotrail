# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this project is

Fibotrail is a self-hosted web app that turns an Immich photo library into a travel journal. It
finds trips, maps them, and counts them. Immich supplies photos and GPS. Fibotrail adds trips,
places, collections, and insights on top.

Core principle: **Immich stays untouched.** Fibotrail only reads from Immich and keeps its own
state in its own database. See
[docs/adr/ADR-001-immich-is-read-only.md](docs/adr/ADR-001-immich-is-read-only.md).

```
Immich → Scanner → Trip detector → Database → Web server → Phone or iPad
```

The full picture is in
[docs/knowledge/design-and-branding.md](docs/knowledge/design-and-branding.md).

## Project guidelines

Seven guidelines apply to every change. The full text, with the method for each, is in
[docs/guidelines.md](docs/guidelines.md).

1. **Be plain.** Every document and design states its intention and its methodology. Name the
   mechanism, use numbers, and say what isn't known yet.
2. **Stay organized.** The project is always in a cleanly organized state. Stop and reorganize
   when the layout stops fitting, as its own work item.
3. **Protect secrets.** Secrets come from environment variables, starting with `.env`. Never
   commit, log, or send a secret to the browser.
4. **C# backend, TypeScript UI.** Backend functionality is .NET in `apps/server`. The UI is a
   TypeScript web app in `apps/web`.
5. **Docker next to Immich.** The app runs as one Docker container on the Immich network.
6. **Native iOS feel.** On a phone, the app must feel like a native iOS app. Check UI changes
   against the table in ADR-007.
7. **Tested, formatted, linted.** New functionality comes with a unit test. Every check-in passes
   `./scripts/check.sh`.

## Spec-driven, work-item-driven development

This workflow is required. Follow it before you write code for any meaningful change:

1. Check [docs/specs/](docs/specs/) for a spec that covers the behavior. If none exists for a
   user-facing or cross-cutting change, write one first. The template is in
   [docs/specs/README.md](docs/specs/README.md).
2. Check [GitHub Issues](https://github.com/bradreimer/fibotrail/issues) for an
   implementation-sized work item. If none exists, create one with the template that matches:
   - **User Story** for a scoped, user-facing capability.
   - **Bug Report** for a defect.
   - **Feature Request** for an idea that isn't scoped yet.

   The issue number is the work item ID. Reference it in the commit body, for example
   `Closes #123`.
3. Implement in small, focused commits with tests.
4. Update [docs/status.md](docs/status.md) and the relevant spec as progress changes.
5. Record each significant architectural decision as an ADR in [docs/adr/](docs/adr/).
6. Record what you learned that others need later as a knowledge document in
   [docs/knowledge/](docs/knowledge/).

## Where things go

Project organization is strict, and tests enforce it. The rules are in
[docs/specs/project-organization.md](docs/specs/project-organization.md).

| Content | Location | Rule |
| --- | --- | --- |
| Work items | GitHub Issues | Never a Markdown file in `docs/` |
| Architectural decisions | `docs/adr/ADR-NNN-short-title.md` | Numbered in order, fixed sections, listed in the index |
| Behavioral specs | `docs/specs/short-title.md` | Fixed sections, linked to a tracking issue, listed in the index |
| Reference knowledge | `docs/knowledge/short-title.md` | Listed in the index; images in `docs/knowledge/images/` |
| Planning and progress | `docs/roadmap.md` and `docs/status.md` | No other loose files in `docs/` |
| Backend | `apps/server/` | A .NET solution; each project in `src/` has a test project in `tests/` |
| Web UI | `apps/web/` | A TypeScript, React, and Vite project |
| Secrets | `.env` (never committed) | `.env.example` lists the variables, with secret values empty |
| Collection data | `collections/` | YAML files and GeoJSON boundaries |
| Docker files | `docker/` | Dockerfile and example compose files |
| Scripts | `scripts/` | Shell scripts for checks and maintenance |
| Organization tests | `tests/organization/` | One test file per rule area |

Don't add a new top-level file or folder, or a new folder under `docs/`, without updating the
project organization spec and its tests in the same change.

When documents disagree, the order of authority is: ADRs, then specs, then knowledge documents.
The design and branding document is a snapshot of the original design. An accepted ADR or a spec
that came later wins.

## Working conventions

- Prefer small, composable changes over rewrites.
- Keep the trip-detection rules in their own C# project, as pure functions, so they can be tested
  with plain data and no running Immich.
- Never write to Immich. Never send photo data or location data to an outside service. The one
  exception is the MCP server, which may return counts, dates, and country, continent, collection,
  and park names. See
  [docs/adr/ADR-018-an-mcp-server-answers-questions-with-coarse-read-only-data.md](docs/adr/ADR-018-an-mcp-server-answers-questions-with-coarse-read-only-data.md).
- Your decisions (added, ignored, renamed, merged) are stored separately from detected data and
  always win. See
  [docs/adr/ADR-002-decisions-are-stored-separately-from-detected-data.md](docs/adr/ADR-002-decisions-are-stored-separately-from-detected-data.md).
- One logical change per commit. Commit messages describe intent, for example
  `feat(inbox): add the ignored tab` or `fix(scanner): skip photos at 0, 0`.
- For user-facing text, follow the voice and tone table in the design and branding document.

## Validation before you consider work done

```bash
./scripts/check.sh
```

This runs the organization tests, the .NET checks in `apps/server`, and the UI checks in
`apps/web`, as [docs/specs/quality-gates.md](docs/specs/quality-gates.md) requires. CI runs the
same script on every pull request.

After you change an API route, update the OpenAPI document and the TypeScript types. The steps
are in [docs/knowledge/web-app-development.md](docs/knowledge/web-app-development.md).

## Key docs map

- [docs/guidelines.md](docs/guidelines.md): the seven project guidelines.
- [docs/README.md](docs/README.md): the map of the documentation.
- [docs/knowledge/design-and-branding.md](docs/knowledge/design-and-branding.md): brand,
  experience, and architecture for version 1.
- [docs/roadmap.md](docs/roadmap.md) and [docs/status.md](docs/status.md): planning and current
  state.
- [docs/development-workflow.md](docs/development-workflow.md): idea to release, step by step.
- [docs/adr/](docs/adr/): architectural decisions.
- [docs/specs/](docs/specs/): behavioral specs.
- [docs/knowledge/](docs/knowledge/): reference knowledge.
- [CONTRIBUTING.md](CONTRIBUTING.md): contribution conventions.
