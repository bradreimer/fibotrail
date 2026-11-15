# Contributing to Fibotrail

Thanks for your interest in contributing. Fibotrail is a small, open-source project that turns an
Immich photo library into a travel journal. One person maintains it as a hobby project.

## Before you start

For larger changes, such as a new feature, a data model change, or a change to trip detection,
open an issue first. It saves you from writing code that goes in a direction the project doesn't
want.

For small fixes, documentation, and tests, open a pull request directly.

## How work flows

Idea → spec → work item → implementation → tests → documentation update → commit → release

1. **Spec.** A change that affects user-facing behavior, workflow, or cross-cutting architecture
   needs a spec in [docs/specs/](docs/specs/) first.
2. **Work item.** Implementation-sized work is tracked as a
   [GitHub Issue](https://github.com/bradreimer/fibotrail/issues). Use the User Story, Bug Report,
   or Feature Request template.
3. **Implementation.** Make small, focused commits with tests.
4. **Documentation.** Update [docs/status.md](docs/status.md) and the spec. Record a significant
   architectural decision as an ADR in [docs/adr/](docs/adr/), and lasting reference material in
   [docs/knowledge/](docs/knowledge/).

See [docs/development-workflow.md](docs/development-workflow.md) for details.

## Project organization

Every file has one correct place, and unit tests enforce it. Read
[docs/specs/project-organization.md](docs/specs/project-organization.md) before you add a file or
folder. If a test in `tests/organization/` fails, move the file or fix the document. Change the
rule only by updating the spec and the test together.

## Running the checks

You need Node.js 22 or later. No dependencies need to be installed yet.

```bash
./scripts/check.sh
```

## Making changes

A good pull request:

- Solves one problem.
- Makes the smallest reasonable change.
- Includes tests for new or changed behavior.
- Doesn't bundle in an unrelated refactor.

## Architecture principles

- **Immich stays untouched.** Fibotrail only reads from Immich.
- **You decide.** Fibotrail suggests trips and places, and nothing is added until the user says
  yes. Decisions survive rescans.
- **Private by default.** Don't add code that sends photos, locations, or metadata to an outside
  service. Don't store secrets in the repository.

The accepted decisions are in [docs/adr/](docs/adr/).

## Pull requests

Explain what changed, why, and how you tested it:

```text
## Summary
Adds the Ignored tab to the trip inbox.

## Why
You need a way to restore a suggestion you ignored by mistake.

## Testing
./scripts/check.sh
```

Reference the work item in the pull request body, for example `Closes #123`.

## Commit messages

Keep them short, and describe intent:

```text
feat(inbox): add the ignored tab
fix(scanner): skip photos at 0, 0
docs(adr): record the map tile decision
```

## Reporting bugs

Include what you expected, what happened instead, the steps involved, any error messages, and the
project version or commit.

Don't attach personal photos or private location data to an issue. Describe the problem instead.

## License

By contributing, you agree that your contributions are licensed under the same license as the
project. See [LICENSE](LICENSE).
