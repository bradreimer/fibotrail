# ADR-020: Trip page maps use the 1:10 million outline

## Status

Accepted

## Context

The [trip page](../specs/trip-page.md) has two maps: the route map, and the small map in the photo
viewer that shows where one photo was taken. Both draw on the bundled Natural Earth country outline
at 1:50 million scale, as
[ADR-009](ADR-009-a-bundled-natural-earth-base-map.md) decided. The trip page is where you zoom in
the most, so it's where that scale shows its limits: small islands are missing, and a stop on a
coast can look as if it sits in the water
([#153](https://github.com/bradreimer/fibotrail/issues/153)).

ADR-009 set aside the 1:10 million outline for its size. This ADR revisits that choice for the trip
page only.

What is known, measured on 2026-11-18 from `world-atlas@2.0.2`:

| Scale | Countries | Polygons | Size | Size gzipped |
| --- | --- | --- | --- | --- |
| 1:50 million | 241 | 1,616 | 756 KB | 230 KB |
| 1:10 million | 255 | 4,253 | 3,661 KB | 921 KB |

The gzipped sizes are at level 9. The Vite build reports them at its default level: 243 KB and
986 KB. What the browser downloads depends on how the server compresses the file.

A point-in-polygon test on 15 island and coast places found 3 in the water at 1:50 million that are
on land at 1:10 million:

| Place | 1:50 million | 1:10 million |
| --- | --- | --- |
| Friday Harbor, San Juan Island, Washington | Water | Land |
| Kahoolawe, Hawaii | Water | Land |
| Charlottetown, Prince Edward Island | Water | Land |
| Capri, Italy | Water | Water |
| Bora Bora, French Polynesia | Land | Water |
| Lanai and Molokai, Hawaii; Key West; Martha's Vineyard; Nantucket; Mackinac Island; Santorini; Catalina Island; Victoria, BC; Grand Cayman | Land | Land |

Two places tested as water at 1:10 million. Capri, about 10 square kilometers, isn't in the data.
Bora Bora tested as land at 1:50 million and water at 1:10 million, so the finer coast moved away
from the test point. The test points are rounded to two decimals, so one result on a coast can be
off.

The service worker caches every file under `/assets/` when it installs, so every file of the app
reaches the browser once per version, whether a screen uses it or not.

The [quality gates spec](../specs/quality-gates.md) sets no size budget for the app. ADR-012 named
one, but none exists yet.

## Decision

The two maps on the trip page draw on the Natural Earth 1:10 million country outline. The other
maps keep the 1:50 million outline.

- **The data.** `world-atlas/countries-10m.json`, from the package and version that already supply
  the 1:50 million file. Natural Earth is in the public domain. It's used as the package ships it,
  so the web app needs no build step for it.
- **Which maps.** The route map, its full-screen view, and the photo viewer map use 1:10 million.
  The Map tab and the home location step keep 1:50 million. So does the server, which matches
  stops to countries ([ADR-011](ADR-011-visits-are-derived-from-added-trips-on-the-server.md)) and
  regions ([ADR-012](ADR-012-detail-regions-are-bundled-outlines-for-a-short-list-of-countries.md)).
  Visited-country counts don't change.
- **Embedded in the image.** The file is part of the web app build, so it ships inside the
  container image. The container doesn't download it at start, and it doesn't use a cache volume.
- **Loaded on first use.** The trip page fetches the file the first time it shows a map, from
  Fibotrail. The file isn't in the first page load. The service worker then serves it from its
  cache.
- **One file per map.** A trip page map loads only the 1:10 million file. It doesn't draw the
  1:50 million file first and swap.
- **The style doesn't change.** Water, land, and borders, with colors from the design tokens, in
  light and dark mode. No labels, no tiles, and no outside address, as in ADR-009.

ADR-009 stays accepted. It still decides the base map for every other map, and its rule of no
outside request holds for this one.

## Alternatives considered

- **Download the file when the container starts, into a cache volume.** A smaller image. Set aside
  because the file is 3.7 MB. A download adds an outside call from the container, a step that can
  fail on first boot or offline, and a version and checksum to check, for a file that changes only
  when the package does. A cache volume fits much larger data, such as self-hosted vector tiles.
- **1:10 million everywhere.** One outline for every map and the server. Set aside because the Map
  tab shows the whole world, where 1:50 million is enough. On the server, more detail would move
  stops between countries and change the counts. That is a separate decision.
- **Draw 1:50 million first, then swap to 1:10 million when you zoom in.** A faster first draw.
  Set aside because the service worker caches both files anyway, and two sources for one map add
  code and a visible change in the outline while you zoom.
- **Round the coordinates to make the file smaller.** ADR-012 does this for regions. Set aside for
  now because the package file is already quantized TopoJSON. A rounded copy is a build step to
  keep, for a saving that hasn't been measured.
- **Self-hosted vector tiles.** Streets, labels, and detail at city scale. Set aside because the
  data is 1–10 GB per region, which needs its own story and ADR.

## Consequences

- With the default setup, Fibotrail still makes no outside request for a map, from the browser or
  from the container.
- The container image grows by about 3.7 MB.
- The service worker downloads 921 KB more, gzipped, once per app version, because it caches
  every asset when it installs. On a phone on a cellular network, that's part of the cost of each
  update.
- The trip page draws 4,253 polygons instead of 1,616. It must be checked on an iPhone against the
  table in [ADR-007](ADR-007-native-ios-feel-through-a-progressive-web-app.md) for pan and zoom.
- A stop can sit on land on the trip page and still count in no country, or the nearest one, on
  the server, because the server matches against 1:50 million. This is the accepted limit.
- Tests must check that the trip page maps use the 1:10 million file, that the Map tab and the home
  location step still use 1:50 million, and that no style has a tile source, font address, or image
  address.
- Some small islands, such as Capri, are still missing, and a stop near a coast can still look as
  if it sits in the water.
