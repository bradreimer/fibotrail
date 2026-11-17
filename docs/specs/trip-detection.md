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
- Day trips, and splitting or changing the dates of a trip. Joining two trips is covered by rule 3c,
  and adding a trip by hand by the manual trip command.
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
| Home blip | 2 photos, 24 hours | A run of at most 2 photos at home doesn't end a trip when the away photos on both sides are at most 24 hours apart |
| Burst photos per day | 30 | A day with this many photos with no position can be part of a trip with no location |
| Burst minimum days | 3 | Consecutive burst days that make a trip with no location |
| Burst edge photos per day | 10 | A lighter day next to a burst joins it, for up to 2 days at each end |
| Far distance | 1,000 km | Photos this far from home can form a trip with fewer photos and days |
| Far gap limit | 7 days | A pause this long between far photos ends a far visit |
| Far minimum photos | 2 | Fewer photos than this, counting photos with no position, isn't a far visit |

### Rules

1. **Collect.** Use every photo that has a capture time.
2. **Mark away photos.** A photo with a position is away when it's farther from home than the
   home radius.
3. **Split into trips.** Sort away photos by time. Start a new trip when a photo appears inside
   the home radius, or when no away photo appears for the gap limit.
3b. **Find trips with no location.** Cameras before about 2010 recorded no position, so rules 2
   and 3 can't see those trips. A calendar day is a burst day when it has at least 30 photos with
   no position, no photo inside the home radius, and no overlap with a trip from rule 3. A run of
   at least 3 consecutive burst days is a trip. A day next to the run joins it with at least 10
   photos with no position, up to 2 days at each end, which catches travel days. The trip has no
   stops, no route, and no main place, so it's named "Trip, March 2006", and it's always Low
   confidence. Only its dates and photos are known. Count only the photos with no position on
   those days.
3c. **Apply joins.** A join is a range of days that you chose with `fibotrail join`. The groups of
   away photos from rule 3 that touch the range become one group, in time order, even when the
   pause between them is longer than the gap limit. Rule 4 then applies to the joined group, so two
   groups that are each too small can pass together. A join changes rule 3 groups only: a trip
   from rule 3b or 4b isn't joined. A range that touches fewer than two groups changes nothing.
4. **Filter.** Keep a trip that has at least the minimum photos and the minimum days. Only away
   photos count toward the minimum photos. Days run from the first day to the last, counting
   both.
4b. **Find far visits.** Rule 4 drops a short visit with few photos, such as a weekend abroad with
   a handful of geotagged photos. Day trips near home are a non-goal, so this rule looks only at
   the away photos farther from home than the far distance, and only at days that no trip from
   rule 4 or rule 3b covers. Sort those photos by time. Start a new visit when a photo appears
   inside the home radius, or when no far photo appears for the far gap limit. Add the photos with
   no position taken between the visit's first and last day, the way rule 5 does. Keep a visit
   with at least the far minimum photos in all. It has no minimum days, so one day is enough. The
   visit is always Low confidence, because it clears neither minimum. Rules 6 to 11 treat it like
   any other trip.
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
   state and then the country as fallbacks. See [main destination](main-destination.md).
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
  [main destination](main-destination.md).
- A detected trip with the fingerprint of a stored trip is the same trip. Its photos, stops, and
  route are updated. Its status, name, and cover aren't.
- New photos that continue an added trip extend it, without a new suggestion.
- A detected trip with the fingerprint of an "ignored" decision stays ignored, however many
  photos it gains.
- A detected trip that matches nothing becomes a new suggestion.
- Detected data can be deleted and rebuilt from the `photo` table at any time. The `decision`
  table is never rebuilt.
- The tables are `trip`, `trip_photo`, `decision`, `on_the_way_photo`, `trip_join`, `manual_trip`,
  and `setting`. A row in `on_the_way_photo` is a decision about one photo, and detection never
  changes it. Rules 6 to 8
  skip those photos: see [on the way](on-the-way.md). Stops and the route are stored
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

### Join command

