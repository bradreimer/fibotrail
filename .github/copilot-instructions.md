# Copilot instructions for Fibotrail

[CLAUDE.md](../CLAUDE.md) is the full guide. This file repeats the rules that matter most.

## Development workflow

- Follow spec-driven, work-item-driven development.
- Before a meaningful feature or behavior change, check the spec in `docs/specs/` and the work item in GitHub Issues.
- If no spec or work item exists, create one before you write code. Issues use the User Story, Bug Report, or Feature Request template in `.github/ISSUE_TEMPLATE/`.
- Keep the documentation in sync with implementation decisions, architecture changes, and release progress.

## Required documentation artifacts

- `docs/knowledge/` for reference material: how things are and why.
- `docs/specs/` for behavioral intent and acceptance criteria.
- GitHub Issues for implementation-sized work items with a clear scope and test plan.
- `docs/adr/` for significant architectural decisions.
- `docs/roadmap.md` for release scope, and `docs/status.md` for current progress.

## Implementation expectations

- Make small, focused changes with tests.
- Reference the work item in the commit body, for example `Closes #123`.
- Update `docs/status.md` and the relevant spec when progress changes.
- Put every file where `docs/specs/project-organization.md` says it belongs. The tests in `tests/organization/` enforce the layout.

## Validation

- Run `./scripts/check.sh` before you consider work complete.
