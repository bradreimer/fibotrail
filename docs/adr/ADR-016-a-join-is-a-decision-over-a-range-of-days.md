# ADR-016: A join is a decision over a range of days

## Status

Accepted

## Context

A pause of 72 hours between away photos ends a trip. A trip with a few days of no pictures shows
as two trips, and nothing could repair it. A join must survive every rescan, per
[ADR-002](ADR-002-decisions-are-stored-separately-from-detected-data.md).

## Decision

A join is a row in the `trip_join` table with a first day and a last day. Detection reads the
table on every run. In rule 3c, the groups of away photos that touch a range become one group before
the minimums apply. `fibotrail join` writes the rows, and carries the decision of the trips to the
joined trip, so its status and name stay.

## Alternatives considered

- **Join by trip fingerprint.** A fingerprint changes when the start date or main place changes, so
  the join would be lost. Days are what you know.
- **A longer gap limit.** It fixes this case and merges unrelated trips elsewhere.
- **Merge the stored trip rows.** Detected data is rebuilt on every run, so the merge would be undone.

## Consequences

- Joins are never rebuilt or deleted by detection.
- Removing a join splits the trips again. The decision of the set-aside trip applies again.
- Trips from rules 3b and 4b can't be joined yet.
