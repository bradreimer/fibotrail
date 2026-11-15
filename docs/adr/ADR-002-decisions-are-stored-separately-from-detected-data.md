# ADR-002: Decisions are stored separately from detected data

## Status

Accepted

## Context

Fibotrail scans the library on a schedule and detects trips again each time. The person also
makes choices: add a trip, ignore a suggestion, rename, merge, split, or override a place. The
design principle "You decide" requires that a choice made once is never asked again.

If decisions lived only on detected rows, a rescan or a change to the detection rules could
overwrite or orphan them.

## Decision

Fibotrail separates what it detects from what you decide.

- Detected data (photos, suggested trips, stops, visits) can be rebuilt at any time.
- Decisions are stored in their own table and always win over detected data.
- Each trip has a fingerprint: a stable hash of its start date and main place. A decision is keyed
  by fingerprint, so a rescan recognizes the same trip and applies the decision again.
- Overrides to collections ("Mark as visited" and "Not a visit") are decisions too.

## Alternatives considered

- **Store the decision as a status on the detected trip row only.** This is simpler, but a rebuild
  of detected data would lose the decision.
- **Never re-detect a trip after a decision.** This protects the decision, but it stops new photos
  from extending a trip and blocks improvements to the detection rules.

## Consequences

- The "Rebuild from Immich" action can re-derive all detected data and keep every decision.
- The detection rules can change between releases without losing your choices.
- The fingerprint must stay stable. A change to how it's computed needs a migration.
- A test must prove that a rescan never undoes a decision.
