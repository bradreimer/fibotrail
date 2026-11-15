# Knowledge

Knowledge documents are reference material: how things are, what we learned, and facts the
project depends on. Examples are the design and branding document, the findings of a spike, and
notes on how the Immich API behaves.

A knowledge document describes. It doesn't decide and it doesn't promise:

- If it records why a choice was made, it's an [ADR](../adr/README.md).
- If it states what a capability must do, it's a [spec](../specs/README.md).
- If it's work to do, it's a [GitHub Issue](https://github.com/bradreimer/fibotrail/issues).

## Rules

- One topic per file, named `short-title.md` in lowercase with hyphens.
- Images live in `images/`. Every image is used by at least one document.
- Say where the knowledge came from and when, so a reader can judge how current it is.
- List every document in the index below.

## Index

| Document | What it covers | Source |
| --- | --- | --- |
| [design-and-branding.md](design-and-branding.md) | Brand, experience, and architecture for version 1 | Exported from the design document on 2026-11-15. ADR-004 and ADR-005 replace its stack table and its Docker secret setup. The first run and sign-in spec replaces the published port in its compose example |
| [immich-api-findings.md](immich-api-findings.md) | What Immich returns for dates, positions, and place names, with made-up responses for tests | The spike, #12, run against Immich 3.4.1 on 2026-11-15 |
| [web-app-development.md](web-app-development.md) | How to run and change the web app, regenerate the API types, and redraw the icons | Written with the app shell, #6, on 2026-11-15 |
| [accessibility-checklist.md](accessibility-checklist.md) | The accessibility checks that tests make, and the manual checks to run on an iPhone before a release | Written for the release, #24, on 2026-11-15. The tests are in `apps/web/src/accessibility/` |
