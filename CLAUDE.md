# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this project is

Fibotrail is a self-hosted web app that turns an Immich photo library into a travel journal. It
finds trips, maps them, and counts them. Immich supplies photos and GPS. Fibotrail adds trips,
places, collections, and statistics on top.

Core principle: **Immich stays untouched.** Fibotrail only reads from Immich and keeps its own
state in its own database. See
[docs/adr/ADR-001-immich-is-read-only.md](docs/adr/ADR-001-immich-is-read-only.md).

```
Immich → Scanner → Trip detector → Database → Web server → Phone or iPad
```

The full picture is in
[docs/knowledge/design-and-branding.md](docs/knowledge/design-and-branding.md).

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
| Server and web apps | `apps/` | Per the repository layout in the design document |
| Shared packages | `packages/` | Same |
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
- Keep the trip-detection rules as pure functions so they can be tested with plain data and no
  running Immich.
- Never write to Immich. Never send photo data or location data to an outside service.
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

This runs `npm test`, which runs the organization tests. CI runs the same script on every pull
request. When application code arrives, add its lint, build, and test steps to the same script.

## Key docs map

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
