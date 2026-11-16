# Trip names

Tracking issue: [#30](https://github.com/bradreimer/fibotrail/issues/30).

## Purpose

This spec defines how Fibotrail names a trip and where a trip's main destination comes from. A
suggested trip arrives with a name you didn't choose. A good name should take one tap, and a wrong
guess shouldn't cost you typing.

The main destination section is built under
[#31](https://github.com/bradreimer/fibotrail/issues/31), and the name options under #30.

It extends [trip detection](trip-detection.md) (rule 9), the [trip inbox](trip-inbox.md) (the
**Add trip** sheet), and the [trip page](trip-page.md) (**Rename or change cover**). It was added
after the maintainer's first review of real suggestions, on 2026-11-16: names such as "Horizon
West" and "Paradise" were not what the trips would be called.

## User story

As a person reviewing suggested trips, I want to pick a trip's name from a few options, or type my
own, so that a good name takes one tap. I also want the main destination to be where I spent the
most days, so that the first guess is usually right.

## Goals

- The first suggested name is right more often than today's.
- A different name is one tap away, with no typing.
- Your own name is always possible, and it always wins.

## Non-goals

- Editing place names, or changing what Immich calls a place.
- A name from an outside service or a language model. Names come from the places in your own
  photos, per [ADR-001](../adr/ADR-001-immich-is-read-only.md).
- Naming or merging stops. Airports and layovers have their own idea in
  [#32](https://github.com/bradreimer/fibotrail/issues/32).

## Requirements

### Main destination

- The main place of a trip is the city with the most distinct local days that have at least one
  away photo. A tie goes to the city with more away photos, then to the one with the earlier first
  photo.
- When no photo has a city, the main place is the state counted the same way, then the country.
- A day counts for a place when at least one of that day's away photos is there. A day with photos
  in two cities counts for both.
- The main place sets the fingerprint, the suggested name, and the main destination on the trip
  card and the inbox card. The inbox card's "main places" line lists up to three cities by days,
  then by photos.
- Changing the rule moves the fingerprints of existing trips. A trip you added or ignored keeps its
  decision, because reconciliation matches by dates when no fingerprint matches, per
  [trip detection](trip-detection.md#reconciliation-and-decisions).
- That date match needs the stored trip to cover more than half of the new trip's days. A trip
  that more than doubles in length and changes its main place in the same run is treated as a new
  suggestion. The old trip is removed, and its decision stays and applies again if the old trip
  comes back.

### Name options

- Detection returns up to five name options for each trip, distinct when compared without regard
  to case, in this order. Each is `<place>, <Month Year>`, with the month and year of the first
  day, such as "Orlando, March 2025".
  1. The main place. This is the suggested name.
  2. The city with the second most days, when it exists.
  3. The state or region with the most away photos, when known.
  4. The country with the most away photos, when known.
  5. `Trip, <Month Year>`, which is always available.
- An option that duplicates an earlier one is dropped, and the list closes up. A trip always has at
  least option 5.
- A name has 1 to 100 characters. An option longer than that is cut at 100 characters.
- The options are stored with the trip and rebuilt on every detection run, like stops and routes
  ([ADR-010](../adr/ADR-010-stops-and-routes-are-stored-with-the-trip.md)).
- The API returns `nameOptions`, an array of strings, for a suggestion and for a trip.

### Choosing a name

- The **Add trip** sheet and the **Rename or change cover** sheet show the **Name** field first,
  then the options as a group of buttons under the label "Suggested names".
- The field starts with the current name. For a suggestion that is option 1.
- Tapping an option fills the field with it and marks that button as chosen. Typing in the field
  clears the mark, unless the text equals an option exactly.
- Your own name is any text of 1 to 100 characters. It is saved as the name decision.
- Choosing the suggested name, or any text equal to it, stores no name, so a later detection run can
  still improve it. Any other name is saved as a decision and survives rescans, per
  [ADR-002](../adr/ADR-002-decisions-are-stored-separately-from-detected-data.md).
- The options never change a name you chose. A rescan can change the options list, but not your
  name.
- On a phone the buttons wrap onto as many lines as they need, and each is at least 44 points tall.
- The group is labelled "Suggested names". Each button's chosen state is available to a screen
  reader as pressed or not pressed. The buttons work with the keyboard.

## Acceptance criteria

- A trip with 3 days in Orlando and 1 busy day with 300 photos in Kissimmee has the main place
  Orlando.
- A tie in days goes to the city with more away photos.
- With no city on any photo, the main place is the state with the most days, then the country.
- A trip in Orlando, Florida, United States, in March 2025 gets the options "Orlando, March 2025",
  "Florida, March 2025", "United States, March 2025", and "Trip, March 2025", in that order, plus a
  second city when one exists.
- When the state and the main place have the same name, the list has no duplicate.
- A trip with no place names at all has one option: "Trip, March 2025".
- Tapping an option fills the name field, and the button reads as pressed.
- Typing your own name clears the mark. **Add** saves that name, and it survives a rescan.
- Choosing the first option on a suggestion stores no name.
- A trip added before the main destination rule changed keeps its status, name, and cover, even
  though its fingerprint moves.
- The group has the label "Suggested names", and each button is at least 44 points tall.
- The sheet passes the ADR-007 run-through on an iPhone.

## Open questions

1. Should the country option appear for a trip in your home country? It's a weak name for a
   domestic trip. The default is to show it, and to drop it if the review shows it's never chosen.
2. Does "days" beat "photos" on real trips that arrive by a long drive? The review gate in the
   [trip detection spec](trip-detection.md#review-gate) shows how often the first name is kept.
