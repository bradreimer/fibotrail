# MCP server

Tracking issue: [#143](https://github.com/bradreimer/fibotrail/issues/143).

## Purpose

This spec defines the MCP server decided in
[ADR-018](../adr/ADR-018-an-mcp-server-answers-questions-with-coarse-read-only-data.md). It lets
you ask Fibotrail questions in plain language from an MCP client, such as "How many times have I
been to a Disney park?", without a new Stats view for each question.

Think of the Stats screen as a printed menu and the MCP server as the kitchen window. The menu
lists the dishes someone planned. The window takes any order that the kitchen's basic ingredients
can make. The tools are the ingredients, and the model combines them.

```text
MCP client ──▶ POST /mcp ──▶ token check ──▶ tool ──▶ stats library + collections ──▶ database
                                              │
                                              └─▶ result: only fields on the allowed list
```

This spec covers the route, access, the tools and their results, the checks that keep a result
coarse, and the **MCP screen** in the app, which shows how to connect a client and which questions
work. It doesn't cover which client or model you use. Fibotrail doesn't choose one.

## User story

As a person with years of trips in Fibotrail, I want to ask questions about my travel in plain
language through an MCP client, so that I get answers without waiting for a new Stats view for
each question.

## Goals

- "How many times have I been to a Disney park?" is answered from one tool call, with both the
  number of separate trips and the number of park days.
- A new question needs no new code when the existing tools can answer it.
- Every tool gives the same number as the screen that shows it, because both call the same code.
- A tool result never holds a coordinate, a photo, a trip name, or a city name, and a test fails
  when it would.
- The feature is off until you set `MCP_TOKEN`.
- The MCP screen tells you how to turn the server on and how to connect an AI client, and it lists
  example questions. A test runs every example against a made-up library, so no example promises a
  tool that is missing or an input that a tool refuses.

## Non-goals

- Tools that write: adding, ignoring, renaming, or merging a trip, or marking a park.
- Calling Immich, or returning anything from Immich, such as photos, thumbnails, or user names.
- Running or calling a model from Fibotrail, and storing a model API key.
- A screen to create, show, or rotate the token. You set it in `.env`, and the MCP screen never
  shows it.
- Several people, each with their own token and their own trips.
- Streaming results and progress messages. Every tool returns one result.
- Prompts and resources from the MCP specification. Version 1 offers tools only.

## Requirements

### The route

- One route, `/mcp`, in the existing server container, per
  [ADR-006](../adr/ADR-006-one-container-on-the-immich-network.md). It uses the MCP streamable HTTP
  transport and needs no new port.
- The code is a new C# project, `Fibotrail.Mcp`, in `apps/server/src/`, with a test project in
  `apps/server/tests/`. The server project only maps the route and passes in the stats library.
- A tool reads only the stats library and the collections code, per
  [ADR-014](../adr/ADR-014-stats-views-are-self-contained-modules.md). It never reads the database,
  Immich, or a view.

### Access

- The route needs `Authorization: Bearer <token>`. The token is the value of `MCP_TOKEN`
  ([ADR-005](../adr/ADR-005-secrets-come-from-the-environment.md)).
- `MCP_TOKEN` is separate from the web session. A web session can't call `/mcp`, and the token
  can't call `/api`.
- With `MCP_TOKEN` empty or missing, `/mcp` returns 404 and the server logs "MCP is off" once at
  startup. A token shorter than 32 characters stops startup with an error that names the variable
  and the minimum length, never its value.
- A wrong or missing token returns 401 with no detail. The check takes the same time for a wrong
  token of any length.
- The token never appears in a log, an error, a result, or a response to the browser.
- `.env.example` lists `MCP_TOKEN` with an empty value.
- Requests over 60 a minute return 429. See open question 2.
- A request that carries an `Origin` header must name this server's own address, or it gets 403. A
  client that isn't a web page sends none, and passes. This stops another site from reaching
  `/mcp` through your browser.
- Only `POST` is allowed. Any other method gets 405 with `Allow: POST`, once the token is right.
- A body over 1 MiB gets 413.
- A wrong token doesn't use up the limit. Only requests with the right token count.
- The Settings screen shows one line, "MCP server: on" or "MCP server: off", and a button that
  opens the MCP screen. It never shows the token.

### What a result may hold

Every field in every result is one of these kinds. This is the vocabulary of ADR-018.

| Kind | Examples |
| --- | --- |
| Count or duration | `trips: 9`, `days: 34` |
| Date, to the day | `firstVisit: "2019-07-03"` |
| Country or continent name | `Sweden`, `Europe` |
| Collection, resort, or park name | `Disney parks`, `Walt Disney World Resort`, `Magic Kingdom` |
| Collection or country ID | `disney-parks`, `752` |
| Trip ID | `42` |
| Distance in kilometers, per trip | `distanceKm: 8120` |
| Yes or no | `markedByYou: false`, `truncated: true` |
| A fixed sentence that the code writes | "The list is cut short: 212 trips match and 200 are listed." |

These never appear in a result: latitude, longitude, a route or stop center, a photo ID, a file
name, a thumbnail, camera data, a trip name or note, a city or place name below a park, an Immich
URL, a key, or a user name.

- Every result field is declared with its kind in the code (`[Coarse(...)]`, or `[Nested]` for a
  field that holds another result). A test walks every tool's result type and fails on a field with
  neither, on a field named for data the table leaves out, such as a coordinate, a photo, or a
  city, and on a field whose type doesn't fit its kind. See the acceptance criteria.
- Error messages follow the same table. They hold no stack trace, file path, or SQL.

### What counts

- The rules are those of the [Stats screen](stats-screen.md#what-counts) and
  [collections](collections.md#what-counts), and the tools call the same functions.
- Only added trips are in any result. Suggested, later, and ignored trips change no number.
- Photos on the way count for nothing, per [on the way](on-the-way.md).
- A day with photos but no position counts for days away and for no country or park.
- **Trips** in a collection, resort, or park is the number of distinct added trips that visit it.
  A trip that visits two parks counts once in the collection.
- **Days** in a park is the number of distinct local days with a photo in it. **Days** in a
  collection or resort is the sum of the days of its parks, as the Collections screen shows, so a
  day with photos in two parks counts for both. A trip marked by hand adds a trip and no days, per
  collections.
- **Last visit** is new. It is the latest local day with a photo in the place, or the last day of
  the trip for a park that only you marked. A country with no day that has a position uses the last
  day of its latest trip. It is added to the stats library, with its tests, because ADR-014 puts
  every new fact there: `LastVisit` on `ParkStats` and on `CountryStats`.

### The tools

Each tool has a name, a description that states the counting rules above in a few sentences, and
a typed input. The description is part of the contract, because the model reads it to choose and
to explain. Every list result states its total and says when it was cut short.

| Tool | Input | Result |
| --- | --- | --- |
| `list_collections` | none | Each collection's `id`, name, and its trips, days, and parks visited out of its total |
| `collection_visits` | `collection` (an `id` such as `disney-parks`) | The collection's trips, days, parks visited out of total, first visit, last visit, one entry for each resort (trips), and one for each visited park (resort, trips, days, first visit, last visit, and whether you marked it) |
| `list_countries` | `continent` (optional) | Each visited country with trips and days, then the visited and total counts for the continent or the world |
| `country_summary` | `country` (a name or an `id`) | The continent, trips, days, first visit, and last visit |
| `list_trips` | `from`, `to` (dates), `country`, `collection`, `limit` (default 50, most 200) | For each added trip, newest first: its ID, start and end dates, days, distance in kilometers, countries, and the collections it visited |
| `get_trip` | `id` | The same fields as one `list_trips` entry, plus the visited parks |
| `days_away` | `by` (`month` or `year`) | Days away for each period with a trip, and the total |

- A tool takes the names and IDs it returns. `collection_visits("disney-parks")` works, and
  `list_collections` shows the IDs.
- An unknown collection or country is an error that lists the valid choices by name. A name that
  matches two countries is an error that lists both.
- An empty library is a result with zero counts, never an error.
- Names are matched ignoring case, so "sweden" finds Sweden.
- A date range counts a trip when any of its days falls inside the range. Counts are for the whole
  trip, not the clipped part.

### The Disney question

"How many times have I been to a Disney park?" is `collection_visits("disney-parks")`. The result
reads, for example:

```json
{
  "collection": "Disney parks",
  "trips": 9,
  "days": 34,
  "parksVisited": 7,
  "parksTotal": 12,
  "firstVisit": "2008-03-14",
  "lastVisit": "2024-11-02",
  "resorts": [
    { "name": "Walt Disney World Resort", "trips": 5 },
    { "name": "Disneyland Resort", "trips": 4 }
  ],
  "parks": [
    {
      "name": "Magic Kingdom",
      "resort": "Walt Disney World Resort",
      "trips": 5,
      "days": 9,
      "firstVisit": "2008-03-14",
      "lastVisit": "2024-11-02",
      "markedByYou": false
    }
  ]
}
```

"Times" can mean separate trips or park days, and the tool returns both, so the model can answer
"9 trips and 34 days". The numbers are those of the Collections screen's collection row.

### The MCP screen

A new destination, **MCP**, beside Inbox and Settings. It is a tool, not a main destination, so it
follows the rules of [app-shell.md](app-shell.md#navigation): an icon button in the top bar under
600 px, and an item in the sidebar from 600 px. Its address is `/mcp-setup`, because `/mcp` belongs
to the server. Settings has an **Open MCP** button that goes to it.

- It reads `GET /api/mcp/status`, which needs a session and returns `{ enabled, tools }`. It never
  returns the token.
- **When the server is off**, the screen shows "MCP server: off" and three steps: make a token with
  `openssl rand -hex 32`, add `MCP_TOKEN=` and the token to `.env`, and restart with
  `docker compose up -d --force-recreate`.
- **When the server is on**, the screen shows "MCP server: on", and three parts:
  1. **The address**, which is this page's origin plus `/mcp`, with a **Copy** button. On a plain
     HTTP address, on `localhost`, on a private IP address, and on a name with no dot or ending in
     `.local`, a note says that the address works only on your own network and a cloud client
     can't reach it.
  2. **The token**: a line that says it is the `MCP_TOKEN` value in `.env`, and that Fibotrail never
     shows it. The screen only ever shows the word `YOUR_MCP_TOKEN` where the token goes.
  3. **Three ways to add Fibotrail**, each in its own disclosure with a **Copy** button: the Claude
     Code command, a settings file for an app that starts a local command (using `mcp-remote`),
     and a settings file for a client that takes an address and headers.
- Under both states: **Questions to try**, **What it can't answer**, and **What your AI client
  sees**.
- **Questions to try** come from `apps/web/src/mcp/examples.json`. Each has a question, a plain
  line about what the answer holds, the tools it uses, and a **Copy** button. They are grouped as
  Collections, Countries, and Trips and time, and the first is "How many times have I been to a
  Disney park?". Every call in the file is run by a test against a made-up library, and every tool
  appears in at least one example.
- **What it can't answer** comes from the same file, with a reason for each: photos, exact places,
  trip names, and cities.
- Every copy button says "Copied", or "Couldn't copy. Select the text and copy it." The result is
  in a live region, and a button's accessible name says what it copies.
- The screen has the four screen states of the app shell: loading, error with **Try again**, and
  the two states above. Tap targets are at least 44 px high, code blocks wrap and never scroll
  sideways, and the screen has no color-only meaning. It passes the accessibility check.
- The steps follow each client's own documentation. A client's menus change, so the screen says to
  look for "MCP" or "connectors" in the client's help when a step doesn't match. See open question 1.

### Logging

- The server logs the tool name, the status, and the duration of each call, and never the token
  or a result.
- The server logs arguments only for a call that fails, and only the argument names.

## Acceptance criteria

- With `MCP_TOKEN` empty, `/mcp` returns 404, and `./scripts/check.sh` passes.
- With a token set, a request without it returns 401, and a request with it lists the seven tools.
- A token of 31 characters stops startup with an error that doesn't print the token.
- With three added trips to Disney parks, A (4 days at Walt Disney World), B (5 days there), and C
  (3 days at Walt Disney World and 2 days at Disneyland), `collection_visits("disney-parks")`
  returns 3 trips and 14 days. Walt Disney World shows 3 trips and 12 days, and Disneyland shows
  1 trip and 2 days.
- A fourth trip that is only suggested, ignored, or later changes no number in any tool.
- A park marked by hand adds one trip to its park, resort, and collection, and adds no days.
- The trips of `collection_visits` match the Collections screen, and the countries of
  `list_countries` match the Stats summary, in one test that builds the data once.
- A test walks every tool's result type, and fails when a field has no kind or a kind that isn't
  in the table.
- A test seeds a trip named "Mom's 70th" with a position, and shows that no tool result holds the
  name, a coordinate, or a photo ID.
- `country_summary("sweden")` and `country_summary("Sweden")` return the same result.
- An unknown collection returns an error that lists `disney-parks` and `universal-parks`.
- An empty library returns zero counts from every tool.
- `list_trips` with `limit` 500 returns 200 entries and says the list was cut short.
- The token appears in no log line, and no response body, in a test that makes a call with it.
- The Settings screen shows "MCP server: on" with a token and "MCP server: off" without one, and
  its **Open MCP** button opens the MCP screen.
- Opening `/mcp-setup` in a browser, or reloading it, shows the app and not the MCP endpoint.
- With the server off, the MCP screen shows the three steps, and with it on it shows the address and
  the three ways to connect. Neither shows a token.
- On a plain HTTP or private address, the MCP screen says the address reaches only your own network.
- Every example on the MCP screen is a question whose calls run without an error in a test, the
  first is the Disney question, and every tool is used by an example.
- The MCP screen passes the accessibility check in both states.
- A request with a foreign `Origin` gets 403, a `GET` gets 405, and the sixty-first request in a
  minute gets 429 with `Retry-After`. A wrong token doesn't use up the limit.
- The README says that nothing leaves your network unless you set `MCP_TOKEN` and connect a
  client, and that a result holds only the allowed kinds. No line of it still says that Fibotrail
  never sends location data anywhere.

## Open questions

1. **Which clients can reach a home server?** Not known yet. A cloud client has to reach `/mcp`
   over HTTPS through your reverse proxy, and a desktop client may need a local bridge. The three
   ways on the MCP screen follow each client's documentation and haven't been run against a real
   client yet. Check each one on a real setup, fix the screen where it differs, and record the
   steps in a knowledge document.
2. **Is 60 requests a minute the right limit?** It is a guess. A model can call several tools to
   answer one question. Measure on real use and change the number here.
3. **Should `list_trips` accept a text filter?** Trip names are excluded, so there is nothing to
   search. Leave it out until a question needs it.
