# Project organization

Tracking issue: [#2](https://github.com/bradreimer/fibotrail/issues/2).

## Purpose

Fibotrail is maintained in bursts by one person, with help from coding agents. A predictable
layout means that anyone, person or agent, can find the intent behind a change and knows where a
new file goes. Rules that live only in prose drift, so unit tests enforce these rules.

## User story

As the maintainer, I want every kind of content to have one correct home, checked by tests, so
that the repository stays organized without me policing it.

## Goals

- Every change starts from written intent: a spec for behavior and a work item for the work.
- Each kind of document has one home and one format.
- A misplaced file or a malformed document fails a test, locally and in CI.
- The instructions for coding agents state the same rules that the tests enforce.

## Non-goals

- Checking the quality of what a document says. Tests check structure, not judgment.
- Checking GitHub Issues. Work items are tracked in GitHub, outside the repository.
- Rules for the layout inside `apps/server` and `apps/web`, beyond the test project rule in the
  [quality gates spec](quality-gates.md). Those arrive with the code.

## Requirements

### Work items

- Work items are GitHub Issues. No ticket files exist in the repository.
- Three issue templates exist in `.github/ISSUE_TEMPLATE/`: `user_story.md`, `bug_report.md`, and
  `feature_request.md`.
- Each template has front matter with a name and a label, and the sections its kind requires. The
  User Story and Bug Report templates include Related spec, Acceptance criteria, and Testing
  requirements.

### Repository root

- Required: `.github/`, `docs/`, `scripts/`, `tests/`, `.editorconfig`, `.env.example`,
  `.gitignore`, `CLAUDE.md`, `CONTRIBUTING.md`, `LICENSE`, `README.md`, and `package.json`.
- Allowed when needed: `apps/`, `collections/`, `docker/`, `.dockerignore`, `.nvmrc`, and
  `package-lock.json`.
- Nothing else is tracked at the root.
- `apps/` holds only `server` (the .NET solution) and `web` (the TypeScript UI).

### Secrets

- `.env` is never tracked, and `.gitignore` ignores it.
- `.env.example` lists variables as `NAME=value` lines. A variable whose name contains `KEY`,
  `SECRET`, `TOKEN`, or `PASSWORD` has an empty value.

### The README

- `README.md` has the level-2 sections Install, Configuration, API key permissions, Backup,
  Privacy, and Updating and resetting, in that order.
- It documents, in code format, every variable in `.env.example` (including commented ones) and
  every Immich permission named there.

### The docs folder

- `docs/` holds exactly five files and three folders: `README.md`, `development-workflow.md`,
  `guidelines.md`, `roadmap.md`, `status.md`, `adr/`, `specs/`, and `knowledge/`.
- `docs/status.md` has the sections Current milestone, Completed, In progress, and Next.
- `docs/guidelines.md` has seven numbered guidelines. Each states its rule, its method, and
  where it's enforced.

### ADRs

- `docs/adr/` holds `README.md` and ADR files only, with no subfolders.
- An ADR file is named `ADR-NNN-short-title.md`. Numbers start at 001 and run in order with no
  gaps and no duplicates.
- The first line is `# ADR-NNN: Title`, with the same number as the file name.
- The sections are exactly Status, Context, Decision, Alternatives considered, and Consequences,
  in that order.
- The status is `Proposed`, `Accepted`, `Deprecated`, or `Superseded by ADR-NNN`, where the
  superseding ADR exists.
- The index in `docs/adr/README.md` links to every ADR and shows its current status.

### Specs

- `docs/specs/` holds `README.md` and spec files only, with no subfolders.
- A spec file has a lowercase, hyphenated name.
- A spec starts with a `# Title` line, followed by a `Tracking issue:` line that links to an issue
  in this repository.
- The sections are exactly Purpose, User story, Goals, Non-goals, Requirements, Acceptance
  criteria, and Open questions, in that order.
- The index in `docs/specs/README.md` links to every spec.

### Knowledge

- `docs/knowledge/` holds `README.md`, knowledge documents with lowercase, hyphenated names, and
  an `images/` folder.
- Images have lowercase, hyphenated names and are SVG, PNG, JPEG, or WebP files.
- At least one knowledge document uses each image.
- The index in `docs/knowledge/README.md` links to every knowledge document.

### Links

- Every relative link and image reference in a Markdown file points to a file that exists.

### Instructions

- `CLAUDE.md` and `.github/copilot-instructions.md` exist and name each home: `docs/specs/`,
  `docs/adr/`, `docs/knowledge/`, `docs/status.md`, `docs/guidelines.md`, GitHub Issues, and
  `scripts/check.sh`.
- `scripts/check.sh` runs the tests, and the CI workflow runs `scripts/check.sh`.

### Tests

- The organization tests live in `tests/organization/` and run with `npm test`.
- The rules are pure functions in `tests/organization/rules.mjs`, with their own unit tests, so a
  rule can be tested against good and bad examples.
- A change to a rule updates this spec and its test in the same change.

## Acceptance criteria

- `npm test` passes on a clean checkout.
- Adding a stray file to the root or to `docs/` fails a test that names the file.
- An ADR or spec with a missing section, a wrong name, or a missing index entry fails a test that
  names the document and the problem.
- A broken relative link in any Markdown file fails a test.
- CI runs the same tests on every pull request to `main`.

## Open questions

None.
