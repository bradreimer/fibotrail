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
- The project is `Fibotrail.Detection`. Grouping uses a NetTopologySuite spatial index, so nearby
  photos are found without comparing every pair. Distance is the great-circle distance by the
  haversine formula, because NetTopologySuite measures on a flat plane.
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
- The step shows in the Inbox until a home location is confirmed. It doesn't show before the
  first scan.
- "The last 12 months" counts back from the newest photo with a position. A tie between two areas
  goes to the one with more photos.
- The name of the home location is the nearest city in your photos within 50 km. A pin farther
  from any city is named by its position, such as "33.87° S, 151.21° E".
- The map is the bundled base map of
  [ADR-009](../adr/ADR-009-a-bundled-natural-earth-base-map.md).
- Saving a home location runs detection again and says how many trips it found.

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
4. **Filter.** Keep a trip that has at least the minimum photos and the minimum days. Only away
   photos count toward the minimum photos. Days run from the first day to the last, counting
   both.
5. **Attach photos without a position.** A photo with no position joins a trip when its capture
   time falls between the trip's first and last photo.
6. **Find stops.** Group a trip's photos into stops with the stop radius, and order the stops by
   time. Photos are taken oldest first. Each joins the first stop whose first photo is within the
   stop radius, or starts a stop. A place you return to is one stop.
7. **Draw the route.** Connect the stops in order with straight lines.
8. **Measure.** Distance is the sum of the lines between consecutive stops. Farthest from home is
   the greatest distance from home to a stop.
9. **Name.** The suggested name is the main place, then the month and year of the first day, such
   as "Orlando, March 2027". The main place is the city where the most days were spent, with the
   state and then the country as fallbacks. The options for other names, and the rule for the main
   place, are in [trip names](trip-names.md).
10. **Score.** Label each suggestion High, Medium, or Low confidence from its photo count, days,
    and distance from home. High needs at least 50 photos, 3 days, and 150 km from home. Low is
    a trip with fewer than 25 away photos, which clears the minimum photos by less than 25
    percent. Everything else is Medium. See open question 1.
11. **Reconcile.** Match each result to the stored trips and decisions.

Days are counted in the local time of each photo, so a photo taken at night abroad lands on the
right day.

### Reconciliation and decisions

- A trip's fingerprint is a hash of its start date and its main place. The main place is the
  city, or the state or country when no city is known, with the most days, per
  [trip names](trip-names.md).
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
- When no fingerprint matches, a detected trip matches the stored trip or decision whose dates
  cover more than half of its days. The decision then takes the trip's new fingerprint. See
  [ADR-010](../adr/ADR-010-stops-and-routes-are-stored-with-the-trip.md).
- A stored trip that detection no longer finds is removed. Its decision stays, and applies again
  if the trip comes back.
- A cover you chose stays for as long as its photo is part of the trip.

### Review gate

Intention: judge detection by what you would accept, not by a list of trips you first have to
remember. This is the gate for MVP step 3a. It replaces the replay test as the gate, per the
decision of 2026-11-16 in [#24](https://github.com/bradreimer/fibotrail/issues/24).

Method: scan the real library and confirm the home location. Then:

1. **Accuracy.** Take the first 10 High-confidence suggestions in the inbox, newest first, or all
   of them when there are fewer. Count the ones you would accept with **Add trip**. A trip is
   accepted when its dates and photos are right. A different name or cover doesn't count against
   it. A trip that should be merged with another, split in two, or isn't a trip doesn't count.
   The gate is met when at least 8 of 10 are accepted, or at least 80 percent when there are
   fewer than 10.
2. **Misses.** Write down 3 to 5 trips you remember without looking in your library. A trip is
   found when a suggestion's dates cover its days. The gate is met when at most 1 trip is missing.
3. **Record.** Write the counts, and the reason for each trip that failed, in
   [status.md](../status.md). Answer the open questions below with what you saw.

The numbers are a start. If a gate fails, tune one default at a time, scan again, and record each
try, the way the
[first scan and replay checklist](../knowledge/first-scan-and-replay-checklist.md) describes.

### Replay command

The replay command is a tuning tool and no longer a gate. Use it when you have a list of trips and
want a number after a rule change.

- A command in the container reads a list of known trips from a file in `/data`. Each entry has a
  name, a start date, and an end date.
- The command is `docker exec -it fibotrail fibotrail replay`. The file is
  `/data/known-trips.json`, or the path given as an argument. It's a JSON list such as
  `[{ "name": "Orlando", "start": "2027-03-15", "end": "2027-03-24" }]`.
- The command changes nothing in the database. It exits with 0 when at least 8 of every 10 known
  trips are matched, and with 1 otherwise.
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
- The review gate is met on the maintainer's library: at least 8 of the first 10 High-confidence
  suggestions are accepted, and at most 1 of 3 to 5 remembered trips is missing. This is the gate
  for MVP step 3a.
- The replay command still runs and still reports each known trip, but its result doesn't gate a
  release.

## Open questions

1. Are the thresholds for High, Medium, and Low confidence right? Rule 10 has the starting
   point. The Low margin applies to the photo count only: with a minimum of 2 days, a quarter of
   a day can't be measured, and every weekend trip would be Low. The review gate on the real
   library settles it, and it hasn't run yet.
2. Does a photo at home split too many trips? A photo inside the home radius ends a trip, per
   rule 3. If someone else in the household adds photos at home to the same library while you're
   away, your trip is split in two. The review gate shows how often this happens.
3. Should photos with no position count toward the minimum photos? 84.5 percent of the
   maintainer's photos have no position, and rule 4 counts away photos only. A trip taken with a
   camera that records no position can fall under the minimum. The review gate shows whether
   remembered trips are missed for this reason.
4. Does the home suggestion work for a library with two homes, such as after a move? The MVP uses
   one home location. A dated list of homes is a later feature if the review gate shows a need.
