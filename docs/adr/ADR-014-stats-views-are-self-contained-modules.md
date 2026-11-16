# ADR-014: Stats views are self-contained modules

## Status

Accepted

## Context

The Stats screen ([spec](../specs/stats-screen.md)) will show several visuals: trips per year, a
trip length histogram, records, a season wheel ([spec](../specs/season-wheel.md)), and a Fibonacci
spiral of trips ([spec](../specs/trip-spiral.md)). More ideas are waiting, such as a travel clock
and distance rings.

Which visuals are worth keeping is not known yet. The maintainer expects to change their mind after
living with them on real data. A visual that is woven into the trip tables, the detector, or the
other screens is expensive to remove, so each one would carry the risk of that choice. The goal is
that adding or removing a visual is a small, local change.

Two facts make that possible:

- Every number on the Stats screen comes from added trips, and nothing writes back. A visual only
  reads.
- [ADR-011](ADR-011-visits-are-derived-from-added-trips-on-the-server.md) already derives visited
  countries on each request, with no stored table. The same approach works for every stat.

## Decision

Each stats visual is a **view**: a self-contained module on the server and in the web app, joined
to the rest of the app only by its API route and one registry entry on each side.

Think of the Stats screen as a wall of picture frames. The wall (the host) and the hooks (the
registry) stay put. A view is a picture you hang or take down without touching the wall or the
other pictures.

```text
 Database ─▶ Stats library ─┬─▶ View A (pure function) ─▶ GET /api/stats/a ─▶ web view A ─┐
 (added       (one shared,  ├─▶ View B                  ─▶ GET /api/stats/b ─▶ web view B ─┼─▶ registry ─▶ Stats screen
  trips)       read-only)   └─▶ View C                  ─▶ GET /api/stats/c ─▶ web view C ─┘              or country page
```

**On the server**

- A new C# project, `Fibotrail.Stats`, with a test project. It needs no database or running Immich.
- **The stats library** is the one shared input: the added trips with their dates, days, stops,
  distance, and, for each day, the countries and latitudes of its positioned photos. The server
  builds it once per request from the database and `Fibotrail.Places`. It is read-only.
- **Each view** lives in its own folder, `Fibotrail.Stats/Views/<Name>/`, as a pure function from
  the stats library to the view's result type. Its tests live in the matching folder of the test
  project.
- **Each view has its own route** under `/api/stats/`, such as `GET /api/stats/trip-spiral`, with
  its own result type in the OpenAPI document. The routes are mapped from one list in the server,
  one line per view.

**In the web app**

- Each view lives in its own folder, `apps/web/src/stats/views/<name>/`: its component, its API
  call, and its tests.
- One registry, `apps/web/src/stats/registry.ts`, lists the views in order, each with its **slot**:
  the Stats screen or the country page. The host screens render whatever the registry lists, and
  each view is loaded lazily, so a removed view leaves nothing in the bundle.

**The rules a view follows**

1. A view reads only the stats library. It never reads the database, Immich, or another view.
2. A view has no table, migration, setting, or decision of its own, and writes nothing.
3. Nothing imports a view's folder except the registry, on each side. A test enforces this.
4. Shared rules, such as what counts as visited or how a day is placed in a country, belong in the
   stats library or `Fibotrail.Places`, not in a view. A view may compute its own rules, such as
   seasons, privately.
5. Each view has its own spec. Removing a view removes its spec too.

**Removing a view** is four steps, with no data migration: delete its server folder and tests,
delete its web folder, remove its line from each registry, and regenerate the OpenAPI document and
the TypeScript types.

## Alternatives considered

- **One `GET /api/stats` route that returns everything.** Simple to call, but every view's data
  shares one response type, so removing a view changes a contract that every other view reads. Set
  aside.
- **A generic route, `GET /api/stats/views/{id}`, with an untyped result.** Fewer lines to register,
  but the OpenAPI document and the TypeScript types lose each view's shape. Set aside in favor of
  one typed route per view.
- **Stored stats tables, refreshed after each scan.** Faster on a huge library, but each view would
  bring a migration and a way to go stale, which is exactly what makes removal expensive. Set aside,
  as in ADR-011. If a library grows to thousands of trips, cache the stats library, not the views.
- **Views computed in the browser from the trips list.** No server code per view, but the rules
  couldn't be tested with plain data on the server, and the life map and collections need the same
  rules there. Set aside.

## Consequences

- Adding or removing a visual touches one folder on each side, one registry line on each side, and
  one spec. Nothing else changes, so trying a visual is cheap.
- The stats library is the one place where views are coupled. A view that needs a new fact adds it
  there, and the stats library's tests cover it. Removing a view never removes a fact from it unless
  no other view reads that fact.
- Two views may compute similar things separately, such as the trips in date order. That small
  duplication is accepted as the price of independence.
- Each view is one more request on the screen. With a few views and tens of trips, that is a few
  milliseconds each.
- The host screens can't assume any view is present. With no views registered, the Stats screen
  still shows its summary tiles.
