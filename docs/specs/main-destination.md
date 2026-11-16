# Main destination

Tracking issue: [#31](https://github.com/bradreimer/fibotrail/issues/31).

## Purpose

This spec defines how Fibotrail chooses a trip's main destination. The main destination sets the
suggested name, the place on the trip card and the inbox card, and the trip's fingerprint, so a
wrong guess shows up in several places.

It changes rule 9 of [trip detection](trip-detection.md). It was added after the maintainer's first
review of real suggestions, on 2026-11-16: names such as "Horizon West" and "Paradise" were not
what the trips would be called, because the place with the most photos isn't always the place where
the trip took place. A choice of several names was considered and set aside
([#30](https://github.com/bradreimer/fibotrail/issues/30), closed as not planned): you can already
type a name when you add a trip, and a better first guess matters more.

## User story

As a person reviewing suggested trips, I want a trip's main destination to be where I spent the
most days, so that the name and the card say where I went, not where I took the most photos.

## Goals

- The suggested name is right more often than when it followed the photo count.
- The rule is simple enough to explain in one sentence.

## Non-goals

- A choice of other names. See #30.
- Editing place names, or changing what Immich calls a place.
- A name from an outside service or a language model. Names come from the places in your own
  photos, per [ADR-001](../adr/ADR-001-immich-is-read-only.md).
- Marking airports and layovers. That idea is
  [#32](https://github.com/bradreimer/fibotrail/issues/32).

## Requirements

- The main place of a trip is the city with the most distinct local days that have at least one
  away photo. A tie goes to the city with more away photos, then to the one with the earlier first
  photo.
- When no photo has a city, the main place is the state counted the same way, then the country.
- A day counts for a place when at least one of that day's away photos is there. A day with photos
  in two cities counts for both.
- The main place sets the fingerprint, the suggested name, and the main destination on the trip
  card and the inbox card. The inbox card's "main places" line lists up to three cities by days,
  then by photos.
- The suggested name is the main place, then the month and year of the first day, such as
  "Orlando, March 2027". With no place at all it is "Trip, March 2027". You can still type any name
  of 1 to 100 characters when you add a trip or rename it, and your name always wins.
- Changing the rule moves the fingerprints of existing trips. A trip you added or ignored keeps its
  decision, because reconciliation matches by dates when no fingerprint matches, per
  [trip detection](trip-detection.md#reconciliation-and-decisions).
- That date match needs the stored trip to cover more than half of the new trip's days. A trip
  that more than doubles in length and changes its main place in the same run is treated as a new
  suggestion. The old trip is removed, and its decision stays and applies again if the old trip
  comes back.

## Acceptance criteria

- A trip with 3 days in Orlando and 1 busy day with 300 photos in Kissimmee has the main place
  Orlando.
- A tie in days goes to the city with more away photos.
- With no city on any photo, the main place is the state with the most days, then the country.
- The inbox card lists its main places by days, then by photos.
- A trip added before the rule changed keeps its status, name, and cover, even though its
  fingerprint moves.

## Open questions

- Does "days" beat "photos" on real trips, such as a long drive with many short stops? The
  [review gate](trip-detection.md#review-gate) shows how often the suggested name is kept. If it is
  often wrong, a choice of names (#30) can be reopened.
