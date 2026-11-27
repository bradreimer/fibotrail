# Pin game

Tracking issue: [#183](https://github.com/bradreimer/fibotrail/issues/183).

## Purpose

Pin is the third game on the [Games tab](games.md). It shows one of your trip photos, and you tap
the map where you think you took it. Each miss tells you how far off you were and which way to go.
Timeline asks when, and Slide is the calm one. Pin asks where, which is what Fibotrail knows best.

Think of a friend holding up one of your vacation photos and asking, "Where was this?", then saying
"warmer" or "colder" after each guess.

```text
 Photo          Try 1              Try 2             Try 3
 ┌──────┐      🟥 2,400 km ↗       🟨 310 km →        🟩 18 km
 │  📷  │ ──▶  (tap on the map)   (tap again)       Solved in 3
 └──────┘
                Share:  Fibotrail Pin #9 3/4
                        🟥↗
                        🟨→
                        🟩
```

## User story

As a person with a library of trips, I want to tap the map where I took one of my photos each day,
so that I remember where my trips went and can share how close I got.

## Goals

- One puzzle a day, the same on every device.
- A game takes under a minute, and works with one thumb on a phone.
- The marks after each try point you toward the answer without giving it away.
- You win most days, and a close first guess feels good.

## Non-goals

- Map tiles, street names, or city labels. The map is the bundled base map, with no outside
  request, per ADR-009.
- Hints, a timer, or a past day's puzzle.
- Points for distance. A try solves the puzzle or it doesn't, as in the other games.

## Requirements

### The game library

- Pin needs a new fact: the place where each photo was taken. `GamePhoto` gains a latitude and
  longitude, both optional, and `GameLibraryLoader` loads them from the photo. A photo with no
  location has none, and Pin never picks it. This is the one change outside Pin's own folders.

### The puzzle

- Pin needs **1** added trip with at least one photo that has a location. With none, the game says
  so and links to the inbox.
- From the date's seed, the game picks one of those trips, then one of its photos with a location.
- The answer is that photo's latitude and longitude.

### Playing

- The photo shows at the top, at 3:2, the full width of a phone and no wider than about 500 px on a
  wide screen. The map fills the rest of the screen.
- While you play, the screen shows no trip name, date, place, or country name.
- The map pans and zooms with a pinch on a touch screen, and with drag and scroll on a computer, as
  on the Map screen.
- A tap places a pin. A second tap moves it. **Check** sends the pin. A press that moves more than
  8 px is a pan, and places no pin. While you play, the map stays where you left it; it doesn't
  zoom to a new pin.
- You have **4 tries**. Earlier pins stay on the map, dimmed, each with its mark.
- **Keyboard and VoiceOver.** Under the map, **Pick a place from a list** opens a country picker,
  then, for Canada and the USA, a region picker. Choosing one places the pin at the center of the
  bounding box of its largest piece of land. Check is a button. The places come from the same
  outlines as the map, so no other host is asked.

### Marks

Each try is graded by the great-circle distance (haversine, Earth radius 6,371 km) from the pin to
the answer.

| Mark | Distance | Meaning |
| --- | --- | --- |
| 🟩 Green | 50 km or less | Solved |
| 🟨 Yellow | over 50 km, up to 500 km | Close |
| 🟧 Orange | over 500 km, up to 2,000 km | Same part of the world |
| 🟥 Red | over 2,000 km | Far |

- A miss also shows an arrow: the direction from the pin to the answer, as the initial bearing
  rounded to one of 8 compass points (↑ ↗ → ↘ ↓ ↙ ← ↖). The arrow is part of the mark, so it never
  shows by color alone.
- A miss shows the distance under the mark, rounded up to 10 km below 1,000 km and to 100 km from
  there, such as "310 km" or "2,400 km". Rounding up keeps a 51 km miss from showing "50 km". The distance shows on screen only. It isn't in the share text.
- VoiceOver reads a mark as words, such as "Close, 310 kilometers, go east".

### The end

- When you solve it or run out of tries, the map draws a line from your last pin to the answer, and
  zooms to fit both.
- The photo shows with a caption, as in Slide: the trip name, which links to the trip page, then
  its date range and its main place. A trip with no known place shows the dates alone.
- "Solved in 3." or "So close. It was 310 km away.", Share, and the stats show too, with the
  "Tries to win" bars for 1 to 4.

### The server

- `GET /api/games/pin/puzzle?date=YYYY-MM-DD` returns the puzzle number, the photo ID, the most
  tries, and, when the day is played, its result. It sends no location, trip name, date, or place.
  With no puzzle, it returns `tripsNeeded: 1` and no photo.
- `POST /api/games/pin/check` with a date and one pin returns its mark: the pin, the band, the
  direction, and the shown distance. It stores nothing. A pin off the map is 400.
- `POST /api/games/pin/results` with a date and the pins, each a latitude and longitude, grades
  them. There must be 1 to 4 pins, each latitude from −90 to 90 and each longitude from −180 to 180,
  and no pin may follow a solved one, or the answer is 400. The game must end solved or with 4
  pins, or the answer is 400. The browser never sends a distance, a mark, or a score.
- A result is stored in `game_result` as solved or not, the tries used, and 4 tries allowed. Its
  marks are the pins, as Slide's marks are its moves: "latitude longitude" with 4 decimals (about
  11 m), joined by ";", such as "45.5 -73.6;48.8566 2.3522". Pins are rounded before they're
  graded, so a stored result grades the same when it's shown again, and the end map can draw it.
  No new table is needed. The pins are in Fibotrail's own database only.
- The result has the marks, each try's distance, the answer's location, the share text, the photo's
  trip (ID, name, start and end dates, place), and the stats.
- The share text reads like `Fibotrail Pin #9 3/4`, then one line per try of the square and the
  arrow. An unsolved day shows `X/4`. It holds no distance, place, trip name, date, or photo.

## Acceptance criteria

- The same date and library give the same photo.
- A photo with no location is never picked. With no such photo, the game says so.
- The puzzle route sends no location, trip name, date, or place.
- A pin 50 km or less from the answer solves the puzzle. Pins at 51, 501, and 2,001 km get Yellow,
  Orange, and Red.
- The arrow points from the pin toward the answer, and works across the date line and the poles.
- Pins out of range, more than 4 pins, or a pin after a solved one are refused with 400, and nothing
  is stored.
- A second result for the same day keeps the first.
- At the end, a line joins the last pin and the answer, and the photo shows with its trip name,
  dates, and place.
- The share text has squares and arrows only.
- The map makes no outside request.

## Open questions

- Is 50 km the right size for a win? On a world map with no labels, a pin within 50 km takes some
  zooming. Revisit after a few weeks of play; 100 km is the fallback.
- Many trips start at the same few places, such as one theme park. Should the pick prefer a photo
  far from the trip's other puzzles? Not until repeats are a problem.
- Does the country and region picker give the answer away to VoiceOver users, since a region center
  is often within 50 km? Check with a VoiceOver user before release.
