# ADR-008: A generated Immich client, limited to allowed reads and tested with recorded responses

## Status

Accepted

## Context

[ADR-001](ADR-001-immich-is-read-only.md) says that Fibotrail never writes to Immich. The
[Immich scanning spec](../specs/immich-scanning.md) asks for a C# client generated from Immich's
OpenAPI file, and for a test that fails when a call is made outside an allowed list.

Immich 3.4.1 describes 199 addresses in its OpenAPI file. Fibotrail needs four requests. A client
generated from the whole file would hold a method for every write that Immich has, one typo away
from use.

The [roadmap](../roadmap.md#decisions-the-mvp-still-needs) also asks how CI tests the Immich
contract. CI has no Immich server, and a real library is private.

## Decision

The client holds only the four allowed requests, a guard stops every other request, and the tests
use recorded responses.

- **One list.** `AllowedRequests` in `apps/server/src/Fibotrail.Immich` names the requests:
  `GET /server/version`, `POST /search/metadata`, `GET /assets/statistics`, and
  `GET /assets/{id}/thumbnail`.
- **A client with nothing else in it.** `scripts/generate-immich-client.sh` runs Kiota 1.38.0 on
  the OpenAPI file of a pinned Immich tag, with the list as its include paths. The result is 53
  files in `Fibotrail.Immich/Generated`, and they're committed. No method for a write exists.
- **A guard at the last step.** Every request passes through `ImmichGuardHandler`. It stops a
  request that isn't on the list or isn't for `IMMICH_URL`, before the request leaves. It's also
  the only place that adds the API key, so the key can't reach another host.
- **Recorded responses in CI.** The tests run against `FakeImmichServer`, a stand-in that answers
  in the shapes recorded in [immich-api-findings.md](../knowledge/immich-api-findings.md). Every
  value in them is made up. A test against a real Immich is left for version 1.

Tests fail when the generated client, the script, and the list differ, and when a full scan sends
any request that isn't on the list.

## Alternatives considered

- **Generate the whole client.** One command with no filter. Set aside because it puts about 195
  unused addresses, many of them writes, into the code base, and adds about 1 MB of code to read
  past.
- **Write the four requests by hand.** Less code. Set aside because the spec asks for a generated
  client, and a generated client follows Immich's own field names and types when Immich changes.
- **Rely on the read-only API key alone.** The key's permissions already stop writes. Set aside as
  the only defense because a person can give Fibotrail a key with more permissions by mistake.
- **Run a real Immich in CI.** The closest test of the contract. Set aside for the MVP because it
  needs a seeded library and several containers for a contract that the spike already recorded.

## Consequences

- Fibotrail has three layers against a write: the key's permissions, the client's missing
  methods, and the guard.
- Using a new Immich request takes three edits that a test checks against each other: the list,
  the script, and a run of the script.
- The recorded responses can go stale. When Immich changes a field, the tests still pass, and the
  scan of a real library is what shows it. Settings warns when the Immich version isn't 3.4.
- The OpenAPI file isn't in the repository. The script downloads it from the pinned tag, so
  running the script needs network access to `raw.githubusercontent.com`.
- Decided on 2026-11-15: the generated files get an attribution note, and Fibotrail stays MIT
  licensed. Immich is AGPL-3.0, and the generated files repeat names and descriptions from its
  OpenAPI file, so `scripts/generate-immich-client.sh` writes
  `apps/server/src/Fibotrail.Immich/Generated/NOTICE.md` each time it runs. The note says where the
  names come from, that no Immich source code is included, and that Fibotrail talks to Immich only
  over HTTP. This is a project decision, not legal advice. If the maintainer later decides the
  generated folder should carry the AGPL itself, only that folder changes.
