# ADR-018: An MCP server answers questions with coarse, read-only data

## Status

Superseded by ADR-023

## Context

The Stats screen answers a fixed list of questions. Each question is a hand-built
[view](ADR-014-stats-views-are-self-contained-modules.md), so a new question means new code. The
maintainer wants to ask questions in plain language instead, such as "How many times have I been to
a Disney park?", and get an answer without anyone writing a view first.

The Model Context Protocol (MCP) fits. An MCP server exposes a small set of tools. An MCP client,
such as the Claude app, lets a language model call those tools and put the answers into words.

This raises one conflict. [CLAUDE.md](../../CLAUDE.md) says: "Never send photo data or location data
to an outside service." A tool result goes to the model. If the model runs in the cloud, the result
leaves the home network. Fibotrail can't control which model the client uses, so it can only control
what its tools return.

What exists today:

- The [stats library](../specs/insights.md#the-stats-library) holds added trips, their days, and
  the country of each day. It is read-only and built per request
  ([ADR-014](ADR-014-stats-views-are-self-contained-modules.md)).
- Collections count trips and days per park from stored photo positions
  ([ADR-013](ADR-013-park-days-are-derived-from-stored-photo-positions.md),
  [spec](../specs/collections.md)). The collections spec defines "how many separate trips" and
  "days" for the Disney parks.
- A trip's name is free text that you type. It can hold anything, such as a family member's name.

Not known yet: how well a model answers from these tools without hand-written views, and whether a
country name alone is acceptable to the maintainer as "location data". The consequences
record the answer.

## Decision

Add a read-only MCP server to the one container. Its tools return **coarse data only**: counts,
dates, durations, and public names. They never return coordinates, photos, or photo IDs.

```
Client (Claude app)  ──▶  /mcp  ──▶  tools  ──▶  stats library + collections  ──▶  database
  asks in plain words      token      coarse       (added trips only, read-only)
                                      answers
```

- **Where it lives.** A new C# project, `Fibotrail.Mcp`, with a test project, and one route, `/mcp`,
  in the existing server container
  ([ADR-006](ADR-006-one-container-on-the-immich-network.md)). It uses the streamable HTTP
  transport. There is no new container or port.
- **Where the answers come from.** Each tool calls the stats library and the collections code that
  the screens already call, so the app gives one answer to one question, as in ADR-013. A tool never
  reads the database, Immich, or a view.
- **Read-only.** No tool writes, and none calls Immich
  ([ADR-001](ADR-001-immich-is-read-only.md)). Decisions stay in their own tables
  ([ADR-002](ADR-002-decisions-are-stored-separately-from-detected-data.md)).
- **Only added trips.** Suggested, later, and ignored trips are in no result, as on the Stats
  screen.
- **What a tool may return.** This is the whole vocabulary, and a test enforces it:

  | Allowed | Examples |
  | --- | --- |
  | Counts and durations | 5 trips, 19 days |
  | Dates, to the day | first visit 2019-07-03 |
  | Country and continent names | Sweden, Europe |
  | Collection and park names | Disney parks, Magic Kingdom |
  | Trip IDs | 42 |

  | Never returned | Why |
  | --- | --- |
  | Latitude, longitude, routes, stop centers | Precise location |
  | Photo IDs, thumbnails, file names, camera data | Photo data |
  | Trip names and notes | Free text you typed, which can hold anything |
  | City and place names below a park | Finer than a country |
  | Immich URLs, keys, and user names | Secrets and identity |

- **First tools.** The first set answers the questions of the narrative. The exact list belongs to
  a spec, and the list below is the starting point:

  | Tool | Answers |
  | --- | --- |
  | `collection_visits(collection)` | "How many times have I been to a Disney park?" It returns the trips, the days, the first and last visit, and the same numbers per park |
  | `country_summary(country)` | Trips, days, first and last visit |
  | `list_countries(continent?)` | Visited countries with trips and days |
  | `list_trips(filters)` | Trip ID, dates, days, and countries, filtered by date or country |
  | `days_away(by)` | Days away by month or year |

  "How many times" means separate trips, and "days" means park days. `collection_visits` returns
  both, because the collections spec leads with trips and a person means either one.
- **Access.** The server needs a bearer token from the `MCP_TOKEN` variable
  ([ADR-005](ADR-005-secrets-come-from-the-environment.md)). It is separate from the web session
  token, never sent to the browser, never logged, and listed in `.env.example` with an empty value.
  With the variable empty, `/mcp` returns 404, so the feature is off by default.
- **The model is the client's choice.** Fibotrail doesn't pick or call a model. A person who wants
  nothing to leave the home network points a local model at `/mcp`.

## Alternatives considered

- **Local model only.** Fibotrail would run or require a local model. Set aside because it adds
  weight to the container, and it isn't needed: the coarse vocabulary makes a cloud client
  acceptable, and a local client still works.
- **Amend the rule and return everything.** One ADR would allow precise data for clients that you
  approve. Set aside because it weakens a core principle, and the questions we want to ask need
  counts and dates, not coordinates.
- **A free-form query tool.** The model would write SQL or LINQ against the database. Set aside
  because it is the widest door: it can read columns the vocabulary excludes, and nothing could
  enforce the table above.
- **One natural-language endpoint inside Fibotrail.** Fibotrail would call a model itself. Set aside
  because that makes Fibotrail the one that sends data to an outside service, and it needs an API
  key to store.
- **Add more hard-coded views.** This is the current approach. It stays for charts. Set aside for
  questions, because every new question costs a view, a route, a registry line, and a spec.

## Consequences

- A new question needs no new code when existing tools can answer it. A question that needs a new
  fact adds a field to the stats library, per ADR-014, and a tool or a tool argument.
- The vocabulary table is the contract. A test walks every tool's result type and fails if a field
  isn't on the allowed list, so a later change can't leak a coordinate or a trip name by accident.
- Trip IDs let the model refer to a trip without a name. The model can't open the trip, because no
  tool returns the name or the photos. To read a trip, you open it in the app.
- Country and park names leave the home network when the client is a cloud model. The maintainer
  accepted this on 2026-11-18, because they are coarse. The CLAUDE.md rule "never send photo data
  or location data to an outside service" is read to mean photos, coordinates, and place names
  finer than a park. A tool that returns anything on that list breaks the rule, and the test of the
  allowed list catches it. A person who wants nothing to leave the network uses a local model.
- **Open question:** does a person's visit count to a named park identify them? We treat it as no
  for a private, single-user app, and we revisit this if Fibotrail becomes multi-user.
- The feature adds one token to manage and one more route to secure. A leaked token exposes the
  coarse data only, never photos or coordinates.
- Answers depend on the model's reading of the tool results. Tool descriptions must state the
  rules, such as "days are park days, not calendar days of the trip", so the model doesn't guess.
- The spec is [mcp-server.md](../specs/mcp-server.md), tracked by
  [#143](https://github.com/bradreimer/fibotrail/issues/143). It holds the full tool list.
