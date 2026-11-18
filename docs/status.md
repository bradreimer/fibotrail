# Current status

## Current milestone

Version 0.4.0 adds the read-only MCP server and the **MCP** screen
([#143](https://github.com/bradreimer/fibotrail/issues/143)), so an AI client can ask questions
such as "How many times have I been to a Disney park?" without a new Stats view for each one. The
server is off until `MCP_TOKEN` is set. Version 0.3.1 fixed `fibotrail join`, which saved a join
over a trip with no photo positions and changed nothing
([#144](https://github.com/bradreimer/fibotrail/issues/144)). Version 0.3.0 was the release after
0.2.0 (55 commits): Collections (Disney and Universal parks, with park days counted on the
server), cities and pins on the Map, the Fibonacci spiral on the Stats screen, a back arrow on
every detail page, and the command line tools `fibotrail rescan`, `places`, `join`, and
`trip add`, with a **Rescan everything** button in Settings. The release workflow publishes
`ghcr.io/bradreimer/fibotrail:0.4.0` and `:latest`. See [roadmap.md](roadmap.md) and
[the releases](https://github.com/bradreimer/fibotrail/releases).

**What wasn't recorded before the tag:** `./scripts/check.sh` and CI pass on the tagged commit.
The MCP server is checked in tests, and against a running server with the shipped collection files
and an empty library, where `initialize`, `tools/list`, and the Disney question answered over
HTTP. It hasn't run on the maintainer's library or a phone, and the connection steps on the MCP
screen haven't been run against a real AI client (open question 1 of the
[spec](specs/mcp-server.md)). `fibotrail join` ran on the maintainer's library on 2026-11-18 and
joined trips 2 and 45 into one trip of 1,635 photos. `fibotrail trip add` and the **Rescan
everything** button are checked in tests only, and haven't been run on the maintainer's library or
a phone. The ADR-007 table lines for version 0.1 and the per-line results of the accessibility
checklist are also still unrecorded.

## Completed

- [#157](https://github.com/bradreimer/fibotrail/issues/157) Search on the Trips screen
  ([spec](specs/trip-library.md#search)). A search field above the grid finds trips by name,
  destination, year, country, city, park, resort, or collection, so "disney" finds every trip to a
  Disney park. `GET /api/trips` now returns `keywords` for each trip, worked out by the stats
  library, so search agrees with the collections. Filtering happens on the device, and every word
  of the search must match. The search stays in the address (`/trips?q=...`), so going back from a
  trip page keeps it. Checked in tests only, not yet on a phone.
- [#151](https://github.com/bradreimer/fibotrail/issues/151) One **Insights** tab replaces the
  Collections and Stats tabs ([spec](specs/insights.md),
  [ADR-019](adr/ADR-019-insights-replaces-the-stats-and-collections-tabs.md)). It shows the six
  totals of the old Stats screen (trips, countries, cities, continents, days away, distance), then
  the collections. The tab bar holds Trips, Map, and Insights. The continent and country pages,
  Records, the Fibonacci spiral, and the season wheel are removed, on the web and the server,
  because the MCP server answers those questions. The totals moved from `GET /api/stats/summary`
  to `GET /api/insights/summary`, and the other `/api/stats/*` routes are gone. Collection pages
  moved from `/collections/...` to `/insights/...`, and old `/collections` and `/stats` addresses
  redirect. Checked in tests only, not yet on a phone.
- [#149](https://github.com/bradreimer/fibotrail/issues/149) An opened collection loads with a
  skeleton shaped like its content: a summary line, three resort rows, and a button. It replaces
  one 4:3 block that collapsed when the data arrived. The row height is an estimate (1.5 times the
  tap target), and it hasn't been measured against a real collection on a phone.
- [#143](https://github.com/bradreimer/fibotrail/issues/143) A read-only MCP server and an **MCP**
  screen
  ([ADR-018](adr/ADR-018-an-mcp-server-answers-questions-with-coarse-read-only-data.md),
  [spec](specs/mcp-server.md)). `/mcp` answers JSON-RPC over streamable HTTP with seven tools:
  `list_collections`, `collection_visits`, `list_countries`, `country_summary`, `list_trips`,
  `get_trip`, and `days_away`. "How many times have I been to a Disney park?" is one call to
  `collection_visits`. It is off until `MCP_TOKEN` is set (32 characters or more), checks the token
  in constant time, refuses a foreign `Origin`, and allows 60 requests a minute. A result holds only
  counts, dates, kilometers, IDs, and country, continent, collection, and park names, and a test
  walks every result type to keep it so. The new `Fibotrail.Mcp` project is pure, and its tools read
  the same stats library as the screens. The stats library gained `LastVisit` for parks and
  countries. The **MCP** screen (`/mcp-setup`, a tool in the top bar and the sidebar) shows how to
  turn the server on, the address, three ways to connect a client, and example questions that a test
  runs. Checked in tests only. The connection steps haven't been run against a real client yet
  (open question 1 of the spec), and the README privacy text now names the MCP exception.

- [#156](https://github.com/bradreimer/fibotrail/issues/156) `fibotrail trip pin <id> <photo>` and
  `unpin <photo>` repair a trip that detection left a photo out of. A pin is a decision that moves
  the photo to a day of the trip before the rules run (ADR-021). Asked for on 2026-11-18 to add one
  photo to trip 72. Checked in tests only, not yet run on the maintainer's library.

- [#155](https://github.com/bradreimer/fibotrail/issues/155) `fibotrail trip rename <id> <name>`
  renames any trip as a decision, so a rescan keeps the name. Found on the maintainer's library
  on 2026-11-18: `fibotrail join` kept the name of one joined trip, such as "Ontario, July 2010"
  for a trip that also covers Quebec, and the web rename needs a signed-in session. Checked in
  tests only, not yet run on the maintainer's library.

- [#144](https://github.com/bradreimer/fibotrail/issues/144) `fibotrail join` joins trips found
  from photos with no position (rules 3b and 4b), not only rule 3 trips. Found on the maintainer's
  library: trips 2 and 45, one Oregon Coast trip in July 2008, were a trip with GPS and a trip of
  1,449 photos with none, and the join was saved and changed nothing. A step after every rule now
  merges the trips that touch a joined range and rebuilds the merged trip from their photos
  ([ADR-016](adr/ADR-016-a-join-is-a-decision-over-a-range-of-days.md)). Checked in tests only,
  not yet run on the maintainer's library.

- [#140](https://github.com/bradreimer/fibotrail/issues/140) `fibotrail trip add` adds a trip by
  hand, for photos with no position that no rule can find. The trip is a decision in the new
  `manual_trip` table
  ([ADR-017](adr/ADR-017-a-manual-trip-is-a-decision-that-detection-builds-from-photos.md)). The
  motivating case is the 310 photos from 2005-07-31 to 2005-08-06, which Immich holds with no GPS
  and which rule 3b misses. Checked in tests only, not yet run on the maintainer's library.
- [#138](https://github.com/bradreimer/fibotrail/issues/138) A **Rescan everything** button in
  Settings, the button form of `fibotrail rescan`. It starts a full pass in the server through
  `POST /api/rescan`. Checked in tests only, not yet on a phone.
- [#136](https://github.com/bradreimer/fibotrail/issues/136) `fibotrail join` joins two trips that
  detection split by a pause of 3 days or more. The join is a decision in the new `trip_join`
  table ([ADR-016](adr/ADR-016-a-join-is-a-decision-over-a-range-of-days.md)), and it keeps the
  added status and name. Checked in tests only, not yet on the maintainer's library.
- [#131](https://github.com/bradreimer/fibotrail/issues/131) A back arrow on every detail page.
  A UX review of all 13 routes found the arrow only on the trip page. The collection, resort,
  stamp book, continent, and country pages had a small text link with no arrow. They now share
  one `BackLink` component with the trip page. The button on a "not found" page is unchanged.
  Checked in tests and on a phone.
- [#118](https://github.com/bradreimer/fibotrail/issues/118) and
  [#119](https://github.com/bradreimer/fibotrail/issues/119) Two fixes found on the Disney
  collection, which showed 8 trips for 10 real ones. A run of 2 or fewer photos at home between
  two away photos less than 24 hours apart no longer splits a trip (March 2023 was counted twice).
  **Mark parks visited** on the trip page counts trips with no photo positions (March 2006, 2007,
  and 2012) in a collection, as a decision. Their park days stay 0. Neither is deployed yet.

- [#116](https://github.com/bradreimer/fibotrail/issues/116) Collections expand in place. Each row
  on the Collections list opens like a section under the map, and shows the parks visited, days,
  trips, the resorts, and the Stamp book button. Each resort opens to its parks, a second level. The detail loads on the first open. Checked in
  tests only, not yet on a phone.
- [#111](https://github.com/bradreimer/fibotrail/issues/111) The Stats screen is simpler. Five
  views were removed, each by the four steps of ADR-014: Trips per year, Trip length, Theme parks,
  Travel clock, and Distance rings. Their specs went with them. The stats library lost the two
  facts only they read (the home location and the park sets), and the Trip spiral keeps its own copy
  of the styles it borrowed from the clock and the rings. Collections is unchanged and still reads
  the park days from the stats library. The Stats screen now shows the summary tiles, the continents,
  the Trip spiral, and Records. [ADR-013](adr/ADR-013-park-days-are-derived-from-stored-photo-positions.md)
  still says the Theme parks view shares the park-day function. It is history until someone edits it.
- [#127](https://github.com/bradreimer/fibotrail/issues/127) A resort is a card with its trips.
  Tapping it lists every added trip there, newest first, each with a badge for every park of the
  resort that the trip visited. Park cards left the resort path, and a park's days and first visit
  are in the stamp book. `GET /api/collections/{id}` gives each resort a `tripList`.
- [#110](https://github.com/bradreimer/fibotrail/issues/110) Collections lead with trips. The list
  card shows the trips to the collection, such as "9 trips". Tapping it lists the resorts with
  their trips, and tapping a resort lists its trips with park badges (#127). The progress ring, total
  days, and stamps moved to a stamp book one tap deeper. A trip to two parks counts once, and a
  trip to two resorts counts once for each. See the [collections spec](specs/collections.md).
- [#102](https://github.com/bradreimer/fibotrail/issues/102) and
  [#103](https://github.com/bradreimer/fibotrail/issues/103) City counts and names fixed from the
  first `fibotrail places` run on 52 trips. 25 km stays (89 cities; 10 km gave 116 and 50 km gave
  68). A stop with no city name, such as "Canada", no longer counts as a city, and a merged city
  takes the name found on the most trips. See
  [ADR-015](adr/ADR-015-cities-are-merged-places-and-theme-parks-keep-their-own-identity.md).
- [#64](https://github.com/bradreimer/fibotrail/issues/64) Park days in Collections and Stats. The
  park lookup and day count, the collection file loader and its startup checks, the Collections
  destination with its list, stamp book, and park sheet, the Theme parks Stats view, and the
  Theme parks view and its spec (both removed in [#111](https://github.com/bradreimer/fibotrail/issues/111)).
  Collections and the view read the same park days, and a test checked they agree. All 19 launch parks ship, each from OpenStreetMap with a closed
  ring. **Disney, 12:** Magic Kingdom (way 297428432), EPCOT (805298160), Disney's Hollywood Studios
  (806728401), Disney's Animal Kingdom (297441457), Disneyland Park (relation 5586855), Disney
  California Adventure (15626312), Tokyo Disneyland (1282875870), Tokyo DisneySea (203538370),
  Disneyland Park in Paris (775180147), Disney Adventure World (205734843, formerly Walt Disney
  Studios Park), Hong Kong Disneyland (377094298), and Shanghai Disneyland (494725605).
  **Universal, 7:** Universal Studios Florida (449808332), Universal's Islands of Adventure
  (relation 1124085), Universal Epic Universe (1372221500), Universal Studios Hollywood (1304528747),
  Universal Studios Japan (relation 5695002), Universal Studios Singapore (95756945), and Universal
  Studios Beijing (740165118, which OpenStreetMap names Universal Beijing Resort). The outlines
  couldn't be fetched from this environment on 2026-11-16 (three Overpass hosts refused or timed out),
  so the maintainer exported them and Claude typed them into the files by hand. **Validation:** the
  maintainer checks each outline in production, on real trips. A wrong outline is fixed by replacing
  its GeoJSON file, with no code change. The Japan outline comes from 2008 imagery and may be the
  roughest. ADR-013 planned new `trip_photo` columns,
  but the `photo` table already holds each photo's position and local time, which the stats library
  reads, so no migration was needed. Checked in a desktop browser only, not yet on a phone.

- [#48](https://github.com/bradreimer/fibotrail/issues/48) Cities on the life map and on the Stats
  screen, decided in
  [ADR-015](adr/ADR-015-cities-are-merged-places-and-theme-parks-keep-their-own-identity.md)
  (accepted). Stops of added trips within 25 km merge into one city, named by the stop with the most
  photos. Disney and Universal parks stay places of their own and aren't counted as cities. The Map
  shows a pin for each city and park once zoomed in, with a **Cities and parks** list and a sheet for
  each, and the Stats summary has a Cities tile. Regions stay at Canada and the USA, and every other
  country is one unit, so no new boundary data was added. Park pins appear only after the park
  boundary files are added. The 25 km radius and the time it takes are unchecked on the maintainer's
  library, and it is checked in a desktop browser only, not yet on a phone.

- Docker image build. `docker/Dockerfile` didn't copy `Fibotrail.Stats.csproj` before restoring, so an
  image build of `main` after the Stats screen would have failed at publish. It does now, and it
  copies `collections/` into the image.

- [#59](https://github.com/bradreimer/fibotrail/issues/59) The Fibonacci spiral of trips, a Stats
  view (spec removed in #151). Every added trip is a dot along a spiral of quarter circles
  in Fibonacci squares, oldest at the center, sized by days and colored by continent, with continent
  chips, **Earlier** and **Later** buttons, and a card that opens the trip. A trip takes the continent
  that has most of its countries. The seven continent colors are tokens, each at least 3:1 on Paper
  in light and dark mode. Checked in a desktop browser only, not yet on a phone, and with a few
  trips only, so how it looks at 60 or more is unknown. That is the spec's first open question.

- [#58](https://github.com/bradreimer/fibotrail/issues/58) The season wheel, a country page view
  (spec removed in #151). A ring of 12 months shaded by the country's days, four season
  arcs (solid Gold when seen, dashed when not), a center that reads "2 of 4 seasons seen", and a row
  for each season that dims the rest when tapped. Each day uses its own latitude for the
  hemisphere. The stats library now keeps each day of a country with a latitude. Checked in a
  desktop browser only, not yet on a phone. The spec's open question, whether one day makes a season
  seen, is unanswered: one day does.

- [#67](https://github.com/bradreimer/fibotrail/issues/67) The distance rings, a Stats view
  (removed in [#111](https://github.com/bradreimer/fibotrail/issues/111)). Each added trip is a dot at the bearing and the distance of its
  farthest stop from home, on a log scale out to 20,000 km, with five distance bands, **Nearer** and
  **Farther** buttons, and a text alternative. The home location is a new fact in the stats library,
  and the route returns distances and bearings only. `Geo.BearingDegrees` is new in
  `Fibotrail.Detection`. Checked in a desktop browser only, not yet on a phone. The two open
  questions in the spec (the farthest stop versus the main destination, and a fixed outer edge)
  are unanswered.

- [#66](https://github.com/bradreimer/fibotrail/issues/66) The travel clock, a Stats view
  (removed in [#111](https://github.com/bradreimer/fibotrail/issues/111)). A wheel of 12 months where each bar grows with the days you were
  away in that month, across every year, with a **Days** and **Trips** switch, **Earlier** and
  **Later** buttons, and the month's trips under the wheel. A wedge can be tapped on a touch screen,
  and the buttons cover the keyboard. It is checked in a desktop browser only, not yet on a phone.

- [#54](https://github.com/bradreimer/fibotrail/issues/54) The Stats screen host. A Stats tab with
  the summary tiles, continent cards with progress rings, a continent page, and a country page with
  trips and days ("3 trips · 19 days"). Server: the `Fibotrail.Stats` library, a continent table in
  `Fibotrail.Places`, and `GET /api/stats/*`. The first three views are trips per year, trip length,
  and records, each a self-contained module per
  [ADR-014](adr/ADR-014-stats-views-are-self-contained-modules.md). Not built yet: the continent's
  outline inside each ring. It is checked in a desktop browser only, not yet on a phone, and it
  isn't known how long the screen takes on the maintainer's 46 trips. Related views are
  [#58](https://github.com/bradreimer/fibotrail/issues/58),
  [#59](https://github.com/bradreimer/fibotrail/issues/59),
  [#66](https://github.com/bradreimer/fibotrail/issues/66), and
  [#67](https://github.com/bradreimer/fibotrail/issues/67).

- [#62](https://github.com/bradreimer/fibotrail/issues/62) Provinces, territories, and states on
  the Map. Canada's 13 and the USA's 51 fill when the Map is zoomed in, with a progress line for
  each country, a list of visited regions, and a sheet for each ([spec](specs/life-map.md#detail-regions),
  [ADR-012](adr/ADR-012-detail-regions-are-bundled-outlines-for-a-short-list-of-countries.md)). The
  region outlines add 132 KB gzipped. The Washington Monument falls in Virginia at 1:50 million, so
  DC can be missed. It is checked in a desktop browser only, not yet on a phone or an iPad.

- [#105](https://github.com/bradreimer/fibotrail/issues/105) Map tab navigation. Canada and the
  USA in the progress lines link to their region lists, and the sections under the map are
  collapsed until opened ([spec](specs/life-map.md#the-map-screen)). Checked in tests only, not yet
  on a phone.

- [#61](https://github.com/bradreimer/fibotrail/issues/61) Trips with no location. Rule 3b finds
  runs of 3 or more days with 30 or more photos a day and no position, which catches March 2006,
  March 2007, and March 2012. On the maintainer's library it adds about 29 Low confidence
  suggestions, and how many are real isn't known yet.

- [#133](https://github.com/bradreimer/fibotrail/issues/133) Short visits far from home. Rule 4b
  suggests a visit with 2 or more photos in all, more than 1,000 km from home, with no minimum
  days. It's always Low confidence. On the maintainer's library it adds 13 suggestions, among them
  the five Stockholm visits that rule 4 dropped, and no day trip near home. How many of the others
  are real isn't known yet.

- Image publishing. After CI passes on a push to `main`,
  `.github/workflows/publish-image.yml` builds `docker/Dockerfile` for amd64 and arm64 and pushes
  `ghcr.io/bradreimer/fibotrail:edge` and `:sha-<commit>`, for testing. `:latest` is for releases
  only. The first run was on 2026-11-15, before `edge` replaced `latest`, so `:latest` is a stale
  build of `main` until `v0.1.0` is tagged. The package
  starts private; make it public in the package settings to pull without signing in.
- [#1](https://github.com/bradreimer/fibotrail/pull/1) Design and branding document.
- [#2](https://github.com/bradreimer/fibotrail/issues/2) Project organization: instructions,
  work item templates, ADRs, specs, knowledge, and the tests that enforce the layout.
- [#3](https://github.com/bradreimer/fibotrail/issues/3) Project guidelines, the decisions on
  the stack, secrets, deployment, and the iOS experience, and the quality gates spec.
- [#12](https://github.com/bradreimer/fibotrail/issues/12) The spike. The gate "Immich fields
  confirmed" is met. The findings are in
  [immich-api-findings.md](knowledge/immich-api-findings.md), from Immich 3.4.1 on 2026-11-15,
  with a read-only key.
- [#5](https://github.com/bradreimer/fibotrail/issues/5) First run and sign-in. The .NET
  solution, the SQLite database, Argon2id password setup, sessions, the sign-in delay,
  `fibotrail reset-password`, the Dockerfile, and an example compose file. The setup and sign-in
  screens stay plain HTML from the server, so they work before a session exists.
- [#6](https://github.com/bradreimer/fibotrail/issues/6) The app shell. The React and Vite
  project in `apps/web`, the design tokens, self-hosted Fraunces and Inter, the three
  destinations, the four screen states, the web app manifest, icons, launch images, and the
  service worker. The server serves the built app, and the Docker image includes it. The UI
  checks run in `./scripts/check.sh`. How to work on it is in
  [web-app-development.md](knowledge/web-app-development.md).
- [#7](https://github.com/bradreimer/fibotrail/issues/7) Immich scanning. The generated Immich
  client with its four allowed reads, the connection check, the paged scanner that resumes from
  its cursor and reads only changes after the first run, the `photo` and `scan_run` tables, the
  thumbnail proxy and its cache, **Scan now**, and the progress banner. See
  [ADR-008](adr/ADR-008-a-generated-immich-client-limited-to-allowed-reads.md).
- [#8](https://github.com/bradreimer/fibotrail/issues/8) Trip detection. The rules as pure
  functions in `Fibotrail.Detection`, the home location step with its map, the `trip`,
  `trip_photo`, `decision`, and `setting` tables, reconciliation that keeps decisions across
  rescans, and the `fibotrail replay` command. See
  [ADR-009](adr/ADR-009-a-bundled-natural-earth-base-map.md) and
  [ADR-010](adr/ADR-010-stops-and-routes-are-stored-with-the-trip.md).
- [#9](https://github.com/bradreimer/fibotrail/issues/9) The trip inbox. Suggestion cards with
  a photo strip and a route preview, **Add trip** with a sheet for the name and the cover,
  **Not a trip**, **Later**, swipe on touch screens, the **Ignored** tab with **Restore**, and
  the badge on the Inbox destination.
- [#10](https://github.com/bradreimer/fibotrail/issues/10) The trip library. A grid of trip
  cards with a cover photo, the name, the dates, the day count, and the main destination, newest
  first, in 1, 2, or 3 columns by width, with the loading, empty, and error states.
- [#11](https://github.com/bradreimer/fibotrail/issues/11) The trip page. The header, the
  route map with pins and the stop sheet, the five stat tiles, the photo timeline by local day,
  the photo viewer with swipe, arrow keys, and **Open in Immich**, and **Rename or change
  cover**.
- [#126](https://github.com/bradreimer/fibotrail/issues/126) A back arrow. The trip page has a
  **Trips** link with an arrow at the top, so one tap returns to the trip library.

- [#31](https://github.com/bradreimer/fibotrail/issues/31) The main destination by days. A trip's
  main place is the city where the most days were spent, not the most photos. See the
  [main destination spec](specs/main-destination.md). A choice of names
  ([#30](https://github.com/bradreimer/fibotrail/issues/30)) was set aside on 2026-11-16: a name
  can already be typed, and a better first guess matters more.
- [#28](https://github.com/bradreimer/fibotrail/issues/28) The data folder check. A `/data` bind
  mount that the container can't write to now stops the server with one line that names the fix,
  not a repeating SQLite stack trace.
- [#36](https://github.com/bradreimer/fibotrail/issues/36) The README's backup and restore steps,
  fixed after running them on a real container. Builds of `main` report `edge-<commit>` on
  `/healthz`.
- [#38](https://github.com/bradreimer/fibotrail/issues/38) On an iPhone, the Add trip sheet was
  under the tab bar and **Add** was hidden. The cause was `-webkit-overflow-scrolling: touch` on the
  scroll area. It's removed, with a test, and the maintainer confirmed on the phone that the Add
  sheet, the photo viewer, and the map overlays now cover the tab bar.
- The review gate of step 3a, met on 2026-11-16 on the maintainer's library of 71,318 photos: 10 of
  the first 10 High-confidence suggestions accepted, and 5 of 5 remembered trips found. It replaced
  the replay test as the gate. See the [trip detection spec](specs/trip-detection.md#review-gate).
- The first real scan, on 2026-11-15. A full scan of the 71,318 photos finished, and detection
  found 34 suggestions. The Immich access log was checked for an incremental scan and for browsing
  thumbnails: only `GET /api/server/version`, `POST /api/search/metadata`, and
  `GET /api/assets/{id}/thumbnail` appeared, with no other path and no write method. The first full
  scan ran before logging was on, so `GET /api/assets/statistics` (read only at the start of a full
  scan) wasn't seen.

- Version 0.1.0, tagged on 2026-11-16 at `d7e0970`. The `v0.1.0` tag ran the release workflow for the
  first time. It checked the tag, the commit on `main`, and its CI run, then pushed `:0.1.0` and
  `:latest`. The image carries `FIBOTRAIL_VERSION=0.1.0`, the OCI version label, and runs as user
  1654. The maintainer's run of the manual accessibility checklist on an iPhone passed, apart from
  the tile shapes in the trip timeline
  ([#41](https://github.com/bradreimer/fibotrail/issues/41), fixed and confirmed).

- Version 0.2.0, tagged on 2026-11-16 at `d17f13c`. It adds the Map
  ([#45](https://github.com/bradreimer/fibotrail/issues/45), with the server side in
  [#46](https://github.com/bradreimer/fibotrail/issues/46) and
  [ADR-011](adr/ADR-011-visits-are-derived-from-added-trips-on-the-server.md)), hiding a photo from
  a trip's map ([#35](https://github.com/bradreimer/fibotrail/issues/35), see
  [on-the-way.md](specs/on-the-way.md)), and the navigation change
  ([#47](https://github.com/bradreimer/fibotrail/issues/47)): Trips and Map in the tab bar, Inbox and
  Settings in the top bar under 600 px, and all four in the sidebar from 600 px. The first look at the
  Map found outlines that cross the antimeridian covering the sea and matching stops to Russia
  ([#55](https://github.com/bradreimer/fibotrail/issues/55)), fixed before the tag. The image
  carries `FIBOTRAIL_VERSION=0.2.0` and runs as user 1654. On the maintainer's upgrade, Fibotrail
  copied the database to `fibotrail-backup-<time>.db` before applying the new table.

- [#60](https://github.com/bradreimer/fibotrail/issues/60) Measuring the on-the-way stop rule, the
  first step of [#32](https://github.com/bradreimer/fibotrail/issues/32). `fibotrail stops` reports
  which stops of each added trip the candidate rule flags, and why, and changes nothing. It was
  merged on 2026-11-16 and run on the maintainer's 46 added trips the same day. It flagged 199 of 288
  stops, far too many, and the maintainer set the automatic suggestion aside
  ([#32](https://github.com/bradreimer/fibotrail/issues/32), closed as not planned) in favor of
  hiding photos by hand. See [on-the-way.md](specs/on-the-way.md#result-of-the-first-run).

- [#120](https://github.com/bradreimer/fibotrail/issues/120) `fibotrail rescan`. It rereads every
  photo from Immich with no changed-since filter, then runs trip detection, so photos whose
  location you edited in Immich are picked up even when Immich doesn't change their update time.
  Tested with a stand-in Immich only. Not known yet: whether a location edit changes `updatedAt` in
  a real Immich, and so whether **Scan now** already finds such photos. Check it on the
  maintainer's library the first time the command is run, and record the answer in
  [immich-api-findings.md](knowledge/immich-api-findings.md). See
  [immich-scanning.md](specs/immich-scanning.md#rescan-command). **Scan now** is refused while the
  command runs ([#124](https://github.com/bradreimer/fibotrail/issues/124)).

## In progress

- [#153](https://github.com/bradreimer/fibotrail/issues/153) Finer coasts and islands on the trip
  page maps. Decided in
  [ADR-020](adr/ADR-020-trip-page-maps-use-the-1-10-million-outline.md): the route map and the
  photo viewer map use the Natural Earth 1:10 million outline (3,661 KB, 921 KB gzipped), embedded
  in the image and loaded on first use. The Map tab, the home location step, and the server keep
  1:50 million. The [spec](specs/trip-page.md#route-map) is updated. The maps pick the outline
  with a `detail` setting, and each outline file is fetched once, when a map first asks for it.
  Checked in tests and the build only. Not yet run in a browser or on an iPhone, so pan and zoom
  with 4,253 polygons and the Friday Harbor check are still open.

## Next

The plan is in [roadmap.md](roadmap.md). Specs are in [specs/](specs/README.md).

- Record what hasn't been checked on devices: the Map, the top bar buttons, the sidebar, and **Hide**
  on a phone and an iPad, and the rest of the ADR-007 table for
  [#6](https://github.com/bradreimer/fibotrail/issues/6),
  [#9](https://github.com/bradreimer/fibotrail/issues/9),
  [#10](https://github.com/bradreimer/fibotrail/issues/10), and
  [#11](https://github.com/bradreimer/fibotrail/issues/11). A failed line is fixed in a patch
  release, or recorded as a known gap. Also record the per-line results of the
  [accessibility checklist](knowledge/accessibility-checklist.md).
- Make the switches look like iOS switches: **Hide from map** in the photo viewer
  ([#52](https://github.com/bradreimer/fibotrail/issues/52)) and **Show routes** on the life map,
  and keep the photo viewer dark in dark mode
  ([#51](https://github.com/bradreimer/fibotrail/issues/51)). Built and checked in a desktop browser
  at phone size, not yet on a phone.
- Phase 5: collections, such as Disney and Universal parks, and the travel totals, now together on
  the Insights tab ([insights.md](specs/insights.md)). Collections are specified in
  [collections.md](specs/collections.md)
  ([#64](https://github.com/bradreimer/fibotrail/issues/64)). Park days are derived from stored photo
  positions, per [ADR-013](adr/ADR-013-park-days-are-derived-from-stored-photo-positions.md). The
  collections are built, and the park outlines are validated in production by
  the maintainer. What's left is the run-through of the Insights tab on a phone.
- Answer the open questions in the [life map spec](specs/life-map.md) with real use: whether the
  home country should always fill in, and whether 25 km is the right distance for a stop just off a
  coast.
- Answer the remaining open questions in the
  [design and branding document](knowledge/design-and-branding.md#risks-open-questions-and-next-steps).
  The server language is settled: see [ADR-004](adr/ADR-004-technology-stack.md).
- Review the app icon. The icon and launch images in `apps/web/public` are drawn from the logo
  construction by `npm run icons`. A designed logo can replace the drawing in that script.
