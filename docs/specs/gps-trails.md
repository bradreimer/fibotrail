# GPS trails

Tracking issue: [#193](https://github.com/bradreimer/fibotrail/issues/193).

## Purpose

This spec defines how a GPS trail from a logger is added to a trip and drawn on its map. The trip
page today draws a photo route: straight lines between the stops where photos were taken. A
logger, such as the Qstarz BL-1000ST, records a point every few seconds, so its trail shows where
you actually went.

This spec covers phase 1 of the GPS trail design: upload, validation, storage, and display. Photo
alignment, a watched folder, and trail analytics are later work items.

## User story

As a person who carries a GPS logger on vacation, I want to add the logger's GPX trail to a trip
Fibotrail already found, so that the trip map shows where I actually went, not only the straight
lines between photo stops.

## Goals

- Add one or more GPX files to an existing trip.
- Draw each trail on the trip's route map, together with the photo stops.
- Keep the original file untouched as the source of truth.
- Work on a phone and an iPad, not only on a desktop.
- Keep trail coordinates inside Fibotrail.

## Non-goals

- Reading the logger's raw files. You convert them to GPX with GPSBabel first.
- Matching photos to the trail by time, a time-offset control, and flagging photos outside the
  trail's time range. That is phase 2.
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

### Privacy

- Trail coordinates are never sent to an outside service.
- The map keeps the bundled base map of
  [ADR-009](../adr/ADR-009-a-bundled-natural-earth-base-map.md).
- The MCP server returns no trail data, per
  [ADR-018](../adr/ADR-018-an-mcp-server-answers-questions-with-coarse-read-only-data.md).

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

## Open questions

1. The exact GPSBabel command and options for the BL-1000 format aren't confirmed yet. Verify with
   real files once the device arrives.
2. The 5-minute and 1 km gap thresholds are first guesses. Tune them with real files.
3. Should a trail upload also be possible from the command line, like other trip edits? Not in this
   phase.
