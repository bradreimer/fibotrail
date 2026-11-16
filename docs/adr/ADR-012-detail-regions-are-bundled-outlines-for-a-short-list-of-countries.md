# ADR-012: Detail regions are bundled outlines for a short list of countries

## Status

Accepted

## Context

The [life map spec](../specs/life-map.md#detail-regions) tracks the provinces and territories of
Canada and the states of the USA, because the maintainer travels there most
([#62](https://github.com/bradreimer/fibotrail/issues/62)). Four things need a decision: where the
region outlines come from, how big they are, where visits to regions are worked out, and what counts
as a region.

What is known:

- [ADR-009](ADR-009-a-bundled-natural-earth-base-map.md) bundles Natural Earth country outlines at
  1:50 million scale, and the app makes no outside request for a map.
- [ADR-011](ADR-011-visits-are-derived-from-added-trips-on-the-server.md) derives visited countries
  on the server from the stored stops of added trips, and leaves regions to their own ADR.
- Natural Earth publishes states and provinces ("admin-1") at 1:50 million and 1:10 million scale.
  Filtered to Canada and the USA, measured on 2026-11-16 with coordinates rounded to 3 decimal
  places:

  | Scale | Features | Size | Size gzipped |
  | --- | --- | --- | --- |
  | 1:50 million | 64 (13 Canadian, 51 US) | 432 KB | 132 KB |
  | 1:10 million | 64 | 2,188 KB | 601 KB |

- At 1:50 million the 51 US features are the 50 states and the District of Columbia. Puerto Rico and
  the other territories are separate countries in this data, so they are not among the 51. The 13
  Canadian features are the 10 provinces and 3 territories.
- No ring in the 1:50 million Canada and USA features jumps across the antimeridian, so the
  [cut](ADR-011-visits-are-derived-from-added-trips-on-the-server.md) that Russia and Fiji need isn't
  needed for them.
- The country file is 756 KB, and about 240 KB gzipped.

The size of a library is not a constraint. It has tens of added trips, so matching a few hundred
stops against 64 more outlines is fast.

## Decision

Detail regions use the Natural Earth 1:50 million states and provinces for a short list of
countries, bundled with the app and matched on the server.

- **The data.** Natural Earth admin-1 at 1:50 million, which is in the public domain, filtered to the
  detail countries, with coordinates rounded to 3 decimal places. About 132 KB gzipped for Canada and
  the USA. A script, `npm run outlines`, builds it from the same source as the country outlines, and
  the server and the web app each embed a copy, as for countries in ADR-011.
- **The list is data.** The detail countries are a short file, `apps/web/scripts/detail-countries.json`,
  of ISO 3166-1 alpha-3 codes, each with the plural noun for its regions ("states", "provinces and
  territories"). It starts with `CAN` and `USA`. Adding one means adding a row and rebuilding the
  outlines. Each added
  country adds its own size, which the script prints.
- **Matching.** A new pure function in `Fibotrail.Places`: stop position and a detail country in,
  region out. It first finds the country, then tests only that country's regions. The 25 km
  nearest-region rule from the spec applies, with the same value.
- **No tables.** Visits to regions are derived on each request, as for countries. No `place` or
  `visit` table is added. A feature that needs a stored match, such as a collection with its own
  boundary, adds its own ADR.
- **What counts as a region.** The 51 US features count as states, with the District of Columbia
  named "District of Columbia". US territories are not regions of the USA. Canada's 13 count as
  provinces and territories.
- **Names and IDs.** A region's name and its ISO 3166-2 code (for example `CA-ON` and `US-TX`) come
  from the outlines, never from Immich. The code is the region's `id` in the API.
- **Tapping a small region.** The visited-regions list is also a way in: a row opens the same sheet
  as a tap on the map. Prince Edward Island and the District of Columbia are a few pixels wide on a
  phone, so the map alone can't be the only way to reach them.
- **Zoom.** Regions draw from zoom 2.5, where Canada is about 700 px wide. Below that, the country
  fills as a whole, as today. The whole-country fill of a detail country is a layer with a maximum
  zoom of 2.5, not a zoom in a data expression: MapLibre reads a zoom in a data expression per tile,
  in whole zooms, and the first version left a detail country filled whole between zoom 2.5 and 3.
- **Where the web copy loads.** Only the Map screen asks for the region outlines. The trip page
  maps don't.

## Alternatives considered

- **1:10 million scale.** Coasts and borders that hold up when you zoom in. Set aside because it is
  601 KB gzipped, 4.5 times the size, for a map that has no streets and no labels. It is the choice to
  revisit if the 1:50 million coasts put stops in the water too often.
- **A Canada and USA source with finer boundaries, such as the Census cartographic files.**
  Boundaries that match what a US reader expects. Set aside because it is a different source for
  each country, with its own license, name format, and update schedule, and the list is meant to
  grow.
- **Immich's state names with no outlines.** No new data. Set aside because the names are free text
  that differ between Immich versions, a stop on a coast has no match, and there is no outline to
  fill.
- **Regions for the whole world.** One rule for every country. Set aside because the 1:50 million data
  covers only a few countries, and the 1:10 million data for the world is 40.7 MB before filtering.
- **Store `place` and `visit` rows for regions.** The design document's model. Set aside for the same
  reason as in ADR-011: a migration and a place to go stale, for 64 outlines that are cheap to test.
- **Load the region file from a server on demand.** A smaller first download. Set aside because it
  breaks the rule that the browser talks only to Fibotrail, unless Fibotrail serves it, which is
  what bundling already does.

## Consequences

- With the default setup, Fibotrail still makes no outside request for a map.
- The bundle grows by about 132 KB gzipped for Canada and the USA, 55 percent of the country file.
  Load it with MapLibre, on first use of the map, and cache it with the service worker. The initial
  load must stay within the quality gates budget, and the build checks this.
- Detail is coarse. At 1:50 million, a stop near a border between two regions, or on a coast, can
  fall in the wrong region or in none. The nearest-region rule covers the second case. The first is
  an accepted limit, and the first real library may change it.
- The District of Columbia is about 177 square kilometers, and at this scale its outline is a
  9-point diamond. Measured: the White House and the Capitol fall in DC, and the Washington Monument
  (38.8895, -77.0353) falls in Virginia, because the outline's western edge cuts across the National
  Mall. This is the accepted limit. A trip to DC that only has photos on the Mall's west end can
  miss the District until the 1:10 million data is used, at 601 KB gzipped.
- The detail list is short on purpose. Each country added is a license check, a size, and a name
  check, not only a code.
- Tests must include: a stop in each of two regions, a stop 10 km off a coast, a stop 40 km from
  every region, a stop in a country that isn't on the list, and the counts 13 and 51.
- The outlines ship in two places, as with countries. A web test compares the two lists of regions,
  so a change to the source fails the check until the copy is rebuilt.
- Cities are still undecided. See [#48](https://github.com/bradreimer/fibotrail/issues/48).
