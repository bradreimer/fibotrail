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
| `src/insights/` | The parts of the Insights screens: the totals, a collection's body, and the place sheet. See [insights.md](../specs/insights.md) |
| `src/games/` | The Games tab: the registry, the card, the stats panel, sharing, and one folder per game. Only `registry.tsx` imports a game. See [games.md](../specs/games.md) |
| `src/api/` | The API client, the OpenAPI document, and the types generated from it |
| `src/service-worker.js` | The service worker. The build writes it to `sw.js` |
| `vite-plugins/` | The build steps that write `sw.js` and the launch image links |
| `public/` | The manifest, the icons, and the launch images |

## Where server code goes

The server in `apps/server/src/Fibotrail.Server` follows one rule: static extension methods wire
things together, and classes hold behavior. `ServerApp.Create` is the composition root. It runs
three steps, and each step lives in `Hosting/`:

```text
ServerApp.Create
 ├ AddFibotrailServer    Hosting/ServiceRegistration.cs   core, Immich, features, web, auth
 ├ UseFibotrailPipeline  Hosting/RequestPipeline.cs       the middleware, in order
 └ MapFibotrailRoutes    Hosting/RouteMap.cs              every route group, then the fallback
```

| Change | Where it goes |
| --- | --- |
| A new service | The matching set in `Hosting/ServiceRegistration.cs`. A command uses only `AddFibotrailCore` and `AddFibotrailImmich` |
| New middleware | A class with `InvokeAsync` in the feature's folder, such as `Auth/AntiforgeryMiddleware.cs`, added in `Hosting/RequestPipeline.cs`. The order is part of the security model, and `PipelineTests` checks it |
| New routes | A `<Feature>Routes.cs` file in the feature's folder with a `Map<Feature>Routes` extension method, called from `Hosting/RouteMap.cs`. The sign-in screens are in `Auth/AuthRoutes.cs` |
| Work after the server starts | An extension method on `WebApplication` in the feature's folder, such as `Immich/ImmichStartup.cs`, called from `EntryPoint.ServeAsync` |
| An HTML screen from the server | `Web/Pages.cs` for the markup, `Web/HtmlResults.cs` to send it |

A public route must also be added to `ServerApp.PublicRoutes`, or `RouteProtectionTests` fails.
This layout was set by [#169](https://github.com/bradreimer/fibotrail/issues/169).

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

## Sending a file to the server

GPS trails are the first route that takes a file. The pattern, from
[#193](https://github.com/bradreimer/fibotrail/issues/193):

- **Send the raw file as the body**, with its name in a query parameter. `apiUpload` in
  `src/api/client.ts` does this. A minimal API handler that takes `IFormFile` requires ASP.NET's
  own anti-forgery middleware, which Fibotrail replaces with its own, so a form upload doesn't fit.
- **Raise the size limit on the route only.** The server's default request limit is 30 MB. The
  trail routes set `IHttpMaxRequestBodySizeFeature` to one byte past their own 50 MB limit before
  they read, then answer 413 with a plain message when the body is larger.
- **Never build a path from the file's name.** The stored file gets a random name, and the name
  you picked is kept as text.
- **Parse XML with no DTD.** `DtdProcessing.Prohibit` and no resolver, so a file can't make the
  parser read another file.

## What a test can't check

Vitest runs in jsdom, which has no layout and no Safari. The tests check the navigation mode for
each width, the screen states, and the rules in the style sheets. They can't check that the app
feels right on a phone. That check is the manual run-through in
[ADR-007](../adr/ADR-007-native-ios-feel-through-a-progressive-web-app.md), on a real iPhone with
the app installed to the home screen. The service worker needs HTTPS, or `localhost`.
