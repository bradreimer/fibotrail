# Web app development

This document describes how to work on the web app in `apps/web`: how to run it, where its parts
live, and the steps to repeat after certain changes. It was written on 2026-11-15, when the app
shell was built for [#6](https://github.com/bradreimer/fibotrail/issues/6). The rules it follows
are in the [app shell spec](../specs/app-shell.md).

## How the parts fit

```text
apps/web (Vite build) --> dist/ --> copied into the server's wwwroot in the Docker image
                                         |
Browser <-- page, scripts, fonts, icons -+-- ASP.NET Core server --> /api routes (JSON)
```

The server and the web app share one origin. The server sends three kinds of answers:

| Address | Answer | Needs a session |
| --- | --- | --- |
| `/setup`, `/sign-in` | Plain HTML from the server, with no script | No |
| `/assets/*`, `/icons/*`, `/splash/*`, `/sw.js`, `/manifest.webmanifest` | Static files of the web app | No |
| `/api/*` | JSON | Yes |
| Any other address without a file extension | The page of the web app, `index.html` | Yes |

## Run it

You need Node.js 22 and the .NET 10 SDK.

Start the server. It listens on port 8080 and keeps its data in `bin/data`:

```bash
cd apps/server/src/Fibotrail.Server && IMMICH_URL=http://localhost:2283 IMMICH_API_KEY=dev dotnet run
```

Start the web app. Vite serves it on port 5173 and forwards `/api`, `/setup`, `/sign-in`, and
`/sign-out` to the server:

```bash
cd apps/web && npm install && npm run dev
```

Open `http://localhost:5173/setup` the first time, to set a password. The service worker runs
only in a production build, so `npm run dev` never serves a stale file.

## Where things live

| Folder or file in `apps/web` | What it holds |
| --- | --- |
| `src/styles/tokens.css` | The design tokens. The only file with a color, a length, or a duration |
| `src/shell/` | The frame: header, navigation, scroll memory, and the install hint |
| `src/states/` | The four screen states: `Skeleton`, `ScanBanner`, `EmptyState`, and `ErrorState` |
| `src/screens/` | One file for each screen |
| `src/stats/` | The Stats host: the view registry, the view slot, and `views/<name>/` for each view. See [ADR-014](../adr/ADR-014-stats-views-are-self-contained-modules.md) |
| `src/api/` | The API client, the OpenAPI document, and the types generated from it |
| `src/service-worker.js` | The service worker. The build writes it to `sw.js` |
| `vite-plugins/` | The build steps that write `sw.js` and the launch image links |
| `public/` | The manifest, the icons, and the launch images |

## After you change an API route

The TypeScript types come from the server's OpenAPI document, in two steps:

```bash
cd apps/server && UPDATE_OPENAPI=1 dotnet test
```

```bash
cd apps/web && npm run api:types
```

The first command rewrites `apps/web/src/api/openapi.json` from the running server. The second
rewrites `src/api/schema.d.ts` from that file. Commit both. Without `UPDATE_OPENAPI=1`, the server
test fails when the file is out of date, and `npm run api:check` fails when the types are.

A route appears in the document only when its address starts with `/api/` and it returns a typed
result, such as `TypedResults.Ok(new SessionInfo(...))`.

## After the country outlines change

The server finds a stop's country with its own copy of the Natural Earth outlines, written from the
same `world-atlas` package that the web app draws ([ADR-011](../adr/ADR-011-visits-are-derived-from-added-trips-on-the-server.md)).
After the package changes, rebuild the copy and commit it:

```bash
cd apps/web && npm run outlines
```

It writes `apps/server/src/Fibotrail.Places/Data/countries.json.gz`. A web test fails when the copy
and the package disagree.

Russia and Fiji have rings that cross longitude 180, and drawn flat they cover the sea. Both the
script and the web map cut those rings with `src/map/antimeridian.mjs`, one shared function with its
own tests. Don't load the outlines without it.

## After the region outlines or the detail countries change

The regions of Canada and the USA come from Natural Earth's states and provinces, pinned to
release `v5.1.2`, and the list of detail countries is
`apps/web/scripts/detail-countries.json` ([ADR-012](../adr/ADR-012-detail-regions-are-bundled-outlines-for-a-short-list-of-countries.md)).
After either changes, rebuild both copies and commit them:

```bash
cd apps/web && npm run outlines
```

To use a copy of the Natural Earth file you already have, run
`node scripts/build-region-outlines.mjs path/to/ne_50m_admin_1_states_provinces.geojson`. The script
writes `apps/server/src/Fibotrail.Places/Data/regions.json.gz` for the server and
`apps/web/src/map/regions.json` for the map. A web test fails when the two disagree. The web copy is
one long line, so Prettier skips it.

## After you change the icon

The app icons and the iOS launch images are drawn by a script, from the spiral of the logo
construction:

```bash
cd apps/web && npm run icons
```

The script writes PNG files to `public/icons` and `public/splash`. Commit them. To support a new
iPhone or iPad screen size, add it to `splash-screens.json` and run the script again. A test fails
when the list and the files differ.

## What a test can't check

Vitest runs in jsdom, which has no layout and no Safari. The tests check the navigation mode for
each width, the screen states, and the rules in the style sheets. They can't check that the app
feels right on a phone. That check is the manual run-through in
[ADR-007](../adr/ADR-007-native-ios-feel-through-a-progressive-web-app.md), on a real iPhone with
the app installed to the home screen. The service worker needs HTTPS, or `localhost`.
