# Stamps game

Tracking issue: [#191](https://github.com/bradreimer/fibotrail/issues/191).

## Purpose

Stamps is the fifth game on the [Games tab](games.md), and its first pure logic puzzle. The board
is an N by N grid split into N colored regions, and each region is one of your trips. You place
one passport stamp in each row, each column, and each region, and no two stamps may touch, even
diagonally. When the board is solved, the stamped cells become a passport page: one photo from
each trip, with its name and dates. It's in the same genre as region-placement puzzles such as
Queens. Timeline asks when and Pin asks where. Stamps asks you to think, and then shows you where
you've been.

Think of filling a passport with one stamp from each trip, where the border guards have strict
rules about where a stamp may go.

```text
 The board (7 × 7, Wednesday)              Solved: the passport page
 ┌───┬───┬───┬───┬───┬───┬───┐            ┌──────────── Passport ────────────┐
 │ A │ A │ B │ B │ B │ C │ C │            │ 🛂 Lisbon      🛂 Kyoto           │
 ├───┼───┼───┼───┼───┼───┼───┤            │    May 2023       Apr 2025        │
 │ A │🛂 │ B │ D │ D │ C │ C │   ──▶      │ 🛂 Yosemite    🛂 Oaxaca          │
 ├───┼───┼───┼───┼───┼───┼───┤            │    Aug 2024       Nov 2022        │
 │ E │ E │ E │ D │ D │ D │🛂 │            │ 🛂 ...                            │
 └───┴───┴───┴───┴───┴───┴───┘            └──── tap a stamp to open its trip ─┘
  A, B, C ... are trips. Each cell shows      Solved in 2:41 · no hints
  a faint photo from its trip.

 Rules: one 🛂 per row, per column, and per trip. No two 🛂 touch, even at a corner.
 Tap once = ✕ (not here). Tap twice = 🛂. Tap again = empty.

 Share:  Fibotrail Stamps #12 Wed 7×7 2:41 💡0
```

## User story

As a person with a library of trips, I want a daily logic puzzle where each colored region is one
of my trips, so that I have a quick brain teaser that ends with one photo from each of those trips.

## Goals

- One board a day, the same on every device, that you can always solve by logic alone.
- It takes about 1 to 5 minutes, and gets a little harder through the week.
- Marking feels quick with one thumb: tap, drag, and undo, with a haptic tap and a stamp animation.
- The regions are easy to tell apart without relying on color alone.
- No trip name, date, or place shows until the board is solved.
- Leaving halfway doesn't lose your board or your time.

## Non-goals

- Regions made from something other than trips, such as countries or months. A library with fewer
  than 5 trips doesn't get a board. See [Open questions](#open-questions).
- A way to lose, or a try limit. Like [Slide](slide-game.md), a board always ends solved.
- Giving up, or showing the solution.
- Playing a past day's board.
- Cross-game features, such as a "today's set" ring on the Games tab or a morning notification.
  Those belong in their own Feature Request.

## Requirements

### The puzzle

- Stamps needs **5** added trips with at least one photo each in the
  [game library](games.md#the-game-library-the-games-api). With fewer, the game says how many more
  trips it needs and links to the inbox.
- **Size by weekday.** The puzzle date's weekday sets the board size N and the label on the card:

  | Day | Size | Label |
  | --- | --- | --- |
  | Monday, Tuesday | 6 × 6 | Easy |
  | Wednesday, Thursday, Friday | 7 × 7 | Medium |
  | Saturday, Sunday | 8 × 8 | Hard |

- **Fallback.** With fewer trips than N, the size is the number of trips, down to 5, and the label
  is the label of that size. The rule line says why, such as "7 × 7 today, because you have 7
  trips."
- **Trips.** From the date's seed, the game picks N trips. Each trip becomes one region.
- **Generator.** Every step uses the date's seed, so the same date and library give the same board.
  1. Place N stamps: one per row and one per column, where the stamps in rows next to each other
     are at least 2 columns apart. This is a random permutation, retried until no two stamps touch.
  2. Grow the regions. Each stamp seeds one region. Repeatedly, a random region takes a random
     empty cell next to it (up, down, left, or right), until every cell has a region. So every
     region is connected.
  3. **Repair.** Random regions almost never have one solution: in a sample of 3,000 boards,
     19 had one at 6 × 6, 1 at 7 × 7, and none at 8 × 8. So while the uniqueness solver finds
     another solution, one of that solution's cells that isn't a planned stamp moves into a region
     next to it, if its own region stays connected. The other solution then has two stamps in one
     region, so it breaks, and the planned one still holds. A repair stops after 4 moves per cell.
  4. Keep the board only when the repair ends with exactly one solution, and the **logic solver**
     solves it with no guessing. Otherwise, go back to step 1 with the next seed step, for up to
     2,000 attempts.
  5. If every attempt fails, use the smaller size, down to 5. A day with no board at 5 shows "No
     board today. Try again tomorrow." The tests check that this doesn't happen for the sizes and
     seeds they cover.
- **Uniqueness solver.** A backtracking search, row by row, that stops at 2 solutions.
- **Logic solver.** It repeats these rules until the board is solved or no rule applies. A board
  it can't finish needs guessing and isn't used.
  1. A row, column, or region with one open cell gets a stamp there.
  2. A stamp rules out the rest of its row, column, and region, and the 8 cells around it.
  3. When all of a region's open cells are in one row (or one column), the other cells of that row
     (or column) are ruled out.
  4. When k regions have all their open cells in the same k rows (or columns), the other cells of
     those rows (or columns) are ruled out. The solver checks k up to 3.
  5. A cell is ruled out when a stamp there would leave some row, column, or region with no open
     cell.
- **Difficulty.** The weekday controls difficulty through size only. Larger boards need more steps
  of the rules above. How long each size takes a person isn't measured yet.
- **Speed.** Over 200 seeded days per size, no size failed. An 8 × 8 board took 10 attempts on
  average and 40 at most, and the slowest took 29 ms. The tests check 365 days per size, with at
  most 200 attempts each.
- **Photos.** Each cell shows a photo from its region's trip. The region's cells go through the
  trip's photos in time order, from a start picked by the seed, and repeat when the trip has fewer
  photos than cells. The stamp cell of each region shows a photo too, which becomes the passport
  photo at the end.

### Playing

- **Tap cycle.** A tap on a cell moves it from empty to ✕, from ✕ to stamp, and from stamp to
  empty.
- **Drag to mark.** A press that moves more than 6 px before release marks ✕ on every empty cell
  the pointer passes over. A drag that starts on a ✕ clears the ✕ marks it passes over. A drag
  never places or removes a stamp.
- **Auto-✕.** When you place a stamp, the cells it rules out (its row, column, region, and the 8
  cells around it) show a faint ✕. These marks are drawn lighter than your own ✕ marks, and they
  go away when the stamp is removed. A switch under the board, "Mark ruled-out cells", turns this
  on or off. It's on by default and remembered on that device.
- **Conflicts.** When two stamps break a rule, both pulse red until one moves. There's no message
  and no penalty.
- **Undo.** An undo button takes back the last tap or drag, as far back as the start. "Clear
  board" asks for confirmation, then removes every mark and stamp. Clearing doesn't reset the
  timer.
- **Peek.** Press and hold a cell for 500 ms without moving to see its photo full size. Releasing
  closes it. The peek shows no trip name, date, or place.
- **Hints.** A hint button asks the server for the next step of the logic solver, from the current
  board. The board marks that one cell: a stamp it must have, or a ✕ it can't have, with the
  rule's one-line reason, such as "Stamp here: Only one open cell is left in this row." A hint on a
  board with a wrong stamp, or a ✕ on a solution cell, points to that mistake instead, starting
  with "Look again:". The hint count shows next to the timer. A hint starts the timer.
- **Timer.** It starts on the first tap or drag, not when the screen opens. It pauses while the page
  is hidden (`visibilitychange`), and shows minutes and seconds.
- **Feel.** A placed stamp scales from 120% to 100% and tilts 3° over 180 ms, and the stamp shows
  the region's letter, not the trip's name or initial. Where the browser has `navigator.vibrate`, a
  stamp gives a 10 ms pulse and a ✕ gives none. With reduce motion on, the stamp appears with no motion.
- **Looks.** The grid shows the region colors first. Photos sit over them at 25% opacity, blended
  by brightness only (`mix-blend-mode: luminosity`), so a region keeps one color and its photos
  show as texture. Borders between regions are 3 px; borders inside a region are 1 px. Each cell
  draws only its top and left line, in whole pixels, and the board draws its outer edge, because a
  browser rounds half-pixel lines to the same width. So the regions can be told apart by their
  borders, not only by color. The 8 region colors are custom properties in the game's own
  stylesheet, with a light and a dark value each, so removing the game removes them.
- **Keyboard and VoiceOver.** Each cell is a button named by its row, column, region letter, and
  state, such as "Row 2, column 3, region C, empty". Enter, Space, or a VoiceOver double tap moves
  it through the tap cycle. The arrow keys move focus between cells. A conflict adds "breaks a rule"
  to the name.
- **Resume.** The board, the marks, the elapsed time, and the hint count are saved on that device
  for that date, in `localStorage`, after every change. Opening Stamps the same day restores them.
  A saved board for another date is removed. When storage isn't available, the game still works
  and starts over each time.
- **Solved.** When every row, column, and region has one stamp and no rule is broken, the timer
  stops. After 600 ms, the browser sends the result.

### The end

- The board fades out and the stamped photos drop into a passport page, one per trip, in the order
  of the trips' start dates. Each shows the trip name, which links to the trip page, and its month
  and year, such as "Apr 2025".
- Tapping a photo opens it in Immich, in a new tab, at the `IMMICH_EXTERNAL_URL` address. With that
  address empty, the photo isn't a link, as in [Slide](slide-game.md#the-end).
- "Solved in 2:41." shows with "no hints" or "1 hint", then Share and the stats.

### The server

- `GET /api/games/stamps/puzzle?date=YYYY-MM-DD` returns the puzzle number, the size, the label,
  the weekday, the regions as N rows of region letters, the photo ID of each cell, and, when the
  day is played, its result. It sends no trip ID, name, date, or place, and not the solution. With
  no puzzle, it returns `tripsNeeded` and no board.
- `POST /api/games/stamps/hint` with a date and the current board (the stamps and the ✕ marks)
  returns the next step of the logic solver: the cell, stamp or ✕, and the rule. It stores nothing.
- `POST /api/games/stamps/results` with a date, the N stamp cells, the elapsed seconds, and the hint
  count grades the stamps: N stamps, one per row, column, and region, none touching. Otherwise the
  answer is 400. The seconds must be from 1 to 86,400, and the hints from 0 to N × N.
- The stamp cells are graded by the server. The **time and hints are reported by the browser**,
  because the timer pauses in the browser and hints are counted there. The server can't check
  them. This is a personal app, so a false time only fools yourself.
- A result is stored as solved, 1 try of 1, with the seconds in the new `seconds` column of
  `game_result` (see [Timed games](games.md#timed-games)). Its marks are the stamp columns, one
  digit per row, then `h` and the hint count, such as "1503624h0".
- The result has the time, the hints, the share text, the stamped photos with each one's trip (ID,
  name, start and end dates, and Immich address), and the stats.
- The share text reads like `Fibotrail Stamps #12 Wed 7×7 2:41 💡0`. It holds no trip name, place,
  date, or photo.

### Stats

- Stamps shows Played, Current streak, and Best streak, as every game does. Since a board always
  ends solved, it shows no Win % and no "Tries to win" bars.
- It also shows **Best time** and **Average time**, each over days with a time. With the board
  size changing by weekday, both are split by size, such as "6 × 6: best 1:12, average 2:05".
- The Stamps card on the Games screen adds the best time over every size, such as
  "· best 1:12". An average over sizes would mix easy and hard days, so the card leaves it out.

## Acceptance criteria

- The same date and library give the same board, size, and photos.
- Every board has exactly one solution, and the logic solver solves it, for every size from 5 to 8
  over 365 seeded days in the tests.
- The size follows the weekday, and falls back to the number of trips when there are fewer.
- With fewer than 5 trips, the game says how many more are needed and links to the inbox.
- Tap moves a cell from empty to ✕ to stamp to empty. A drag marks ✕ and never places a stamp.
- With Auto-✕ on, a stamp marks the cells it rules out, and removing it clears them.
- Two stamps that break a rule both show a conflict.
- Undo takes back the last change, and Clear board asks first.
- The timer starts on the first tap, and pauses while the page is hidden.
- Leaving and opening Stamps again the same day restores the board, the time, and the hints.
- The Stamps card shows the best time, and the stats show the best and average time by size.
- A hint returns a step that follows from the board, and the hint count goes up.
- While you play, no trip name, date, or place shows, and the puzzle route sends none.
- A result with stamps that break a rule is refused with 400, and nothing is stored.
- A second result for the same day keeps the first.
- At the end, the passport page shows each stamped photo with its trip's name and month, linked to
  the trip.
- The share text has no trip name, place, date, or photo.
- Removing Stamps's line from both registries removes its card and route, and no other test fails.

## Open questions

- How long does each size take a person? The size table is a guess. Revisit after a few weeks of
  play, and record solve times per size in [status.md](../status.md).
- Should a library with fewer than 5 trips get regions from countries or months instead? Not until
  it's asked for.
- Do 25% photos keep the regions readable on a real iPhone in bright light? Checked only in
  Chromium with iPhone 13 emulation, light and dark, with stand-in photos.
- Do the drag (6 px) and peek (500 ms) feel right on a real iPhone? Checked only in tests.
