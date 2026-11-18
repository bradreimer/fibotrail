# Collections

Tracking issue: [#64](https://github.com/bradreimer/fibotrail/issues/64).

## Purpose

This spec defines collections, the first part of phase 5 of the [roadmap](../roadmap.md). A
collection is a named set of places, such as "Disney parks." Fibotrail matches your photos to each
place and shows a stamp for every place you've been to.

The [design document](../knowledge/design-and-branding.md) and
[ADR-003](../adr/ADR-003-collections-are-data-files.md) decide that a collection is a data file
and that a place counts as **visited** when enough photos fall inside its boundary. This spec
adds three things they don't cover:

1. **Days.** Each place counts the days you spent in it, so you can ask "How many days have I
   spent in Universal Studios Florida?"
2. **Universal.** Universal parks join Disney parks as the two launch collections.
3. **One source for the numbers.** The park days come from one function, so every screen that
   shows them gives the same answer.

Think of a country on the life map as a big room. A park is a smaller room inside a city, and
the same rule for counting days works for both.

```text
Added trips ─▶ Photos with a position ─▶ Park boundaries ─▶ Park days ─▶ Collections: stamps
```

The collection screens lead with trips, per
[#110](https://github.com/bradreimer/fibotrail/issues/110). Since
[#151](https://github.com/bradreimer/fibotrail/issues/151), they are part of the Insights tab, under
the travel totals. See [insights.md](insights.md).

The Stats screen once had a Theme parks view of these days. It was removed in
[#111](https://github.com/bradreimer/fibotrail/issues/111). This spec covers the data files, the
counting rule, the shared numbers, and the collection screens of the Insights tab.

## User story

As a person who visits Disney and Universal theme parks often, I want Fibotrail to record which
park I was in on each day of a trip, so that I can answer questions such as "How many days have I
spent in Universal Studios Florida?"

## Goals

- The Collections list leads with trips: "How many separate trips have I taken to Disney parks?"
  Resorts and parks answer the same question one tap deeper.
- Each park shows its days and its trips as numbers, not only as a stamp.
- A park day uses the same rule as a day in a country, so "6 days in Universal Studios Florida"
  reads like "19 days in Sweden."
- Photos at CityWalk, a hotel, or a parking lot don't count as a park day.
- Adding a park or a whole collection needs a data file and no code, per ADR-003.
- Every number comes from added trips, and nothing from a suggestion.
- Nothing leaves your device: boundaries are bundled files, and no request goes to another host.

## Non-goals

- Anything inside a park: rides, lines, shows, restaurants.
- Water parks, resort hotels, and shopping districts, such as Disney Springs and CityWalk.
- Other brands. The file format allows them later, and each needs only a data file.
- **Not a visit**, the override that removes a park a photo placed. Only **Mark as visited** is in
  this spec, per [#119](https://github.com/bradreimer/fibotrail/issues/119).
- Adding or removing a park day by hand.
- A park chart. The MCP server answers questions about park days.
- National parks, countries, and regions as collections. They follow the same format later.
- Filters by year.

## Requirements

### Collection files

- Each collection is one YAML file in `collections/`. Each place has a GeoJSON polygon in
  `collections/boundaries/`. The format is the one in the design document, and it is a public
  contract: a change to it needs a change to this spec.
- The launch collections are `disney-parks` and `universal-parks`.
- A collection has an `id`, a `name`, a `description`, a `rule`, and a list of places.
- A place has an `id`, a `name`, a `group` (the resort, such as "Walt Disney World Resort"), a
  `status` of `open` or `planned`, and a `boundary` file.
- The `rule` has `min_photos` (3) and `buffer_m` (50). A place may set its own `buffer_m`, so a
  park beside a shopping district can use a smaller buffer.
- A place with the status `planned` shows as a gray "Coming" stamp. It isn't counted in the total
  until it opens.
- The server checks every file when it starts. A missing boundary, a polygon that isn't closed, or
  a duplicate `id` is an error that names the file. The app refuses to start with a bad file, and
  never skips one silently.

### The launch places

| Collection | Resort | Parks |
| --- | --- | --- |
| `disney-parks` | Walt Disney World Resort, Orlando | Magic Kingdom, EPCOT, Disney's Hollywood Studios, Disney's Animal Kingdom |
| `disney-parks` | Disneyland Resort, Anaheim | Disneyland Park, Disney California Adventure |
| `disney-parks` | Tokyo Disney Resort | Tokyo Disneyland, Tokyo DisneySea |
| `disney-parks` | Disneyland Paris | Disneyland Park, Disney Adventure World (formerly Walt Disney Studios Park) |
| `disney-parks` | Hong Kong Disneyland Resort | Hong Kong Disneyland |
| `disney-parks` | Shanghai Disney Resort | Shanghai Disneyland |
| `universal-parks` | Universal Orlando Resort | Universal Studios Florida, Universal's Islands of Adventure, Universal Epic Universe |
| `universal-parks` | Universal Studios Hollywood | Universal Studios Hollywood |
| `universal-parks` | Universal Studios Japan, Osaka | Universal Studios Japan |
| `universal-parks` | Universal Studios Singapore | Universal Studios Singapore |
| `universal-parks` | Universal Beijing Resort | Universal Studios Beijing |

That is 12 Disney parks and 7 Universal parks. The Abu Dhabi Disney park is `planned` until it
opens.

### Boundaries

- Boundaries start from OpenStreetMap `tourism=theme_park` outlines, are simplified, and are
  checked by hand against satellite imagery. The park's own gates and fence are the edge, not the
  resort.
- Neighboring parks have separate polygons that don't overlap. Universal Studios Florida,
  Islands of Adventure, and Epic Universe are three places. CityWalk is in none of them.
- The app shows the OpenStreetMap attribution wherever it shows a collection, per ADR-003.
- The size of the bundled boundary files is recorded in this spec once they exist. See open
  question 4.

### What counts

- Only trips with the status "added" count. Suggested, later, and ignored trips don't.
- Only away photos with a position count. A photo on the way
  ([on the way](on-the-way.md)) doesn't count, the same as for a country on the
  [life map](life-map.md#what-counts-as-visited).
- A photo is **in** a park when its position is inside the park's polygon, allowing `buffer_m`.
  When a photo falls in two buffers, it belongs to the park whose polygon is nearer.
- A park is **visited on a trip** when at least `min_photos` photos of that trip are in it. A park
  is **visited** when any added trip visits it.
- A **park day** is a distinct local day, across all added trips, that has at least one photo in a
  visited park. Days use the local time of each photo, per [trip detection](trip-detection.md#rules).
  A night photo abroad lands on the right day.
- A day with photos in two parks counts for both.
- A day with no photo in any park counts for no park, even when the trip's hotel is on the resort.
- **Trips in a park** is the number of added trips that visit it.
- The first visit is the earliest local day with a photo in the park.
- A park always shows both numbers, such as "6 days · 2 trips".
- **Collection trips** is the number of distinct added trips that visit at least one open park in
  the collection. **Resort trips** is the same for the parks of one resort, the `group` of the
  place. A trip that visits two parks counts once. A trip that visits two resorts counts once in
  the collection and once in each resort, so the resort numbers can add up to more than the
  collection's. Every level reads the visits that the park already has, so a park's trips, its
  resort's trips, and the collection's trips can't disagree.

The first rule keeps one stray photo from creating a park. The second keeps a quiet day in a real
visit, such as a day your phone died after one photo. See open question 1.

### Marking a park by hand

Photos from before cameras recorded a position have no location, so no photo is in a park, and the
counting rule above can't see the trip. You mark the parks yourself.

- The trip page has a **Parks** section. It lists the parks the trip visited, each with its source:
  "From photos" when at least `min_photos` photos of the trip are in the park, or "Marked by you".
  **Mark parks visited** opens a sheet with one switch for each open park, grouped by collection
  and resort. A park that photos place stays on and can't be turned off.
- A park you mark is saved as a decision on the trip, in the `decision` table, so it survives every
  rescan, per
  [ADR-002](../adr/ADR-002-decisions-are-stored-separately-from-detected-data.md). It follows the
  trip when detection changes the trip's fingerprint.
- A marked park counts the trip: it adds the trip to the park, its resort, and the collection, and
  it makes the park visited. It adds **no days**, because with no position no day is known. A park
  that photos also place counts once, with its photo days.
- The first visit of a park that only you marked is the first day of the trip.
- The park sheet shows "Marked by you" for such a trip, where it shows "3 days here" for others.
- Only an open park of a collection can be marked. A planned or unknown id is refused.
- It counts only for added trips, like everything else here.

### Shared numbers

- The park days are computed on the server, on each request, from the stored position and local
  day of each trip photo and from the boundaries. No park or day table is stored, per
  [ADR-013](../adr/ADR-013-park-days-are-derived-from-stored-photo-positions.md), which follows
  [ADR-011](../adr/ADR-011-visits-are-derived-from-added-trips-on-the-server.md).
- The lookup and the day count are pure functions in the detection project, so they test with plain
  data and no running Immich.

### Insights screens

- The collections are on the **Insights** screen, under the travel totals, per
  [insights.md](insights.md). Their pages are at `/insights/{id}`,
  `/insights/{id}/resorts/{resort}`, and `/insights/{id}/stamps`.
- Collections drill down in three levels, then a stamp book:
  1. **List.** On the Insights screen, one row per collection: the name and its trips, such as "9 trips". The row has no
     ring and no days. A collection with no trips reads "0 trips · Not visited yet". Each row
     opens and closes in place, like a section under the map on the
     [life map](life-map.md), per [#116](https://github.com/bradreimer/fibotrail/issues/116).
     Rows start closed, and several can be open at once. An open row loads the collection the
     first time it opens, and shows "7 / 12 parks · 34 days · 9 trips", one card for each
     resort, the shared-trip note, and the **Stamp book** button. A failed load shows the error
     and **Try again**.
  2. **Collection.** The trips again, then one card for each resort, most trips first and then by
     name, such as "Walt Disney World Resort, 5 trips". A note says "A trip to two resorts counts
     once for each." A **Stamp book** button opens the stamp book.
  3. **Resort.** Every added trip that visits the resort, newest first, each opening its trip
     page. The header shows the resort's trips, such as "5 trips". A trip that visits two parks of
     the resort appears once, so the list length equals the resort's trip count, per
     [#127](https://github.com/bradreimer/fibotrail/issues/127).
- A **resort card** (levels 1 and 2) shows the resort name and its trips, such as "5 trips". A
  resort with no trips reads "0 trips · Not visited yet". Tapping the card opens the resort's trip
  list. The card has no park cards under it. A park's days and first visit are in the stamp book.
- A **trip row** in the resort list shows the trip and one **badge** for each park of the resort
  that the trip visited, such as "Magic Kingdom" and "EPCOT". A park the trip didn't visit has no
  badge. A badge is text with a check shape, never color alone, and wraps on a narrow phone. A
  park marked by hand has a badge too. The badges are a list labeled "Parks on" and the trip's
  name, so a screen reader reads the trip, then its parks.
- The **stamp book** holds what the list used to show: the progress ring with the visited count
  over the open total, such as "7 / 12", the total days, such as "34 days across 8 parks", and the
  grid of stamps grouped by resort. A visited stamp is gold, with the name, "6 days", and "2 trips."
  A stamp that isn't visited has a gray outline and "Not visited yet."
- The stamp states differ by text and shape, never by color alone.
- Tapping a visited stamp opens a sheet with the park's days, trips, and first visit. Each trip
  links to its trip page and shows how many of that trip's days were in the park.
- A screen reader reads each stamp once, as one label: the name, the days, and the trips, such as
  "Universal Studios Florida, 6 days, 2 trips." The grid is also the text alternative to the ring.
- The four screen states of the app shell apply: loading, empty, error, and offline. With no added
  trips, a collection page says "Add a trip to start your stamp book."

### API

- Every route needs a session.
- `GET /api/parks` returns every open park with its id, name, resort, and collection.
- `PUT /api/trips/{id}/parks` takes `{ "parks": ["magic-kingdom"] }`, replaces the trip's marked
  parks, and answers with the trip page. An empty list clears them. `GET /api/trips/{id}` has a
  `parks` list with each park's `source`, `photos` or `you`.
- `GET /api/collections` returns each collection with its name, visited count, open total, total
  days, and trips.
- `GET /api/collections/{id}` returns the summary, each resort with its visited count, open total,
  days, trips, and trip IDs, and each place with its name, group, status, days, trips, first visit, and trip
  IDs.
- The OpenAPI document and the TypeScript types are updated, per
  [web-app-development.md](../knowledge/web-app-development.md).

### Accessibility and iOS

- Stamps and the sheet have a tap target of at least 44 by 44 points and work with a keyboard and
  a screen reader.
- The screens meet the table in ADR-007, and pass its run-through on an iPhone.

## Acceptance criteria

- An added trip with photos in Universal Studios Florida on 3 different local days, at least 3
  photos in all, shows **3 days** and **1 trip** for that park, on the collection detail.
- Two added trips with 3 and 2 park days in Universal Studios Florida show **5 days** and
  **2 trips**.
- Photos in Universal Studios Florida and Islands of Adventure on one day add 1 day to each park.
- A trip with only 2 photos in Magic Kingdom, and none elsewhere in it, doesn't visit the park, so
  it shows no stamp and no days.
- Photos only at CityWalk, or at a hotel beside a park, add no park day.
- A suggested, later, or ignored trip adds no days, and no stamp.
- A photo on the way, such as one from a train inside a resort, adds no day.
- A photo taken at 11:30 pm local time counts for that day, not the next.
- An added trip with no photo positions, with Magic Kingdom marked, shows **1 trip** and **0 days**
  for that park, **1 trip** for its resort and the collection, and "Marked by you" in the park
  sheet. Clearing the mark removes it.
- A park that photos place can't be turned off on the trip page, and marking it too adds no second
  trip.
- A marked park survives a rescan.
- A new collection file with a valid boundary shows up after a restart, with no code change. A
  file with a missing boundary stops the app with a message that names the file.
- The collection list and detail give the same days for every park.
- The browser's network log shows no request to a host other than Fibotrail.
- The collection screens pass the ADR-007 run-through on an iPhone, and the accessibility
  checklist.
- A unit test covers the park lookup (inside, inside the buffer, outside, and two neighbors) and
  the day count (several days, two parks in a day, a trip that crosses midnight, and one stray
  photo).
- A test loads every collection file and checks that each park has a valid, closed boundary and
  that no two parks in a collection overlap.
- `./scripts/check.sh` passes.

- A trip with photos in Magic Kingdom and EPCOT shows **1 trip** for the collection, **1 trip** for
  Walt Disney World Resort, and **1 trip** for each park.
- A trip to Walt Disney World and Disneyland Resort shows **1 trip** for the collection and **1
  trip** for each resort.
- Three added trips to Magic Kingdom show **3 trips** for the collection, the resort, and the park.
- A trip with only 2 photos in a park adds no trip at any level.
- A resort never shows more trips than its collection. A park's trips equal its trips in the Theme
  parks view.
- The list row shows trips and no days. The ring, the total days, and the stamps are in the stamp
  book, one tap from the collection.
- A resort card shows its trips. Tapping it lists every added trip at the resort once, and the
  list length equals the card's count. Each trip row has a badge for each park of the resort that
  the trip visited.
- A collection row on the list starts closed. Tapping it opens it in place, with its totals, its
  resorts, and the **Stamp book** button. Tapping again closes it.

## Open questions

1. **Is one photo enough for a day?** The proposal is yes, once the park is visited on the trip
   (3 photos). The risk is a photo taken from the road at a park's edge. Check against real trips,
   and consider a minimum of 2 photos for a day.
2. **Does a photo on the way count for a day?** This spec says no, to match countries. A photo
   hidden from the map is still a photo taken inside the park. Decide after a real trip.
3. **Is 50 m the right buffer?** GPS drifts, and Universal Studios Florida's gate sits close to
   CityWalk. Measure on real photos, then set a smaller `buffer_m` for the places that need it.
4. **How large are the boundary files?** The app builds a spatial index of every boundary at
   startup, per ADR-003. Record the size and the startup time here, and check them against the
   budget in [quality gates](quality-gates.md).
5. **How are park names kept current?** Parks get renamed. A place's `id` stays the same, and the
   name is changed in the data file. The design document should say who checks the list, and
   how often.
6. **What happens to a trip that is only partly detected?** A photo with no position adds no
   park day, so a day inside a park with no GPS fix doesn't count. The collection sheet could
   say so, such as "Days count only photos with a location."
