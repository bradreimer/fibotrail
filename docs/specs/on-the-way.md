# On the way

Tracking issue: [#35](https://github.com/bradreimer/fibotrail/issues/35).

## Purpose

This spec defines what it means for a photo or a stop to be **on the way**: it belongs to the trip,
but it isn't a place you went to. Layover airports and photos from a plane or train window are worth
keeping, but detection makes a stop for every place with photos, so each one adds a pin, a detour
in the route, and distance that you didn't travel for the trip itself. A place on the way also
shouldn't fill in a country on the life map (phase 4 of the [roadmap](../roadmap.md)), or count as a place you visited.

One idea has two parts, and they use the same words and have the same effect:

1. **You hide a photo.** A checkbox in the photo viewer puts one photo on the way. This is built
   first, tracked by [#35](https://github.com/bradreimer/fibotrail/issues/35).
2. **Fibotrail suggests stops.** Detection proposes that a short first or last stop is on the way,
   and you can accept or undo it. This comes later, tracked by
   [#32](https://github.com/bradreimer/fibotrail/issues/32), because its rule isn't tested yet.

The manual part works today and stays useful when the automatic part guesses wrong. The two earlier
names for this, "getting there" and "along the way", mean the same thing, and "on the way" replaces
both.

## User story

As a person who travels by plane, I want to put a photo on the way from the photo viewer, so that
layover airports and in-transit photos stay in my trip but don't add stops, lines, or distance to
the map.

## Goals

- One tap in the photo viewer puts a photo on the way, or brings it back.
- The checkbox label says what it does, **Hide**, not why you'd use it. The hint says what stays:
  the photo is still in the trip.
- The map, the stat tiles, and the stop list update at once, without a rescan.
- The mark is a decision. It survives every rescan, per
  [ADR-002](../adr/ADR-002-decisions-are-stored-separately-from-detected-data.md).

## Non-goals

- Finding airports or layovers automatically. That is the second part of this idea, in
  [Suggesting stops](#suggesting-stops-later).
- Marking several photos at once.
- Changing how photos are split into trips, or how the main destination is chosen.
- Removing a photo from a trip, or moving it to another trip.
- Writing anything to Immich.

## Requirements

### The mark

- A photo is either on the way or not. New photos aren't on the way. This spec calls the state
  "on the way", and the checkbox that sets it **Hide**.
- The state is stored as a decision, keyed by the Immich asset ID, in its own table. Detection
  never rebuilds or changes it.
- The mark belongs to the photo, not to the trip. If detection later puts the photo in a
  different trip, the mark goes with it.

### Effect on detection

- Rules 6 to 8 of [trip detection](trip-detection.md#rules) skip photos on the way. Photos on the way
  join no stop, so they add no pin, no line, and no distance, and they don't count toward
  "farthest from home."
- A stop whose photos are all on the way disappears. A stop with some photos on the way stays, and
  its photo count leaves them out.
- Rules 1 to 5 and 9 to 11 are unchanged. Photos on the way still count toward the trip's photos,
  days, minimum photos, and main destination. Confidence (rule 10) reads the same photo count and
  days, and it reads the farthest point from home, which follows the stops, so a far layover that
  goes on the way can lower a trip's confidence.
- Visits for the [life map](life-map.md) are built from the stored stops, so a country that only
  has photos on the way doesn't count as visited.
- Changing the state recomputes that trip's stops, route, distance, and farthest point at once,
  because they're stored with the trip, per
  [ADR-010](../adr/ADR-010-stops-and-routes-are-stored-with-the-trip.md). Other trips aren't
  touched.

### Photo viewer

- The viewer shows a **Hide** checkbox under the place name, with the hint "Keeps this
  photo in the trip but off the map."
- Checking or clearing it saves at once. There's no Save button.
- When the save fails, the checkbox returns to its earlier state, and a short message says "We
  couldn't save that. Try again."
- The small map in the viewer still shows the photo's own position.

### Trip page

- The Photos tile, the day timeline, and the viewer's "2 of 500" count still include photos on the way.
- A photo on the way shows a small mark in the corner of its timeline thumbnail. Its alt text ends
  with ", hidden from the map" (no full stop, like the rest of the alt text).
- The route map, the stop pins, the **Stops in order** list, and the Stops, Distance, and Farthest
  from home tiles leave photos on the way out.
- The inbox route preview leaves them out too, because it reads the same stored stops.

### Suggesting stops (later)

This part isn't built, and its rule isn't tested. It is written down so the two parts stay one idea.

- Detection may propose that a stop is on the way. A candidate rule, to be checked against real
  trips first: the stop is the first or last stop of the trip and has fewer than 5 photos, or the
  photos at it span less than 3 hours. Immich's place names don't say which place is an airport, so
  the rule uses time and photo count only.
- A proposed stop shows a lighter pin and the words "On the way" in **Stops in order**. It isn't
  applied until you accept it. Accepting hides the photos at that stop, the same as the checkbox.
- Your choice wins. A stop you've put back on the way or brought back stays that way, and detection
  never proposes it again.

#### Measuring the rule

`fibotrail stops` ([#60](https://github.com/bradreimer/fibotrail/issues/60), the first step of
[#32](https://github.com/bradreimer/fibotrail/issues/32)) shows how the candidate rule behaves on a
real library, so the rule is tuned before any suggestion is built. It is read-only and it applies nothing: no stop is hidden, no photo is
marked, and no trip is changed.

- The command is `docker exec -it fibotrail fibotrail stops`. It takes no arguments.
- It reads the stops stored with each **added** trip, in order of start date. Photos already on the
  way aren't in those stops, so they don't count here. Trips in the inbox, ignored trips, and trips
  you put off with Later are skipped.
- For each trip it prints the name and dates. For each stop it prints the number in the route, the
  place, whether the stop is first, last, middle, or the only stop, the photo count, the time from the
  first photo to the last, and either `not flagged` or `FLAGGED` with the condition that held.
- It ends with the number of trips and stops, the number of flagged stops, and how many flags came
  from each condition: first or last with fewer than 5 photos, photos spanning less than 3 hours, and
  both.
- The thresholds are the defaults, 5 photos and 3 hours. The function is
  `OnTheWayRule.Assess` in `Fibotrail.Detection`, and it takes both as parameters, so a later change
  to the rule needs no change to the stored data.
- The output holds trip names, place names, counts, and times. It never holds coordinates, photo IDs,
  or the Immich key, so it can be pasted into a conversation.
- A stop with one photo spans 0 hours, so the rule flags it wherever it is in the trip.

What the output can't tell you: whether a flagged stop was really on the way. Read each flagged line
and decide. A rule that flags stops you wanted to keep, or misses layovers, is the reason to change a
threshold, and the results settle that before [#32](https://github.com/bradreimer/fibotrail/issues/32)
builds suggestions.

### API

- One route sets or clears the mark for one photo, and returns the trip's new stops and measures.
- The trip route returns, for each photo, whether it's on the way.
- The OpenAPI document and the TypeScript types are updated, per
  [web-app-development.md](../knowledge/web-app-development.md).

### Accessibility and iOS

- The checkbox has a visible label, a tap target of at least 44 by 44 points, and works with a
  keyboard and a screen reader.
- The viewer meets the table in
  [ADR-007](../adr/ADR-007-native-ios-feel-through-a-progressive-web-app.md).

## Acceptance criteria

- A trip with stops at home airport, a layover airport, and Orlando shows three pins. Marking every
  photo at the layover shows two pins and one line, and the Distance tile shrinks to match,
  without a rescan.
- Hiding one of three photos at a stop keeps the pin, which then shows 2.
- After marking, the Photos tile and the timeline show the same count as before.
- A photo on the way has a mark on its thumbnail, and clearing the checkbox brings the stop back.
- After a rescan, and after deleting every `trip` and `trip_photo` row and running detection
  again, the mark is still there and the layover still has no pin.
- With the server stopped, checking the box shows the message and leaves the box clear.
- The viewer passes the ADR-007 run-through on an iPhone.

## Open questions

1. **Should the checkbox read Hide or On the way?** The state is called "on the way" everywhere
   else, in this spec and in **Stops in order**. The checkbox is **Hide**, because it says what it
   does. If the two words confuse people in the iPhone run-through, change the checkbox to "On the
   way", with the same hint.
2. **Checkbox or switch?** The request asks for a checkbox. iOS settings use a switch for an
   on-off choice like this one, and ADR-007 asks for a native feel. The spec says checkbox until
   the iPhone run-through says otherwise.
3. **Should a photo on the way affect the main destination?** Not yet. A layover rarely wins on days,
   so it rarely names a trip. If real trips show otherwise, rule 9 can skip photos on the way too.
