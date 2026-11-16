# Life map

Tracking issue: [#45](https://github.com/bradreimer/fibotrail/issues/45).

## Purpose

This spec defines the first release of the Map screen, phase 4 of the
[roadmap](../roadmap.md): a world map that fills in the countries you've visited, with a summary,
your first visit to each country, and a switch to show your trip routes. It turns the trips you've
added into one picture of where you've been.

The first release stops at countries. Zooming to regions and cities needs boundary data that isn't
in the app, and a decision about how to name and count them. That is
[#48](https://github.com/bradreimer/fibotrail/issues/48). The work is split across
[#45](https://github.com/bradreimer/fibotrail/issues/45) (the screen),
[#46](https://github.com/bradreimer/fibotrail/issues/46) (which countries count), and
[#47](https://github.com/bradreimer/fibotrail/issues/47) (the navigation change). The data decision
is [ADR-011](../adr/ADR-011-visits-are-derived-from-added-trips-on-the-server.md), accepted.

## User story

As a person with years of photos, I want to open a world map that fills in the countries I've
visited, so that I can see my travel history at a glance.

## Goals

- One glance answers "where have I been?".
- Every number comes from the trips you've added, and nothing from a suggestion.
- A country you only passed through doesn't fill in.
- Nothing leaves your device: the map is the bundled one.

## Non-goals

- Regions and cities, and zooming to them ([#48](https://github.com/bradreimer/fibotrail/issues/48)).
- Collections such as Disney parks, and the Stats screen. They are phase 5.
- Continents, and a count of cities. City names from Immich are granular, so a count would
  overstate how many cities you've seen.
- Adding or removing a visit by hand.
- Filters by year or collection.

## Requirements

### What counts as visited

- Only trips with the status "added" count. Suggested, later, and ignored trips don't.
- A country is visited when an added trip has a stop in it. Stops are the ones stored with the trip
  ([ADR-010](../adr/ADR-010-stops-and-routes-are-stored-with-the-trip.md)), and they leave out
  photos that are on the way ([on the way](on-the-way.md)), so a layover country doesn't fill in.
- A stop belongs to the country whose outline contains its position. The outlines are the bundled
  Natural Earth countries of [ADR-009](../adr/ADR-009-a-bundled-natural-earth-base-map.md).
- A stop that no outline contains, such as one on a coast at 1:50 million detail, joins the nearest
  country within 25 km. A stop farther than that from every country counts for none. 25 km is a
  starting value, and real trips may change it.
- A trip with stops in two countries counts for both.
- The home country counts only when an added trip has a stop there. See the open questions.
- The first visit to a country is the start date of the earliest added trip that has a stop there.

### The Map screen

- **Header and map.** The world map is the bundled base map. Visited countries are filled with
  Meadow. Other land keeps the land fill. The map pans and zooms with a pinch on a touch screen.
- **Summary.** A strip with four numbers: countries visited, trips, days away, and distance
  traveled. Days away is the sum of the days of added trips. Distance is the sum of their
  distances, the same numbers as on the trip pages.
- **A visited country** opens a sheet when you tap it: the country's name, how many trips went
  there, the date of your first visit, and the trips, each linking to its trip page. An unvisited
  country does nothing when tapped.
- **First visits.** A list under the map shows each visited country with the date of its first
  visit, oldest first. It is also the text alternative to the map: a screen reader reads the
  visited countries from it, and "visited" is never shown by color alone.
- **Routes.** A switch, off by default, draws the route of every added trip on the map as lines
  through its stops, in the style of the trip page.
- **No outside request.** The map uses no tile source, font host, or image host, per ADR-009.

### States

| State | What you see |
| --- | --- |
| Loading | A gray map shape and skeleton numbers in the final layout |
| Empty | "No places yet. Add a trip, and its countries fill in." and a link to the Inbox |
| Error | A plain explanation and **Try again** |

### Navigation

- When the Map ships, the tab bar holds Trips and Map. Collections and Stats don't exist yet, and a
  tab never leads to a screen that doesn't exist, per
  [ADR-007](../adr/ADR-007-native-ios-feel-through-a-progressive-web-app.md).
- Inbox and Settings move to the top bar as icon buttons, each at least 44 points, with accessible
  names. The Inbox badge moves with the Inbox button.
- The [app shell spec](app-shell.md) is updated in the same change.

### API

- `GET /api/map` needs a session, like every other route. It returns the visited countries (name,
  code, first visit, and trips with ID, name, and dates), each added trip's route (the stops in
  order), and the totals (countries, trips, days away, and distance in kilometers).
- The OpenAPI document and the TypeScript types are updated, per
  [web-app-development.md](../knowledge/web-app-development.md).

### Accessibility and iOS

- The screen meets the table in ADR-007. Sheet and viewer styles follow the rules recorded for the
  Add trip sheet: dialogs sit outside any scroll layer, so they cover the tab bar.
- The switch and the sheet have visible labels, work with a keyboard and a screen reader, and honor
  reduce motion. Nothing animates when the setting is on.

## Acceptance criteria

- With no added trips, the screen shows the empty state and no filled country.
- With added trips in France and Sweden, two countries fill in, and the strip says 2 countries.
- A suggested, later, or ignored trip's country doesn't fill in.
- A country where an added trip has only photos on the way doesn't fill in.
- Tapping a visited country opens a sheet with its trips and the date of the first visit, and each
  trip links to its page.
- The first visits list shows each visited country once, oldest first, and a screen reader reads
  it.
- The routes switch draws one line for each added trip, and turning it off removes them.
- A stop 10 km off a coast, outside every outline, counts for the nearest country. A stop 40 km
  from every country counts for none.
- Days away is the sum of the days of added trips, and distance is the sum of their distances.
- The browser's network log shows no request to a host other than Fibotrail.
- The tab bar shows Trips and Map. Inbox and Settings are in the top bar, and the Inbox button shows
  the badge.
- The screen passes the ADR-007 run-through on an iPhone.

## Open questions

1. **Should the home country always fill in?** Today it fills in only when a trip has a stop there,
   so a library of foreign trips leaves home gray. Filling it always would count a place you didn't
   travel to. The default is to count it only after a domestic trip.
2. **Is 25 km the right distance for a stop outside every outline?** It is a starting value, not
   tested on real trips.
3. **Where do Inbox and Settings go on a tablet or desktop?** ADR-007 moves them to the top bar for
   the phone tab bar. The sidebar layout (600 px and wider) has room for all four destinations.
   The default is to keep them in the sidebar there, and to move them only under 600 px.
4. **How are regions and cities named and counted?** That belongs to
   [#48](https://github.com/bradreimer/fibotrail/issues/48), and to the Stats screen.
