# Slide game

Tracking issue: [#179](https://github.com/bradreimer/fibotrail/issues/179).

## Purpose

Slide is the second game on the [Games tab](games.md). It cuts one of your trip photos into a 3 by
3 grid, removes the bottom-right tile, and shuffles the other 8. You slide tiles into the empty
space until the photo is whole. Then the photo shows in full, with the trip's name, dates, and
place, so the game ends on a memory of the trip.

Think of the wooden sliding puzzle from a toy box, with your own photo glued on.

```text
 Start (shuffled)        You slide              Solved
 ┌───┬───┬───┐          ┌───┬───┬───┐          ┌───┬───┬───┐
 │ 4 │ 1 │ 3 │          │ 1 │ 2 │ 3 │          │           │
 ├───┼───┼───┤   ──▶    ├───┼───┼───┤   ──▶    │  whole    │  Banff, July 2025
 │ 7 │ 2 │ 5 │          │ 4 │ 5 │ 6 │          │  photo    │  Jul 3–9, 2025 · Banff
 ├───┼───┼───┤          ├───┼───┼───┤          │           │
 │ 8 │ 6 │   │          │ 7 │ 8 │   │          └───────────┘
 └───┴───┴───┘          └───┴───┴───┘
```

## User story

As a person with a library of trips, I want to slide the tiles of one of my vacation photos back
into place each day, so that I have a calm puzzle that ends with a look back at a trip.

## Goals

- One puzzle a day, the same on every device.
- Tiles feel like physical pieces: they follow the finger or mouse, and click into place.
- Works with one thumb on a phone, a mouse on a computer, and a keyboard or VoiceOver.
- No trip name, date, or place shows until the photo is whole.

## Non-goals

- Grids other than 3 by 3, a timer, a hint, or an undo.
- A way to lose. A sliding puzzle always ends solved, so there's no try limit.
- Keeping a half-played board when you leave the screen. You start over from the day's start.

## Requirements

### The puzzle

- Slide needs **1** added trip with at least one photo in the
  [game library](games.md#the-game-library-the-games-api). With none, the game says so and links to
  the inbox.
- From the date's seed, the game picks one of those trips, then one of its photos.
- The board has 9 cells, row by row from the top left. Tile n belongs in cell n − 1, and the empty
  cell starts at the bottom right.
- The 8 tiles are shuffled at random. With the empty cell at the bottom right, a 3 by 3 board can be
  solved exactly when the tiles hold an even number of inversions (pairs in the wrong order). An odd
  shuffle swaps its first two tiles, which makes it even. A shuffle that comes out solved rotates
  its first three tiles, which keeps it even. So every start can be solved, and none is solved.
- The photo is cropped to the center square, so every tile is square.

### Playing

- A tile in the same row or column as the empty cell can move. Taking it moves it and every tile
  between it and the empty cell, one cell toward the empty cell. A move list records each tile
  that moved, nearest the empty cell first.
- **Tap**: a press that moves less than 6 px slides the tiles at once.
- **Drag**: the tiles follow the pointer along the one axis they can move, from 0 to one cell, with
  4% of a cell of give past each end at a quarter of the pointer's speed. On release, they snap into
  place when they've moved more than 40% of a cell, or when the last movement was faster than
  0.35 px per ms toward the empty cell (a flick). Otherwise they spring back. Faster than 0.35 px
  per ms away from the empty cell always springs back.
- Dragging uses pointer events, so touch and mouse work the same. The board takes every touch
  (`touch-action: none`), so a drag never scrolls or zooms the page.
- **Feel.** While dragged, a tile has no transition, so it stays under the finger. On release, it
  moves to its cell over 233 ms with a slight overshoot (`cubic-bezier(0.25, 1.35, 0.5, 1)`), which
  reads as a piece clicking into its slot. A pressed tile scales to 97% with a deeper shadow, as if
  lifted. Where the browser has `navigator.vibrate`, a move gives an 8 ms pulse. Safari on iOS has
  no vibration, so on an iPhone the feel comes from motion alone. With reduce motion on, the
  durations are 0 and tiles jump.
- Tiles move with a CSS transform, not layout, so a slide runs on the GPU.
- **Keyboard and VoiceOver.** Each tile is a button named by its number and cell, such as "Tile 5,
  row 2, column 3". Enter, Space, or a VoiceOver double tap moves it. The arrow keys move the tile
  next to the empty cell in the arrow's direction, and focus follows the moved tile. A tile that
  can't move is `aria-disabled`.
- The move count shows under the board.
- When the board is solved, the missing tile fades in and the seams close, so the photo is whole.
  After 900 ms, the browser sends the moves.

### The end

- The whole photo shows, with a caption: the trip name, which links to the trip page, then its date
  range and its main place, such as "Jul 3–9, 2025 · Banff, Alberta". A trip with no known place
  shows the dates alone.
- Tapping the photo opens it in Immich, in a new tab, at the `IMMICH_EXTERNAL_URL` address, and a
  line says "Tap the photo to open it in Immich." With that address empty, the photo isn't a link
  ([#181](https://github.com/bradreimer/fibotrail/issues/181)).
- "Solved in 42 moves.", Share, and the stats show too. Slide is always one try, so the stats show
  no "Tries to win" bars.

### The server

- `GET /api/games/slide/puzzle?date=YYYY-MM-DD` returns the puzzle number, the photo ID, the start
  board, and, when the day is played, its result. It sends no trip name, date, or place. With no
  puzzle, it returns `tripsNeeded: 1`, no photo, and no tiles.
- `POST /api/games/slide/results` with a date and the moves replays them from the start. Each move
  must slide a tile next to the empty cell, no move may follow a solved board, the board must end
  solved, and there are at most 5,000 moves, or the answer is 400. The browser never sends a count.
- A result is stored as solved, 1 try of 1. Its marks are the moves, one digit per tile, such as
  "56", so the move count is their length. No new table is needed.
- The result has the move count, the share text, the photo's trip (ID, name, start and end dates,
  place), and the stats.
- The share text reads like `Fibotrail Slide #5 42 moves`. It holds no trip name, place, date, or
  photo.

## Acceptance criteria

- The same date and library give the same photo and the same start.
- Every start can be solved, isn't solved, and has the empty cell at the bottom right.
- A tile moves only when it's in line with the empty cell, and a tap moves it at once.
- A drag past 40% of a cell, or a flick, snaps the tile into place; a short drag springs back.
- Arrow keys and Enter move tiles.
- While you play, no trip name, date, or place shows, and the puzzle route sends none.
- A move list that doesn't end solved is refused with 400, and nothing is stored.
- A second result for the same day keeps the first.
- At the end, the whole photo shows with the trip name, dates, and place, and links to the trip.
- At the end, tapping the photo opens it in Immich, when `IMMICH_EXTERNAL_URL` is set.

## Open questions

- Is 3 by 3 too easy to keep coming back to? A random 3 by 3 start takes about 22 moves at best.
  Revisit after a few weeks of play; a 4 by 4 day of the week is one option.
- Do the snap distance (40%) and flick speed (0.35 px per ms) feel right on a real iPhone? They're
  checked in Chromium with touch emulation, not yet on a phone.
