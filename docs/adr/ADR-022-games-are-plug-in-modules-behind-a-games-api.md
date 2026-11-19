# ADR-022: Games are plug-in modules behind a games API

## Status

Accepted

## Context

The Games tab ([games.md](../specs/games.md)) starts with one game, Timeline, and more ideas are
waiting, such as a Wordle-style trip guess and a brick breaker made of visited countries. Which
games are worth keeping isn't known yet. A game that reaches into the trip tables, the detector, or
another screen is expensive to remove, and a change to those features could break it.

Games also need something no other feature has: state of their own, the results that make stats
and streaks.

[ADR-014](ADR-014-stats-views-are-self-contained-modules.md) solved a similar problem for stats
views with self-contained modules joined by routes and a registry. That pattern worked; the views
were removed for product reasons, not because of the pattern.

## Decision

Each game is a **plug-in module**. It reads one input, the **game library**, writes only to one
shared **results table**, and joins the app through its routes and one registry entry on each side.

```text
 Other features            Games API                     Games
 ─────────────────         ─────────────────────         ─────────────────────────────────
 trip, trip_photo,  ─▶     GameLibraryLoader  ─▶         GameLibrary ─▶ Timeline (pure)
 photo, on_the_way         (only reader)                              ─▶ next game (pure)
                                                         GameResultStore ◀─▶ game_result
```

**On the server**

- A new C# project, `Fibotrail.Games`, with a test project. It holds the game library records, the
  daily seed, the stats calculator, and one folder per game, as pure functions. It references no
  other Fibotrail project, so a game can't reach other features by accident.
- `Fibotrail.Server/Games/` holds the only code that touches the rest of the app:
  `GameLibraryLoader` (reads the database into the game library), `GameResultStore` (reads and
  writes `game_result`), `GameRegistry` (the list of games), and one folder per game with its
  routes under `/api/games/<id>/`.
- Each game route has its own typed result in the OpenAPI document.

**In the web app**

- `apps/web/src/games/` holds the hub parts: the card, the stats panel, sharing, and
  `registry.ts`. Each game lives in `apps/web/src/games/<id>/`, loaded lazily.
- Nothing imports a game's folder except the registry. A test enforces this.

**The rules a game follows**

1. A game reads only the game library and its own results. It never reads the database, Immich, or
   another game.
2. A game's only stored state is its rows in `game_result`. It adds no table or setting.
3. Shared game rules, such as the seed, stats, and the share text, live in the hub, not in a game.
4. Each game has its own spec.

**Removing a game** is four steps, with no migration: delete its server folders and tests, delete
its web folder, remove its line from each registry, and regenerate the OpenAPI document and the
TypeScript types.

## Alternatives considered

- **Reuse the stats library.** It already holds added trips, but not photos with their times, and
  its job is counting for the insights and the MCP server. Mixing photo picks into it would couple
  games to those features. A separate, smaller game library keeps both simple.
- **Keep results in the browser's storage.** No table, but stats would differ per device and could
  be edited by hand, which defeats bragging. Set aside.
- **A table per game.** Each game could store what it likes, but each would bring a migration, and
  removal would need one too. One generic table with marks as text covers daily puzzles.
- **Grade in the browser and send a score.** Fewer routes, but a score can't be trusted. The server
  grades from the guesses.

## Consequences

- Adding or removing a game touches its folders, one registry line on each side, and one spec.
- The game library is the one place where games are coupled to the rest of the app. It's the API
  that's maintained and extended as games need new facts.
- The tab bar grows to four tabs: Trips, Map, Insights, and Games. iOS tab bars hold up to five.
  This updates the tab list of [ADR-019](ADR-019-insights-replaces-the-stats-and-collections-tabs.md)
  without changing its decision.
- `game_result` stays after a game is removed. Its rows are small and unused.
