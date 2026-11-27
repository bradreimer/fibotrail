# Fetch game

Tracking issue: [#189](https://github.com/bradreimer/fibotrail/issues/189).

## Purpose

Fetch is the fourth game on the [Games tab](games.md), and the first one you play in real time.
A caped schnauzer flies one of your trips. You tap to flap through 8 gates. The gates rise and fall
with where you went: north is up. Between the gates float 8 treats, and each treat is one of the
trip's photos. The game ends on the trip's name, as Slide does. Timeline asks when and Pin asks
where. Fetch asks only that you enjoy the ride.

Fetch is an experiment, inspired by the Floppy Schnauzer project. It's built so that it can be
removed in a few steps if it doesn't earn its place. See [Removing Fetch](#removing-fetch).

Think of a paper airplane flown over the map of a road trip, with the trip's snapshots pinned along
the way.

```text
 Gate 1       Gate 2       Gate 3        ...       Gate 8      Finish
   ║    📷       ║            ║   📷                  ║           🏁
   ║           🐕~>           ║                       ║    📷
 (gap)         (gap)   📷   (gap)                   (gap)
   ║             ║            ║                       ║
 ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
   Day 1         Day 2        Day 3                 Day 6
               ▲ the gap heights trace the trip's latitude

 Share:  Fibotrail Fetch #9 2/3 🦴 6/8
         🦴⬛🦴💥
         🦴🦴⬛🦴🦴⬛🦴🦴🏁
```

## User story

As a person with a library of trips, I want a short arcade game each day where a schnauzer flies
the shape of one of my trips and catches its photos, so that I have a playful reason to look back
at a trip.

## Goals

- One course a day, the same on every device.
- A run takes about 19 seconds, and a whole day under a minute and a half. One thumb plays it.
- The course is recognizably the trip: its gates follow the trip's route north and south, and its
  treats are the trip's photos.
- The server grades every run. The browser never sends a score.
- Fetch can be removed without touching any other game or feature.

## Non-goals

- Sound, difficulty settings, an endless mode, or leaderboards.
- WebGPU. Fetch draws with Canvas 2D, which every iOS home-screen app supports.
- A full VoiceOver version of the flying. A real-time game can't be played by touch exploration.
  The game says so, and the trip still shows at the end.
- A new fact in the game library. Fetch reads only the photo times and locations that Pin already
  needs.

## Requirements

### The course

- Fetch needs **1** added trip with at least one photo that has a location. With none, the game
  says so and links to the inbox.
- From the date's seed, the game picks one of those trips, sorted by ID.
- It takes the trip's photos that have a location, in time order, and picks 8 of them evenly
  spaced: photo `round(i × (n − 1) / 7)` for i = 0 to 7. A trip with fewer than 8 such photos uses
  some photos twice.
- **Gate heights.** Each picked photo gives one gate. Its gap's center follows the photo's
  latitude: the northernmost photo is highest. The latitudes are scaled to fill the band from
  185 px to 435 px of the 700 px world, centered on the middle of the trip's span. The span is at
  least 0.5° (about 55 km), so a city trip makes a nearly flat course rather than a zig-zag of GPS
  noise.
- No gap center moves more than 150 px from the one before it, so every course can be flown.
- Each gate shows the trip day of its photo on the ground, such as "Day 3". Day 1 is the start date.
- **Treats.** Each gate has one treat after it, halfway to the next gate. Its height is halfway
  between the two gaps' centers (the last gate's own center for the last treat), then 70 px up or
  down, from the seed. A treat shows its photo's thumbnail in a circle.

### The world and the physics

All lengths are integers in hundredths of a pixel of a 400 × 700 px world, and time is in ticks
of 1/60 of a second. Only integers are used, so the C# and TypeScript simulations give exactly the
same result. The numbers are sent with each course, so the browser holds no copy of them.

| What | Value | In pixels and seconds |
| --- | --- | --- |
| Gravity | 50 per tick² | 1,800 px/s² |
| Flap | −750 per tick | 450 px/s up |
| Speed | 200 per tick | 120 px/s |
| The schnauzer | Drawn 64 px square at x = 100 px; its body is the box from (6, 20) to (58, 44) | |
| Start | Top at y = 318 px, still | |
| Ground | y = 620 px | |
| Gates | 70 px wide, a 250 px gap, the first at x = 440 px, then every 240 px | |
| Treats | A circle of radius 24 px | |
| Finish | 240 px after the left edge of the last gate | |

- Each tick: a flap on that tick sets the speed to Flap; then gravity adds to the speed; then the
  schnauzer moves by its speed, and the world by Speed.
- The run ends in a **crash** when the body touches a gate outside its gap, goes above the top of
  the world, or reaches the ground. It ends at the **finish** when the body's left edge passes the
  finish line.
- A treat is **caught** when the body touches its circle. A caught treat stays caught for the run.
- A gate is **passed** when the body's left edge is past its right edge.

### Playing