Tracking issue: [#136](https://github.com/bradreimer/fibotrail/issues/136).

Intention: repair a trip that detection split because you took no pictures for 3 days or more.
Detection can't know the days were one trip, so you tell it, and the choice is a decision per
[ADR-016](../adr/ADR-016-a-join-is-a-decision-over-a-range-of-days.md).

- `docker exec -it fibotrail fibotrail join 2027-03-16 2027-03-24` takes one day in each trip. The
  order doesn't matter. It needs no Immich variables.
- It stores a join from the first day of the earlier trip to the last day of the later trip, runs
  detection, and prints the joined trip's name, dates, days, photos, and status.
- The joined trip keeps your decision. When one of the two trips was added, the joined trip is
  added, with that trip's name. Otherwise it takes the earlier trip's decision. The other decision
  is set aside, not deleted, and applies again if you remove the join.
- `fibotrail join --list` shows each join with its ID. `fibotrail join --remove <id>` deletes the
  join and runs detection, and the trips split again.
- It refuses, with exit code 1 and no change, when a day is in no trip or both days are in the
  same trip. Any other arguments print the usage and exit with 2.
- A join survives every scan and detection run.

### Manual trip command

Tracking issue: [#140](https://github.com/bradreimer/fibotrail/issues/140).

Intention: add a trip that no rule can find, because its photos have no position, such as a trip
photographed in 2005. You say which days it covered and where, and the choice is a decision per
[ADR-017](../adr/ADR-017-a-manual-trip-is-a-decision-that-detection-builds-from-photos.md). There
is no web screen for it.

- `docker exec -it fibotrail fibotrail trip add --start 2005-07-31 --end 2005-08-06 --place "San
  Francisco" --country "United States" --lat 37.7749 --lon -122.4194` adds a trip. `--start` and
  `--end` are required, and the last day is included. `--name`, `--place`, `--country`, and the
  pair `--lat` and `--lon` are optional. Without `--name`, the name is the place and the month,
  such as "San Francisco, July 2005". It needs no Immich variables.
- The trip holds every photo whose local day falls in the range, with or without a position. With
  no photo position in the range, the trip has one stop at `--lat` and `--lon`, which holds every
  photo. With photo positions, the stops come from them, as in rule 6. With neither, the trip has
  no stop, and the map and the statistics don't place it.
- The trip is added, as if you chose **Add trip**, with High confidence. It's stored in
  `manual_trip` with an Added decision. Detection builds it from the photos on every run, and a
  rescan keeps it, with no new suggestion. Photos that arrive later join it.
- A detected trip that shares a day with a manual trip is dropped. Its decision stays.
- The command runs detection and prints the trip's name, dates, days, photos, and status. When no
  home location is confirmed, it saves the trip and says detection runs once you confirm one.
- `fibotrail trip list` shows each trip with its ID. `fibotrail trip remove <id>` deletes the trip
  and its decision, and runs detection. To change a trip, remove it and add it again.
- It refuses, with exit code 1 and no change, when no photo was taken in the days, or when an
  added trip or another manual trip shares a day. A missing or badly formed option, `--lat`
  without `--lon`, a position out of range, or an end before the start prints a message and exits
  with 2.

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
- 4 days of 90 photos each with no position, taken far from any home photo, become one Low
  confidence trip. 2 such days, or 4 days of 25 photos, become none.
- A day with a photo at home, or inside a trip that rule 3 found, is never a burst day.
- One photo at home in the middle of a 3 day trip doesn't split it. 3 photos at home, or one
  photo with the away photos on each side more than 24 hours apart, do.
- After **Not a trip**, a rescan that adds photos to the same trip creates no suggestion.
- After a rename, a rescan keeps the new name.
- 3 photos 3,000 km from home on one day become one Low confidence suggestion. 1 photo becomes
  none.
- 2 photos 3,000 km from home 4 days apart, with 90 photos with no position between them, become
  one Low confidence suggestion, and the 90 photos belong to it.
- 25 photos 200 km from home on one day, or 3 photos 900 km from home, become no suggestion.
- Photos 3,000 km from home on days that a rule 4 trip covers add no second suggestion.
- `fibotrail join` turns two trips 6 days apart into one trip that holds every photo. The trip
  keeps its added status and name. A later detection run keeps it, with no new suggestion.
- Two groups of 12 photos each, 6 days apart, become no trip. With a join they become one.
- `fibotrail join --remove` splits the trips again.
- `fibotrail trip add` turns a week of photos with no position into one added trip with every
  photo and one stop at the position you gave. A later detection run keeps it, with no new
  suggestion. `fibotrail trip remove` removes it and its decision.
- A manual trip replaces a suggested trip that shares its days. It's refused when no photo falls
  in its days or an added trip shares a day.
- The review gate is met on the maintainer's library: at least 8 of the first 10 High-confidence
  suggestions are accepted, and at most 1 of 3 to 5 remembered trips is missing. This is the gate
  for MVP step 3a.
- The replay command still runs and still reports each known trip, but its result doesn't gate a
  release.

## Open questions

The review gate was met on 2026-11-16: 10 of the first 10 High-confidence suggestions were
accepted, and 5 of 5 remembered trips were found. No split trip showed among those 10, and no
remembered trip was missed. The causes weren't recorded, and Medium and Low suggestions weren't
reviewed, so the questions below stay open until a case turns up.

1. Are the thresholds for High, Medium, and Low confidence right? Rule 10 has the starting
   point. The Low margin applies to the photo count only: with a minimum of 2 days, a quarter of
   a day can't be measured, and every weekend trip would be Low. The review gate on the real
   library settles it, and it hasn't run yet.
2. Does a photo at home split too many trips? The blip rule was added on 2026-11-17, after one
   photo at home split the March 2023 Disneyland trip. The original question stays open for
   longer runs at home. A photo inside the home radius ends a trip, per
   rule 3. If someone else in the household adds photos at home to the same library while you're
   away, your trip is split in two. The review gate shows how often this happens.
3. Should photos with no position count toward the minimum photos? 84.5 percent of the
   maintainer's photos have no position, and rule 4 counts away photos only. Rule 3b answers it
   for trips with no position at all, found on 2026-11-16: March 2006 and March 2007 were missed
   because no photo from those years has a position. On the maintainer's library rule 3b finds
   about 29 more trips, all Low confidence. How many are real trips isn't known yet. Christmas
   and weekend gatherings at home with no position may be among them.
4. Does the home suggestion work for a library with two homes, such as after a move? The MVP uses
   one home location. A dated list of homes is a later feature if the review gate shows a need.
