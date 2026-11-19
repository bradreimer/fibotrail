# Games

Tracking issue: [#166](https://github.com/bradreimer/fibotrail/issues/166).

## Purpose

This spec defines the Games tab: a hub of small daily games made from your own trips, the games API
that every game reads, and the stats that each game remembers. Each game has its own spec. The
first is [Timeline](timeline-game.md).

Think of the Games tab as a game shelf. The shelf (the hub), its labels (the registry), and the
scorebook (game stats) stay put. A game is a box you put on the shelf or take off, without moving
the other boxes. [ADR-022](../adr/ADR-022-games-are-plug-in-modules-behind-a-games-api.md) records
the decision.

```text
Database ─▶ GameLibraryLoader ─▶ GameLibrary ─┬─▶ Timeline (pure) ─▶ /api/games/timeline/* ─┐
            (the only reader)     (the games   └─▶ next game        ─▶ /api/games/next/*     ─┼─▶ web registry ─▶ Games tab
                                   API)                                                      │
game_result table ◀─ GameResultStore ◀─ graded results ─────────────────────────────────────┘
```

## User story

As a person with a library of trips, I want a Games tab with short daily games made from my photos,
and stats for each game, so that I have a fun reason to look back at my trips and a score to share
with friends.

## Goals

- Games is a tab, one tap from anywhere. The Games screen shows one card for each game.
- A card shows the game's name, a one-line description, whether today's puzzle is done, and the
  current streak.
- Adding, changing, or removing a game touches only that game's folders, one registry line on each
  side, and its spec.
- Every game reads the same input, the game library, through one loader. No game reads the
  database, Immich, or another feature directly.
- Each game remembers one result per day, on the server, so stats are the same on every device.

## Non-goals

- Leaderboards, friends, or accounts. Bragging is a share text that you paste anywhere.
- Games that need data the game library doesn't hold yet. A game that needs a new fact adds it to
  the game library first.
- Playing a past day's puzzle. Only today's puzzle is offered.

## Requirements

### The game library: the games API

- The game library is the one input of every game. It holds the added trips, each with its ID, name,
  start and end dates, main place, and its photos with an ID and the local time taken.
- A photo is in the game library when it's an image, not a video, and it isn't on the way
  ([on the way](on-the-way.md)).
- Only trips with the status "added" are in it, as for the [insights](insights.md).
- `GameLibraryLoader` in `Fibotrail.Server/Games/` builds it once per request. It's the only game
  code that reads the database. The library itself is plain records in the `Fibotrail.Games`
  project.
- A game needs a new fact → the fact is added to the game library and the loader, with a test. A
  fact is removed only when no game reads it.

### A game

- On the server, a game is a folder `Fibotrail.Games/<Name>/` of pure functions over the game
  library, and a folder `Fibotrail.Server/Games/<Name>/` with its routes under `/api/games/<id>/`.
- In the web app, a game is a folder `apps/web/src/games/<id>/` with its component and tests.
- Nothing imports a game's folder except the registry on each side. A web test enforces this.
- A game's daily puzzle comes from a seed made from the game's ID and the date, so the same date
  gives the same puzzle. The seed is an FNV-1a hash, not `string.GetHashCode`, which changes each
  run. The puzzle changes only when the game library changes, for example after you add a trip.

### The registry

- The server lists the games in `GameRegistry`. `GET /api/games` returns one entry per game: its
  ID, name, description, whether today is played, and its stats.
- The web app lists the games in `apps/web/src/games/registry.ts`: the ID, the icon, and the lazily
  loaded component. A game on the server with no web entry isn't shown, and a web entry with no
  server game isn't shown.

### Results and stats

- One table, `game_result`, holds one row per game per day: the game ID, the puzzle date, whether
  it was solved, the tries used, the most tries allowed, the marks of each try as a short text, and
  when it was played.
- The server grades every result from the guesses. The browser never sends a score. A second result
  for the same game and day doesn't change the first; the server answers with the stored one.
- A game's stats, computed by a pure function from its results:
  - **Played**: days with a result.
  - **Win %**: solved days divided by played days, rounded to a whole number. 0 when none played.
  - **Current streak**: solved days in a row that end today, or end yesterday when today isn't
    played yet. An unsolved day ends a streak.
  - **Best streak**: the longest run of solved days in a row.
  - **Tries**: for each number of tries, how many solved days used it.
- "Today" is the date the browser sends, in its own time zone. The server accepts a date only from
  yesterday to tomorrow of its own clock, which covers every time zone.
- Removing a game leaves its rows in `game_result`. They're small and harmless, and come back if the
  game returns.

### Sharing

- A share text has the game name, the puzzle number, the tries, and one line of marks per try. It
  holds no trip name, place, date, or photo, so it's safe to paste anywhere.
- The puzzle number counts days from 2026-11-19, which is #1.
- Share uses the system share sheet when the browser has one, and copies to the clipboard
  otherwise.

### Screens

- The tab bar holds Trips, Map, Insights, and Games. The sidebar holds the same four, then Inbox,
  MCP, and Settings.
- `/games` is the Games screen. `/games/<id>` opens a game, with a back link to Games.
- An unknown game ID opens the Games screen.

## Acceptance criteria

- The tab bar shows Games, and the Games screen shows a Timeline card with its state and streak.
- Removing a game's line from both registries removes its card and route, and no other test fails.
- The stats are the same on a phone and a computer.
- Sending a result twice for one day keeps the first.
- The share text has no trip name, place, date, or photo.

## Open questions

- Should a past day's puzzle be playable, without counting toward stats? Not until it's asked for.
