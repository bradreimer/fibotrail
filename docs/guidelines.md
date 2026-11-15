# Project guidelines

These seven guidelines apply to every change. Each one states the rule, the method used to follow
it, and where it's enforced. If a guideline and another document disagree, fix the other document.

## 1. State intention and methodology plainly

**Rule.** Every document and design says what it's for and how it works. No vague concepts.

**Method.**

- Open with the purpose: what the document decides, specifies, or describes, and for whom.
- Name the mechanism. Write "the scanner pages through `POST /api/search/metadata`", not "the app
  integrates with Immich".
- Use numbers, names, and examples where an adjective would otherwise go.
- Say what isn't known yet, and what will settle it.

**Enforced by.** The fixed sections of ADRs and specs, which a test checks, and review. A reviewer
rejects a document that a newcomer couldn't act on.

## 2. Keep the project cleanly organized at all times

**Rule.** Every file has one correct place. When the organization stops fitting, stop and
reorganize before adding more.

**Method.**

- Follow [specs/project-organization.md](specs/project-organization.md).
- When a new kind of file has no home, update that spec and its tests first, then add the file.
- A reorganization is its own work item and its own commit, separate from feature work.

**Enforced by.** The tests in `tests/organization/`, run by `./scripts/check.sh` and CI.

## 3. Handle secrets through the environment, starting with `.env`

**Rule.** No secret is ever committed, logged, or sent to the browser.

**Method.**

- Secrets reach the app as environment variables. Docker Compose loads them from a `.env` file
  next to the compose file.
- `.env` is ignored by Git. `.env.example` is committed and lists every variable with secret
  values left empty.
- The Immich API key is limited to read permissions.
- The app refuses to start, with a clear message, when a required secret is missing.

**Enforced by.** [ADR-005](adr/ADR-005-secrets-come-from-the-environment.md), and tests that fail
if `.env` is tracked, if `.gitignore` stops ignoring it, or if `.env.example` holds a secret value.

## 4. Use C# for the backend and TypeScript for the UI

**Rule.** Backend functionality is .NET (C#). The user interface is a web app in TypeScript.

**Method.** The backend is an ASP.NET Core app in `apps/server`. The UI is a React app in
`apps/web`. The backend serves the built UI, so the two ship as one unit.

**Enforced by.** [ADR-004](adr/ADR-004-technology-stack.md).

## 5. Host the app in a Docker container with local access to Immich

**Rule.** Fibotrail runs as one Docker container that reaches the Immich container directly.

**Method.** The Fibotrail container joins the Docker network that Immich is on and calls Immich by
its container name. Nothing is exposed to the internet by Fibotrail itself.

**Enforced by.** [ADR-006](adr/ADR-006-one-container-on-the-immich-network.md).

## 6. Make the phone experience feel like a native iOS app

**Rule.** On an iPhone, Fibotrail opens from the home screen and behaves like an app, not like a
website.

**Method.** The UI is a progressive web app that runs in standalone mode, respects the safe areas,
uses a bottom tab bar, and responds to touch the way iOS apps do.

**Enforced by.** [ADR-007](adr/ADR-007-native-ios-feel-through-a-progressive-web-app.md), which
lists the specific behaviors, and a checklist on every work item that changes the UI.

## 7. Validate new functionality with unit tests, and pass formatting and linting

**Rule.** New functionality comes with a unit test. Every check-in passes the formatting and
linting checks.

**Method.**

- `./scripts/check.sh` runs every formatter check, linter, and test. Run it before each commit.
- CI runs the same script on every pull request, and a failing check blocks the merge.
- A bug fix comes with a regression test.

**Enforced by.** [specs/quality-gates.md](specs/quality-gates.md), CI, and tests that fail if the
check script drops a required step.
