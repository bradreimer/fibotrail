# GPS trails

Tracking issue: [#193](https://github.com/bradreimer/fibotrail/issues/193).

## Purpose

This spec defines how a GPS trail from a logger is added to a trip and drawn on its map. The trip
page today draws a photo route: straight lines between the stops where photos were taken. A
logger, such as the Qstarz BL-1000ST, records a point every few seconds, so its trail shows where
you actually went.

This spec covers phase 1 of the GPS trail design (upload, validation, storage, and display) and
phase 2 (photo alignment: each photo placed on the trail by the time it was taken). A watched
folder and trail analytics are later work items.

## User story

As a person who carries a GPS logger on vacation, I want to add the logger's GPX trail to a trip
Fibotrail already found, so that the trip map shows where I actually went, not only the straight
lines between photo stops. And I want my photos placed along the trail by the time I took
them, so that I can see where each photo was taken, including photos my camera didn't tag with a
location.

## Goals

- Add one or more GPX files to an existing trip.
- Draw each trail on the trip's route map, together with the photo stops.
- Keep the original file untouched as the source of truth.
- Work on a phone and an iPad, not only on a desktop.
- Keep trail coordinates inside Fibotrail.
- Show where each photo was taken on the trail, using only the photo's time.

## Non-goals

- Reading the logger's raw files. You convert them to GPX with GPSBabel first.
- Using a position estimated from a trail anywhere but the trip page and its viewer: not in
  stops, distance, parks, the life map, or detection. See
  [ADR-025](../adr/ADR-025-a-position-from-a-trail-is-shown-and-never-stored.md).
- An offset for each photo or each camera. A trail has one offset.
- A watched folder, a GPSBabel helper, and per-day playback. That is phase 3.
- Time per area, distance per day, and heat maps. That is phase 4.
- Live tracking, real-time sync, and editing track points.
- Writing an estimated location back to Immich. That would break
  [ADR-001](../adr/ADR-001-immich-is-read-only.md) and needs its own ADR first.

## Requirements

### Workflow

1. You carry a GPS logger during a trip.
2. At home, you copy the log files from the logger's microSD card to a computer.
3. You convert them to GPX with GPSBabel and its Qstarz BL-1000 input format.
4. In Fibotrail, you open the trip and choose **Add GPS trail**.
5. You pick the GPX file. On iOS, the picker opens the Files app.
6. Fibotrail checks the file and shows a preview: the dates covered, the distance, and the number
   of points.
7. When the trail's dates don't overlap the trip's dates, the preview shows a warning. You can
   still add it.
8. You choose **Add trail**. The trail appears on the trip page.

### Parsing

- Parsing is a pure function in its own C# project, `Fibotrail.Trails`, so it can be tested with
  plain files and no running server.
- It reads GPX 1.0 and 1.1. It reads tracks and track segments, ignores waypoints, and reads routes
  only when the file has no track.
- Each point has a UTC time, a latitude, a longitude, and an optional elevation. A point with no
  time, or at 0, 0, is skipped.
- A file that isn't well-formed GPX, or that has no usable point, is refused.

### Summary

| Value | Rule |
| --- | --- |
| Start and end | The first and last point times, in UTC |
| Distance | The sum of the great-circle distances between consecutive points in a segment, in kilometers |
| Moving time | The sum of the intervals between consecutive points where the speed is above 1 km/h |
| Points | The number of usable points |

- Dates are shown in the device's time zone. A trip's days are local, and Fibotrail doesn't store
  the trip's time zone, so the server returns UTC times and the browser converts them.
- A trail overlaps its trip when its time span shares a moment with the trip's days, with 14 hours
  allowed either side for the trip's unknown time zone. Without the allowance, a walk on the
  trip's last evening in Tokyo would look like the next day.

### Gaps

- Two consecutive points more than 5 minutes or 1 km apart start a new segment.
- The map draws each segment as its own line, and never draws a line across a gap.
- Distance and moving time don't count the step across a gap.

### Storage

- The uploaded file is kept byte for byte at `/data/trails/<random name>.gpx`. The name you picked
  is stored as text only, without any folder, so it never becomes part of a path.
- A `trail` table holds the trip's decision, the original file name, the start and end, the
  distance, the moving time, the point count, and the simplified line for display.
- A trail belongs to the trip's decision, not to the trip row, so it survives a rescan and follows
  the trip when its fingerprint moves
  ([ADR-024](../adr/ADR-024-a-gps-trail-is-a-kept-file-that-belongs-to-a-trips-decision.md),
  [ADR-010](../adr/ADR-010-stops-and-routes-are-stored-with-the-trip.md)). Adding a trail to a
  trip with no decision creates one with no choice in it, as a rename does.
- Removing a trail deletes its row and its file.

### Display

- The trail is a 3 px line under the photo route and the pins, in Deep Ocean in light mode and
  Golden Gold in dark mode, so it differs from the Trail Teal photo route.
- For display, each trail is simplified with Douglas–Peucker to about 5 m, and the tolerance is
  doubled until the trail has no more than 5,000 points. A trail of more than 20,000 points is
  first thinned to evenly spaced points, because Douglas–Peucker is slow on a long, jagged track.
  Storage keeps every point.
- The map opens fitted to the photo route and the trails together.
- A caption under the map says "GPS trail: the path your logger recorded."
- A **GPS trails** section under the stat tiles lists each trail's file name, dates, and
  distance, with **Remove**, and has the **Add GPS trail** button. With no trail, it says "No GPS
  trail yet."

### API

- A file is sent as the raw request body, with its name in the `name` query parameter, and the
  anti-forgery token in the `X-CSRF-Token` header like every change.
- `POST /api/trips/{id}/trails/preview` checks the file and returns the summary and whether it
  overlaps the trip, without saving.
- `POST /api/trips/{id}/trails` checks and saves the file, and returns the trail.
- `GET /api/trips/{id}/trails` returns the trip's trails with their simplified lines.
- `DELETE /api/trips/{id}/trails/{trailId}` removes a trail.
- An upload over 50 MB is refused with 413. A file that isn't usable GPX is refused with 400 and a
  plain message.

### Photo alignment

Tracking issue: [#201](https://github.com/bradreimer/fibotrail/issues/201).

```
photo local time ──(− offset)──▶ UTC moment ──▶ the trail's two points around it ──▶ position
                                                 │
                                                 └── in a gap, or before or after the trail ──▶ outside
```

- **The offset.** A trail's points are in UTC, and Fibotrail keeps only each photo's local
  wall-clock time. Immich has no time zone for 61% of photos
  ([immich-api-findings.md](../knowledge/immich-api-findings.md#1-local-capture-time-and-time-zone)),
  and camera clocks drift. So each trail has one offset, in whole minutes: photo local time = trail
  UTC time + offset. It covers both the time zone and the camera clock.
- **Aligning a photo.** The photo's local time minus the offset is a UTC moment. When the moment
  lies inside a segment of the trail, the photo's trail position is interpolated between the two
  points either side, in proportion to the time. When it falls in a gap between segments, or
  before or after the trail, the photo is **outside** the trail and has no trail position.
- **Estimating the offset.** Fibotrail tries every offset from −14 hours to +14 hours, a minute
  apart. For each, it aligns the photos that have a position of their own, and measures how far
  each is from its trail position. The best offset has the most photos within 200 m of their trail
  position. A tie goes to the smaller median distance of the photos that land on the trail, then
  to the offset nearer 0. The count comes first because a wrong offset can put 3 photos close by
  chance. There is no estimate when fewer than 3 photos land within 200 m, or when the best median
  is over 500 m. Then the offset is 0, marked "not estimated". At most 500 placed photos are used,
  evenly spaced in time.
- **Your offset wins.** **Adjust time** saves an offset you choose, and it is never replaced by an
  estimate. **Use estimate** removes it. The offset you chose is stored on the trail row. The
  estimate isn't stored. It's worked out when the trip page asks, so it follows the trip's photos
  after a rescan.
- **On the map.** Each photo with a trail position is a small dot on the trail, over the trail line
  and under the stop pins, in Sunset Coral. A photo with no position of its own is a hollow dot,
  and its label says "Estimated from the GPS trail". Tapping a dot opens the photo viewer on that
  photo. A photo with a position of its own still counts at that position everywhere else.
- **In the viewer.** A photo with no position of its own, but a trail position, shows the trail
  position on the viewer's small map, with the line "Estimated from the GPS trail".
- **In the trail list.** Each trail shows its offset, such as "Camera time: trail + 5 h 00 min
  (estimated)", "(set by you)", or "(not estimated)". A line under the list gives the photos that
  fall outside every trail, such as "12 photos are outside the trails' times." Nothing is shown
  when every photo is on a trail.
- **The Adjust time sheet.** It shows the offset, with buttons for −1 h, −1 min, +1 min, and +1 h,
  and a live line such as "38 of 52 photos land on this trail." Each change asks the server again,
  without saving. **Save** stores the offset, **Use estimate** removes yours, and **Cancel** keeps
  what was there. When there's no estimate, the sheet says "Fibotrail couldn't estimate the time.
  Set it so the photos land where you took them."

#### Alignment API

- `GET /api/trips/{id}/trails/alignment` returns, for each trail of the trip, its offset, where the
  offset came from (`you`, `estimated`, or `none`), and each trip photo in time order with its
  trail position or none. It also returns the IDs of the photos outside every trail.
- `?trailId=<id>&offsetMinutes=<n>` returns the same with a trial offset for one trail, without
  saving. The Adjust time sheet uses it.
- `PUT /api/trips/{id}/trails/{trailId}/offset` with `{ "minutes": 300 }` saves your offset, and
  `{ "minutes": null }` removes it. An offset outside −14 h to +14 h is refused with 400.
- The trail's full points are read from its kept file. The server keeps the last 8 files it read in
  memory, so the trip page doesn't read a file again for every request.

### Privacy

- Trail coordinates are never sent to an outside service.
- The map keeps the bundled base map of
  [ADR-009](../adr/ADR-009-a-bundled-natural-earth-base-map.md).
- The MCP server returns no trail data and no position estimated from a trail, per
  [ADR-018](../adr/ADR-018-an-mcp-server-answers-questions-with-coarse-read-only-data.md) and
  [ADR-023](../adr/ADR-023-the-mcp-server-may-return-a-trips-main-city.md).
- A position estimated from a trail is never written to Immich and never stored.

### States, accessibility, and iOS

- The sheet shows a progress state while the file uploads and a plain message when it's refused.
- The sheet and the trail list work with a keyboard and a screen reader.
- The sheet meets the table in
  [ADR-007](../adr/ADR-007-native-ios-feel-through-a-progressive-web-app.md).

## Acceptance criteria

- Uploading a valid GPX file shows a preview with its dates, distance, and point count, and **Add
  trail** draws it on the trip map.
- A file that isn't GPX, has no usable point, or is over 50 MB is refused with a plain message,
  and nothing is stored.
- The stored file is byte-for-byte identical to the uploaded one.
- A trail whose dates don't overlap the trip's dates shows a warning before it's saved, and can
  still be added.
- A trip can have more than one trail, and each one can be removed.
- A trail with a 10-minute gap is drawn as two separate lines.
- A trail of 100,000 points sends no more than 5,000 points to the browser.
- Trails survive a rescan and a container restart.
- With the default base map, adding and viewing a trail makes no request to a host other than
  Fibotrail.
- The sheet passes the ADR-007 run-through on an iPhone.
- For a test trip whose photos are 5 hours and 7 minutes ahead of the trail's UTC times, the
  estimate is +5 h 07 min, and the placed photos' median distance to the trail is under 100 m.
- A photo with no position, taken between two trail points, is a hollow dot at the interpolated
  point, labeled "Estimated from the GPS trail".
- A photo taken in a 10-minute gap of the trail, or before it starts, has no trail position and
  counts as outside.
- With fewer than 3 placed photos, the offset is 0 and marked "not estimated".
- Changing the offset in the sheet moves the count without saving. A saved offset survives a
  rescan and a restart, and **Use estimate** goes back to the estimate.
- A photo's stop, the trip's distance, and its parks don't change when a trail is added or its
  offset changes.
- The Adjust time sheet passes the ADR-007 run-through on an iPhone.

## Open questions

1. The exact GPSBabel command and options for the BL-1000 format aren't confirmed yet. Verify with
   real files once the device arrives.
2. The 5-minute and 1 km gap thresholds are first guesses. Tune them with real files.
3. Should a trail upload also be possible from the command line, like other trip edits? Not in this
   phase.
4. Is a 500 m median the right limit for a good estimate? It's a first guess. Check it with real
   files and a phone's own GPS photos.
5. Should one trail have several offsets, for a trip across a daylight saving change or with two
   cameras? Not in this phase: the estimate picks the offset that fits most photos.
