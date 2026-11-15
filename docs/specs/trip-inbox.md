# Trip inbox

Tracking issue: [#9](https://github.com/bradreimer/fibotrail/issues/9).

## Purpose

This spec defines the inbox, where you review the trips Fibotrail detected. It works like device
discovery in Home Assistant: Fibotrail suggests, and nothing joins your travel history until you
say yes.

It covers stories U5 and U6 in the [roadmap](../roadmap.md#mvp-version-01), MVP step 3b.

## User story

As a person back from a vacation, I want to review each suggested trip and choose **Add trip**,
**Not a trip**, or **Later**, so that only the trips I confirm become part of my travel history.

## Goals

- You can judge a suggestion from its card, without opening anything.
- Each choice takes one tap or one swipe.
- A choice is saved as a decision and survives every rescan.
- You can clear the inbox with one hand on a phone.

## Non-goals

- Creating a trip by hand from a date range or an Immich album.
- Merging, splitting, or changing the dates of a suggestion.
- Notifications about new suggestions.
- Adding or ignoring several suggestions in one action.

## Requirements

### Inbox list

- The Inbox screen has two tabs: **Suggestions** and **Ignored**.
- Before a home location is confirmed, the Inbox shows the home location step of the
  [trip detection spec](trip-detection.md) in place of the tabs.
- The inbox lists suggestions with the status "suggested" first, then those with the status
  "later". Within each group, the newest trip comes first.
- A badge on the Inbox destination shows the count of "suggested" items, such as "1 possible
  trip" for a screen reader. "Later" items aren't counted.
- With no suggestions and no scan running, the inbox says "No possible trips right now." and
  offers **Scan now**. While the first scan runs, it says "No trips yet. Fibotrail is still
  scanning your library."

### Suggestion card

Each card shows:

- A sentence in the voice of the design and branding document, such as "We found a possible trip:
  Orlando, Mar 15–24, 2027. 312 photos." The year is always shown, because a library holds trips
  from many years.
- The main places: up to three city names, by photo count.
- A confidence label with an icon and a word: High, Medium, or Low.
- A strip of up to eight photos, spread evenly across the trip's days.
- A small route preview: the stops and the lines between them, drawn with the trip page's map
  styling. It doesn't pan or zoom. It's an SVG drawing with north at the top and no base map,
  labeled "Photo route".
- Three buttons: **Add trip** (primary), **Not a trip** (secondary), and **Later** (text).

### Actions

| Action | Result |
| --- | --- |
| **Add trip** | Opens a sheet with the suggested name and cover photo. You can change the name and choose another cover from the trip's photos. **Add** saves the trip with the status "added" and removes the card |
| **Not a trip** | Sets the status "ignored" and moves the card to the **Ignored** tab |
| **Later** | Sets the status "later". The card stays in the inbox, below the new suggestions |

- Every action writes a row to the `decision` table by fingerprint, per
  [ADR-002](../adr/ADR-002-decisions-are-stored-separately-from-detected-data.md).
- A name has 1 to 100 characters. A cover is one of the trip's photos.
- When you add a trip with the suggested name or cover unchanged, the decision stores no name or
  cover, so a later detection run can still improve the suggestion.
- A card with the status "later" has no **Later** button.
- A swipe counts when the card moves 96 pixels sideways. A mostly vertical move scrolls the list.
- On touch screens, a swipe right on a card is **Add trip** and a swipe left is **Not a trip**.
  The buttons are always visible.
- **Not a trip** is never red. Ignoring is a normal choice.
- After an action, the next card takes the place of the old one, and focus moves to it.

### Ignored tab

- The **Ignored** tab lists ignored suggestions, newest trip first, with the same card and one
  button: **Restore**.
- **Restore** sets the status "suggested" and returns the card to the inbox.

### Accessibility and iOS

- Every action is reachable by keyboard and announced by a screen reader. A swipe is never the
  only way to act.
- Each photo in the strip has alt text built from its date and place.
- The screen meets the table in
  [ADR-007](../adr/ADR-007-native-ios-feel-through-a-progressive-web-app.md).

## Acceptance criteria

- With two new suggestions and one "later" item, the badge shows 2, and the "later" item is last.
- **Add trip**, with the name changed, shows the trip in the trip library under the new name.
- **Not a trip** moves the card to **Ignored**, and the badge count drops by one.
- After **Not a trip**, a rescan doesn't return the card to the inbox.
- **Restore** returns the card to the inbox, and the badge count rises by one.
- A swipe right opens the same sheet as **Add trip**.
- With a keyboard only, you can add one suggestion and ignore another.
- With no suggestions, the empty message and **Scan now** show.
- The screen passes the ADR-007 run-through on an iPhone.

## Open questions

None. The route preview doesn't use the bundled outline map of
[ADR-009](../adr/ADR-009-a-bundled-natural-earth-base-map.md). A browser allows about 16 WebGL
maps on one page, and the inbox can hold more suggestions than that, so the preview is an SVG
drawing of the stops and the lines.
