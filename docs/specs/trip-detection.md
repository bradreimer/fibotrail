# Trip detection

Tracking issue: [#8](https://github.com/bradreimer/fibotrail/issues/8).

## Purpose

This spec defines how Fibotrail turns scanned photos into suggested trips. Detection is a
pipeline of plain rules, so you can predict what it does and tune it. It also defines how your
decisions outlive every rescan.

It covers stories U3 and U6 in the [roadmap](../roadmap.md#mvp-version-01), MVP step 3a.

## User story

As a person with years of photos in Immich, I want Fibotrail to find my trips once I confirm where
home is, so that I review suggestions and don't build my travel history by hand.

## Goals

- You accept most High-confidence suggestions without edits.
- The same photos always produce the same suggestions.
- A decision you make once is never asked again, per
  [ADR-002](../adr/ADR-002-decisions-are-stored-separately-from-detected-data.md).
- The rules are tested with plain data and no running Immich.

## Non-goals

- The screens that show suggestions. See [trip-inbox.md](trip-inbox.md).
- A settings screen for the detection rules. The MVP uses the defaults.
- Day trips, and merging, splitting, or changing the dates of a trip.
- Places, visits, and collections.
- Real GPS tracks. A route connects the places where photos were taken.

## Requirements

### Structure

- The rules are pure functions in their own C# project, with no dependency on ASP.NET Core or on
  the database. They take photos and settings, and return trips.
- Distance and grouping use NetTopologySuite.
- Detection runs when a scan completes and when the home location changes. It doesn't run until a
  home location is confirmed.

### Home location

- After the first scan, Fibotrail suggests a home location: the 1 km area with photos on the most
  distinct days in the last 12 months of the library.
- A setup step shows the suggestion on a map and by name, with **Use this location** and
  **Choose another**. You choose another by searching the city names found in your photos, or by
  moving a pin.
- The confirmed location is saved in the `setting` table. You can change it in Settings.
- When the library has no photo with a position, the step explains that Fibotrail can't find
  trips without locations.

### Settings

| Setting | Default | Meaning |
| --- | --- | --- |
| Home radius | 50 km | Photos closer than this count as home |
| Gap limit | 72 hours | A pause this long between away photos ends a trip |
| Minimum photos | 20 | Fewer photos than this isn't a trip |
| Minimum days | 2 calendar days | Shorter outings aren't trips |
| Stop radius | 1 km | Photos this close together form one stop |

### Rules

1. **Collect.** Use every photo that has a capture time.
2. **Mark away photos.** A photo with a position is away when it's farther from home than the
   home radius.
3. **Split into trips.** Sort away photos by time. Start a new trip when a photo appears inside
   the home radius, or when no away photo appears for the gap limit.
4. **Filter.** Keep a trip that has at least the minimum photos and the minimum days.
5. **Attach photos without a position.** A photo with no position joins a trip when its capture
   time falls between the trip's first and last photo.
6. **Find stops.** Group a trip's photos into stops with the stop radius, and order the stops by
   time.
7. **Draw the route.** Connect the stops in order with straight lines.
8. **Measure.** Distance is the sum of the lines between consecutive stops. Farthest from home is
   the greatest distance from home to a stop.
9. **Name.** The suggested name is the city with the most photos, then the month and year of the
   first day, such as "Orlando, March 2027". When no city is known, use the state, then the
   country.
10. **Score.** Label each suggestion High, Medium, or Low confidence from its photo count, days,
    and distance from home. See open question 1.
11. **Reconcile.** Match each result to the stored trips and decisions.

Days are counted in the local time of each photo, so a photo taken at night abroad lands on the
right day.

### Reconciliation and decisions

- A trip's fingerprint is a hash of its start date and its main place. The main place is the
  city, or the state or country when no city is known, with the most photos.
- A detected trip with the fingerprint of a stored trip is the same trip. Its photos, stops, and
  route are updated. Its status, name, and cover aren't.
- New photos that continue an added trip extend it, without a new suggestion.
- A detected trip with the fingerprint of an "ignored" decision stays ignored, however many
  photos it gains.
- A detected trip that matches nothing becomes a new suggestion.
- Detected data can be deleted and rebuilt from the `photo` table at any time. The `decision`
  table is never rebuilt.
- The tables are `trip`, `trip_photo`, `decision`, and `setting`. Stops and the route are stored
  with the trip.

### Replay command

- A command in the container reads a list of known trips from a file in `/data`. Each entry has a
  name, a start date, and an end date.
- It reports each known trip as matched or missed, and lists detected trips that match no known
  trip.
- A known trip is matched when a detected trip starts and ends within one day of it.
- The list holds private data and is never committed.

## Acceptance criteria

- On first run, Fibotrail suggests a home location and runs no detection until you confirm one.
- With the defaults, 25 photos over 3 days, 200 km from home, become one suggestion.
- 19 photos over 3 days, 200 km from home, become no suggestion.
- Two groups of away photos 73 hours apart become two trips.
- A photo with no position, taken during a trip, belongs to that trip.
- A photo taken at 23:30 local time abroad is counted on that local day.
- Running detection twice on the same photos produces the same trips with the same fingerprints.
- After **Not a trip**, a rescan that adds photos to the same trip creates no suggestion.
- After a rename, a rescan keeps the new name.
- The replay command matches at least 8 of every 10 known trips on the maintainer's library. This
  is the gate for MVP step 3b.

## Open questions

1. What are the thresholds for High, Medium, and Low confidence? The replay test settles it. The
   starting point is High for at least 50 photos, 3 days, and 150 km from home, Low for a trip
   that passes the minimums by less than 25 percent, and Medium for the rest.
2. Is a fingerprint of start date and main place stable enough? If an early photo arrives late,
   the start date moves and the fingerprint changes. The fallback is to also match a stored trip
   whose dates overlap the detected trip by more than half. The replay test and the rescan tests
   settle it.
3. Does the home suggestion work for a library with two homes, such as after a move? The MVP uses
   one home location. A dated list of homes is a later feature if the replay test shows a need.
