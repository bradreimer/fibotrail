# Trip library

Tracking issue: [#10](https://github.com/bradreimer/fibotrail/issues/10).

## Purpose

This spec defines the Trips screen: a grid of cards, one for each trip you've added. It's the
front cover of your travel journal and the way into every trip page.

It covers story U7 in the [roadmap](../roadmap.md#mvp-version-01), MVP step 3c.

## User story

As a person on the couch with an iPad, I want to see my trips as cards with a cover photo, dates,
and place, so that I can pick one to relive.

As a person with a lot of trips, I want to search by a name, a year, or a place, such as "disney",
so that I can find one trip without scrolling.

## Goals

- Your photos are the largest thing on the screen.
- You can recognize a trip from its card alone.
- The screen stays fast with 200 trips.

## Non-goals

- Filter chips or menus by year, country, or collection. Search covers these.
- Sort orders other than newest first.
- Collection badges on cards.
- Searching the inbox, or inside a trip's photos.

## Requirements

### Content

- The library shows every trip with the status "added", and no other trip.
- Trips are sorted by start date, newest first.
- An API route returns, for each trip: its ID, name, start and end dates, day count, main
  destination, cover photo ID, and search keywords.

### Trip card

- The card shows a 4:3 cover photo, cropped to fill, with a bottom gradient so the text over it
  stays readable.
- Over the photo: the trip name in Fraunces at 21 px, and one line in Inter at 13 px with the
  dates, the day count, and the main destination, such as "Mar 15–24, 2027 · 10 days · Orlando".
- The card has a 13 px corner radius and no shadow.
- The whole card is one tap target. Tapping it opens the [trip page](trip-page.md).
- The cover photo's alt text is the trip name and dates.
- A screen reader reads the card once, as one label: the name, then the line, such as "Disney
  with the kids, Mar 15–24, 2027 · 10 days · Orlando".
- When a cover photo can't load, the card shows a Mist background with the spiral mark.

### Layout

| Width | Columns |
| --- | --- |
| Under 600 px | 1 |
| 600 to 1023 px | 2 |
| 1024 px and up | 3 |

- Cover photos load through the thumbnail proxy as their cards come within one screen of view.
  The browser's own lazy loading does this, so the exact distance is the browser's choice.
- The column count follows the navigation of the [app shell](app-shell.md), which changes at the
  same widths.
- Going back from a trip page returns to the scroll position you left.

### Search

Search is tracked in [#157](https://github.com/bradreimer/fibotrail/issues/157).

- A search field sits above the grid, with the placeholder "Search trips, years, and places". Its
  text is at least 16 px, so iOS doesn't zoom on focus. The keyboard's key reads **Search**, and
  pressing it closes the keyboard.
- A trip matches when every word of the search appears in its name, main destination, a year it
  touches, or one of its keywords. "magic 2027" finds a 2027 trip to Magic Kingdom. Case and
  accents don't matter. An empty search shows every trip.
- A trip's keywords are its countries, its cities and their stop names, its parks, their resorts,
  and their collections, such as "France", "Magic Kingdom", "Walt Disney World Resort", and
  "Disney Parks". The stats library works them out by the same rules as the
  [collections](collections.md), so a park you marked by hand counts, and a planned park never does.
- Matching happens on the device, because the whole library is already loaded. With 200 trips,
  each key press filters in well under one frame.
- The matching trips keep the newest-first order. A screen reader hears "3 trips found" after
  each change.
- With no match: "No trip matches “tokyo”. Try a trip name, a year, or a place." and
  **Clear search**.
- The search is kept in the address as `?q=`, so going back from a trip page keeps it.

### States

| State | What you see |
| --- | --- |
| Loading | Skeleton cards in the final layout |
| Empty, with suggestions waiting | "No trips yet. You have 3 possible trips to review." and a link to the inbox |
| Empty, with nothing waiting | "No trips yet. Fibotrail is still scanning your library." while a scan runs, or "No trips yet. Scan your library, and Fibotrail suggests the trips it finds." with **Scan now** when none runs |
| Error | A plain explanation and **Try again** |

## Acceptance criteria

- With three added trips, one suggested, and one ignored, the library shows three cards.
- The newest trip is first.
- At 599 px wide the grid has 1 column, at 600 px it has 2, and at 1024 px it has 3.
- Tapping a card opens that trip's page, and going back shows the same card at the same scroll
  position.
- With no added trips and two suggestions, the empty message names the count and links to the
  inbox.
- A screen reader reads each card as the trip name, dates, and destination.
- With 200 trips, the first screen of cards shows within 1 second on the local network.
- The screen passes the ADR-007 run-through on an iPhone.
- Searching "disney" shows only trips with a Disney park, and "2027" only trips that touch 2027.
- Searching "tokyo" with no Tokyo trip shows the no-match message, and **Clear search** shows every
  trip again.

## Open questions

None.
