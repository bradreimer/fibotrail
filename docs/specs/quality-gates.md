# Quality gates

Tracking issue: [#3](https://github.com/bradreimer/fibotrail/issues/3).

## Purpose

This spec defines the checks that every check-in must pass, and how they're run. The checks are
how the maintainer's quality expectations are applied without a manual review of every line.

## User story

As the maintainer, I want one command and one CI job to tell me whether a change meets my quality
expectations, so that nothing unformatted, unlinted, or untested reaches `main`.

## Goals

- One command, `./scripts/check.sh`, runs every check.
- CI runs the same command on every pull request, and a failure blocks the merge.
- New functionality can't be added without a unit test that exercises it.
- Formatting is decided by tools, not by opinion.

## Non-goals

- A code coverage percentage target. Coverage is reported, not gated.
- End-to-end tests against a real Immich. Those belong to a later spec.
- Checking the quality of prose. Review does that.

## Requirements

### One command

- `./scripts/check.sh` exits with a failure if any check fails.
- A check verifies and reports. It doesn't rewrite files, so the script gives the same result
  locally and in CI.
- The CI workflow runs `./scripts/check.sh` and nothing else that decides pass or fail.

### Checks that run today

- The organization tests: `npm test`.
- Text formatting, checked by an organization test: every text file ends with one newline, uses
  LF line endings, and has no trailing spaces. `.editorconfig` states the same rules for editors.

### Checks added with the backend

When `apps/server` exists, `./scripts/check.sh` must also run:

- `dotnet format --verify-no-changes` for formatting and analyzer rules.
- `dotnet build` with warnings treated as errors.
- `dotnet test` for the xUnit tests.

Every C# project in `apps/server/src` has a test project of the same name with a `.Tests` suffix
in `apps/server/tests`.

### Checks added with the UI

When `apps/web` exists, `./scripts/check.sh` must also run, through npm scripts in `apps/web`:

- `npm run format:check` for Prettier.
- `npm run lint` for ESLint.
- `npm run typecheck` for the TypeScript compiler.
- `npm test` for Vitest.

### Tests for new functionality

- A change that adds or changes behavior includes a unit test that fails without the change.
- A bug fix includes a regression test.
- The pull request description says which tests cover the change.

## Acceptance criteria

- `./scripts/check.sh` passes on a clean checkout of `main`.
- A file with trailing spaces or CRLF line endings fails a test that names the file.
- Adding `apps/server` or `apps/web` without the matching steps in `./scripts/check.sh` fails a
  test that names the missing step.
- A C# project without a matching test project fails a test that names the project.
- CI fails a pull request when `./scripts/check.sh` fails.

## Open questions

None.
