# Trip page

Tracking issue: [#11](https://github.com/bradreimer/fibotrail/issues/11).

## Purpose

This spec defines the page for one trip and the photo viewer that opens from it. The page tells
the story of a trip in four parts: where it was, where you went, how big it was, and what you saw
each day.

It covers stories U8 and U9 in the [roadmap](../roadmap.md#mvp-version-01), MVP step 3c.

## User story

As a person reliving a vacation, I want one page with the route, the key numbers, and my photos
day by day, so that I can follow the trip from start to finish and open any photo in Immich.

## Goals

- Photos come first. The map and the numbers support them.
- Every number on the page can be traced to a rule in [trip-detection.md](trip-detection.md).
- Any photo is one tap from full screen and two taps from Immich.
- The page makes no outside request with the default base map.

## Non-goals

- A small map for each day of the timeline.
- Excluding a photo from a trip, or moving it to another trip.
- Merging, splitting, or changing the dates of a trip.
- The animation in which the route draws itself.
- Collection badges, and tiles for places and countries. They arrive with the `place` table.
- Downloading or editing photos.

## Requirements

### Page structure

The page has four parts, from top to bottom. An API route returns everything they need for one
trip.

1. **Header.** The cover photo, the trip name in Fraunces, the dates, and the number of days.
2. **Route map.** See the next section.
3. **Stat tiles.** Days, photos, stops, distance, and farthest from home.
4. **Photo timeline.** Photos grouped by day.

At 600 px and wider, the map sits beside the timeline in two columns.

### Route map

- The map is drawn with MapLibre GL JS and opens fitted to the route.
- The route is a 4 px Trail Teal line between stops, in order, with a 2 px Paper outline.
- The first stop has a 13 px Golden Gold dot.
- Each stop has a 21 px Sunset Coral pin that shows its photo count.
- Tapping a pin opens a sheet with that stop's photos and its place name.
- A caption says "Photo route: lines connect the places where you took photos."
- The base map is the bundled outline of
  [ADR-009](../adr/ADR-009-a-bundled-natural-earth-base-map.md). A **Full screen** button opens
  the map over the whole screen, inside the safe areas, and **Close map** returns.
- A pin stands above its place, so the gold dot of the first stop shows under its pin.
- A text alternative, **Stops in order**, lists the stops with their place names and photo
  counts. Each entry opens the same sheet as its pin.

### Stat tiles

| Tile | Value |
| --- | --- |
| Days | Calendar days from the first photo to the last, in local time |
| Photos | Photos in the trip, including those with no position |
| Stops | Stops found by detection |
| Distance | Sum of the straight lines between consecutive stops, in kilometers |
| Farthest from home | Greatest distance from home to a stop, in kilometers |

- Each tile shows the number in Fraunces at 34 px with tabular figures, a label in Inter at
  13 px, and an icon. The unit "km" is set at 16 px after the number. The 55 px size of the
  design document doesn't fit a value such as "4,208 km" in a tile on an iPad, so every width uses
  34 px.
- A distance under 10 km has one decimal, such as "4.2 km". Larger ones are whole numbers.
- When a trip has photos with no position, a line under the tiles gives the count, such as "42
  photos have no location."

### Photo timeline

- Photos are grouped by the local day they were taken, in time order.
- Each day has a heading with the day number, the date, and the main place, such as "Day 3 · Wed,
  Mar 17 · Orlando".
- Photos sit in rows that keep each photo's proportions. Every photo has the same height, 110 px,
  and its width follows its proportions. Before its thumbnail loads, a tile is 4:3. A tile never
  grows to fill its row, because that crops the photo, so the right edge of a row can be ragged.
  Rows that also fill their width need the photo sizes, which Fibotrail doesn't store yet.
- Thumbnails load through the thumbnail proxy as they come within one screen of view.
- Each photo has alt text built from its date and place.
- A video shows its still frame with a play mark and its length.

### Photo viewer

- Tapping a photo opens it full screen at the preview size.
- A swipe left or right, or an arrow key, moves to the next or previous photo of the trip.
- A swipe down, the **Close** button, or the Escape key returns to the page at the same position.
- The viewer shows the date, the time, the place name, and a small map with a pin when the photo
  has a position. The time is the local time where the photo was taken.
- **Previous** and **Next** buttons do the same as a swipe, and the viewer shows "2 of 500".
- **Open in Immich** opens the photo in Immich in a new tab. The address is
  `IMMICH_EXTERNAL_URL` followed by `/photos/` and the asset ID. The path was confirmed against
  Immich 3.4.1. See
  [immich-api-findings.md](../knowledge/immich-api-findings.md#7-path-in-the-web-app).
- When `IMMICH_EXTERNAL_URL` is empty, **Open in Immich** isn't shown.
- For a video, the viewer shows the still frame, and the link reads **Play in Immich**. Fibotrail
  doesn't play video itself.

### Edits

- **Rename** changes the trip name. **Change cover** picks a cover from the trip's photos. Both
  are in one sheet, opened by **Rename or change cover** in the header.
- Renaming a suggested trip doesn't add it. Its status stays as it was.
- Both are saved as decisions and survive a rescan, per
  [ADR-002](../adr/ADR-002-decisions-are-stored-separately-from-detected-data.md).

### States, accessibility, and iOS

- Loading shows skeleton shapes for all four parts. An error shows a plain explanation and **Try
  again**. A trip that doesn't exist shows "We can't find that trip." and a link to Trips.
- The page and the viewer work with a keyboard and a screen reader.
- The page meets the table in
  [ADR-007](../adr/ADR-007-native-ios-feel-through-a-progressive-web-app.md).

## Acceptance criteria

- The page shows the header, route map, stat tiles, and photo timeline, in that order.
- A trip with four stops shows four pins, three lines, and a gold dot on the first stop.
- Tapping a pin shows the photos of that stop.
- A photo taken at 23:30 local time on day 2 appears under day 2.
- For a test trip with known stops, the distance tile matches the sum of the straight lines to
  within 1 km.
- Tapping a photo opens the viewer, and a swipe left shows the next photo.
- With `IMMICH_EXTERNAL_URL=https://photos.example.com`, the link for asset `abc` is
  `https://photos.example.com/photos/abc`.
- With `IMMICH_EXTERNAL_URL` empty, the viewer shows no **Open in Immich** link.
- A video shows a play mark in the timeline, and the viewer offers **Play in Immich**.
- A portrait photo in the timeline is a narrower tile than a landscape photo, and both are 110 px
  high.
- After a rename and a rescan, the page shows the new name.
- With the default base map, the browser's network log shows no request to a host other than
  Fibotrail.
- The page passes the ADR-007 run-through on an iPhone.

## Open questions

1. Kilometers or miles? The MVP shows kilometers. A unit setting is a later addition if you want
   one.
