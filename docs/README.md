# Fibotrail documentation

This folder holds everything written about Fibotrail that isn't code. Each kind of document has
one home.

```text
docs/
  README.md                 this map
  development-workflow.md   how an idea becomes a release
  roadmap.md                what's planned, by phase
  status.md                 what's done and what's next
  adr/                      decisions: why the architecture is the way it is
  specs/                    intent: what a capability must do
  knowledge/                reference: how things are and what we learned
```

## Which document do I need?

Think of the project as a trip. A **spec** is the itinerary: where you intend to go. A **work
item** is one leg of the journey. An **ADR** is a note about why you took one road and not the
other. **Knowledge** is the guidebook you keep for next time.

| You want to | Use | Location |
| --- | --- | --- |
| Propose or track a piece of work | Work item | [GitHub Issues](https://github.com/bradreimer/fibotrail/issues) |
| Define what a capability must do | Spec | [specs/](specs/README.md) |
| Record why an architectural choice was made | ADR | [adr/](adr/README.md) |
| Keep reference material or findings | Knowledge document | [knowledge/](knowledge/README.md) |
| See the plan or the current state | Roadmap and status | [roadmap.md](roadmap.md) and [status.md](status.md) |

## Rules

- Work items live in GitHub Issues, never in this folder.
- No loose files. Only the files and folders in the map above belong at this level.
- Every ADR, spec, and knowledge document is listed in the index of its folder's README.
- When documents disagree, ADRs win over specs, and specs win over knowledge documents.

The full rules are in [specs/project-organization.md](specs/project-organization.md). The tests in
`tests/organization/` enforce them.