- Before the first run, the screen shows "Tap to start", the number of runs left, and the course.
  The trip name, dates, and place aren't shown while you play.
- The first tap starts a run, and it's also the first flap, on tick 1. Each tap after it is a flap
  on the next tick. Two taps within one tick are one flap.
- A tap is a press on the game, or Space or Enter while it has focus. The game is a button named
  "Flap", so a keyboard can play it.
- A caught treat shows its photo for a moment, in the corner. A status line, read by VoiceOver,
  says "Treat 3 of 8 caught", "Crashed at gate 4", or "Finished".
- You have **3 runs**. The day ends when a run reaches the finish or when the 3 runs are used.
- The game fits the screen: the world keeps its 4:7 shape and is as large as fits, with no page
  scroll. A press on it doesn't zoom or select text.

### The end

- "Finished on run 2, with 6 of 8 treats." or "No finish today. Your best run caught 5 treats."
- The caught photos show in a row of thumbnails.
- The trip shows as in Slide: the trip name, which links to the trip page, then its date range and
  its main place.
- Share and the stats show, with the "Tries to win" bars for 1 to 3.

### The server

- `GET /api/games/fetch/puzzle?date=YYYY-MM-DD` returns the puzzle number, the most runs, the
  course, and, when the day is played, its result. The course has the physics numbers, the gates
  (left edge, gap top, gap bottom, and trip day), the treats (center and photo ID), and the finish
  line. It sends no trip name, date, place, or coordinates. With no course, it returns
  `tripsNeeded: 1` and no course.
- `POST /api/games/fetch/check` with a date and one run's taps returns its grade: how it ended,
  the tick it ended on, the gates passed, and the treats caught. It stores nothing.
- `POST /api/games/fetch/results` with a date and the taps of every run grades them. There must be
  1 to 3 runs, and no run may follow a finished one. The game must end with a finish or with 3 runs.
  Each run is at most 300 taps, each a tick of 2 or more, in increasing order. Otherwise the answer
  is 400. Taps after the run ended are dropped.
- A result is stored in `game_result` as solved (a run finished) or not, the runs used, and 3
  tries allowed. Its marks are the taps, so the server can grade them again: each run's ticks
  joined by ",", and runs joined by ";", such as "14,40,77;9,31". No new table is needed.
- The result has the grade of each run, the most treats caught in one run, the caught photo IDs of
  that run, the share text, the trip (ID, name, start and end dates, place), and the stats.
- The share text reads like `Fibotrail Fetch #9 2/3 🦴 6/8`: the runs used (X when no run
  finished) and the most treats. Then one line per run: 🦴 for a caught treat and ⬛ for a missed
  one, up to where the run ended, then 💥 for a crash or 🏁 for the finish. It holds no trip name,
  place, date, or photo.

### Removing Fetch

Fetch touches nothing outside its own files except one line in each registry and one test's list
of game IDs. To remove it:

1. Delete `apps/server/src/Fibotrail.Games/Fetch/`,
   `apps/server/src/Fibotrail.Server/Games/Fetch/`,
   `apps/server/tests/Fibotrail.Games.Tests/Fetch/`, and
   `apps/server/tests/Fibotrail.Server.Tests/FetchGameTests.cs`.
2. Delete `apps/web/src/games/fetch/`.
3. Remove `FetchRoutes.Definition` from `GameRegistry` in `GameRoutes.cs`, the `fetch` entry from
   `apps/web/src/games/registry.tsx`, and `"fetch"` from the list in `GamesTests.cs`.
4. Regenerate the OpenAPI document and the TypeScript types, as
   [web-app-development.md](../knowledge/web-app-development.md) says.
5. Delete this spec, and its lines in the [specs index](README.md), the [games spec](games.md), and
   [status](../status.md).

Stored results stay in `game_result` under the ID `fetch`, and nothing reads them. No migration is
needed.

## Acceptance criteria

- The same date and library give the same course.
- A photo with no location is never used. With no such photo, the game says so.
- The northernmost picked photo has the highest gap, and no gap moves more than 150 px from the one
  before it.
- The puzzle route sends no trip name, date, place, or coordinates.
- The C# and TypeScript simulations give the same end, tick, gates, and treats for the same course
  and taps. Both test suites hold the same cases.
- Runs with taps out of order, too many taps or runs, or a run after a finish are refused with
  400, and nothing is stored. A second result for the same day keeps the first.
- At the end, the trip shows with its name, dates, and place.
- The share text has bones, squares, a crash, or a flag only.
- Removing the game takes only the steps in [Removing Fetch](#removing-fetch).

## Open questions

- Is a 250 px gap right? Floppy Schnauzer uses 270 px with no treats. Revisit after a week of play.
- Should Reduce Motion slow the game? A slower world changes the physics, so for now the game is
  the same speed for everyone.
- Does the treat thumbnail load fast enough on a slow network? The course works without it: a
  treat with no photo yet is drawn as a gold circle.
