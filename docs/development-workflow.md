# Development workflow

## Flow

Idea → spec → work item → implementation → tests → documentation update → commit → release

## Before you start work

- Check [specs/](specs/README.md) for a spec that covers the change. Write one first if the change
  affects user-facing behavior, workflow, or cross-cutting architecture.
- Check [GitHub Issues](https://github.com/bradreimer/fibotrail/issues) for a matching work item,
  or open one with the template that fits:
  - **User Story** (`.github/ISSUE_TEMPLATE/user_story.md`) for a scoped capability.
  - **Bug Report** (`.github/ISSUE_TEMPLATE/bug_report.md`) for a defect.
  - **Feature Request** (`.github/ISSUE_TEMPLATE/feature_request.md`) for an idea that isn't
    scoped yet.
- Update [status.md](status.md) if the change affects the current milestone or priorities.

## While you work

- Record a significant architectural decision as an ADR in [adr/](adr/README.md).
- Record findings that others need later, such as the result of a spike, as a knowledge document
  in [knowledge/](knowledge/README.md).
- Put each file where [specs/project-organization.md](specs/project-organization.md) says it
  belongs.

## Commits

Make small, focused commits with one logical change each:

```text
feat(inbox): add the ignored tab
fix(scanner): skip photos at 0, 0
```

Reference the work item in the commit body, for example `Closes #123`.

## Testing

Before you commit:

```bash
./scripts/check.sh
```

## Releases

A release needs its work items completed, tests passing, documentation updated, and a release
tag.
