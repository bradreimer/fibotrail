# Architecture decision records

An architecture decision record (ADR) captures one significant architectural choice: what was
decided, why, and what follows from it. Write an ADR when a choice affects the direction of the
project or would be expensive to reverse.

## Rules

- One decision per file, named `ADR-NNN-short-title.md`, with a three-digit number and a
  lowercase, hyphenated title.
- Numbers run in order with no gaps. The next ADR takes the next number.
- The first line is `# ADR-NNN: Title`, and the number matches the file name.
- Every ADR has these sections, in this order: Status, Context, Decision, Alternatives
  considered, Consequences.
- Status is one of `Proposed`, `Accepted`, `Deprecated`, or `Superseded by ADR-NNN`.
- Don't rewrite an accepted ADR to change the decision. Write a new ADR and mark the old one
  superseded.
- List every ADR in the index below.

## Template

```markdown
# ADR-NNN: Title

## Status

Proposed

## Context

What situation forces a decision?

## Decision

What did we decide?

## Alternatives considered

What else did we weigh, and why did we set it aside?

## Consequences

What becomes easier, what becomes harder, and what must stay true?
```

## Index

| ADR | Decision | Status |
| --- | --- | --- |
| [ADR-001](ADR-001-immich-is-read-only.md) | Immich is read-only, and Fibotrail keeps its own database | Accepted |
| [ADR-002](ADR-002-decisions-are-stored-separately-from-detected-data.md) | Decisions are stored separately from detected data | Accepted |
| [ADR-003](ADR-003-collections-are-data-files.md) | Collections are data files | Accepted |
| [ADR-004](ADR-004-technology-stack.md) | Technology stack | Proposed |
