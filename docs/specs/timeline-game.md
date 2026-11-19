# Timeline game

Tracking issue: [#166](https://github.com/bradreimer/fibotrail/issues/166).

## Purpose

Timeline is the first game on the [Games tab](games.md). It shows five photos from five of your
trips, and you put them in the order you took them. It tests how well you remember your own travel.

## User story

As a person with a library of trips, I want to put five of my own photos in date order each day,
so that I remember my trips and can share how I did.

## Goals

- One puzzle a day, the same on every device.
- A game takes under a minute, and works with one thumb on a phone.
- The marks after each try help you without giving the answer away.

## Non-goals

- Dragging cards. Move buttons are easier to hit with a thumb, and work with VoiceOver.
- Hints from the trip page, the map, or the dates. The trip name is the only hint.
- Choosing how many cards or tries.

## Requirements

### The puzzle

- A puzzle has **5 cards**. Each card is one photo from a different added trip, with that trip's
  name.
- Only trips with at least one photo in the [game library](games.md#the-game-library-the-games-api)
  can be picked. With fewer than 5 such trips, there's no puzzle, and the game says how many more
  trips it needs.
- From the date's seed, the game picks 5 of those trips, then one photo of each.
- The cards are shown in a shuffled order that's never already the answer.
- The answer is the cards sorted by the local time of their photo. Trips don't overlap, so no two
  cards share a time.

### Playing

- You have **4 tries**. Each card has Move up and Move down buttons. Check sends the order.
- After each try, each position gets a mark:
  - **Right** 🟩: the card is in its place.
  - **Close** 🟨: the card is one place from its place.
  - **Wrong** ⬜: the card is two or more places away.
- A card marked Right stays in place and can't be moved. Other cards move around it.
- The game ends when every card is Right (solved), or after the fourth try (not solved).
- At the end, the cards show in the right order with their dates, and the stats and Share show.

### The server

- `GET /api/games/timeline/puzzle?date=YYYY-MM-DD` returns the puzzle number, the cards in shown
  order with no dates, the tries allowed, and, when that day is played, its result.
- `POST /api/games/timeline/check` with a date and an order of photo IDs returns the marks.
- `POST /api/games/timeline/results` with a date and every try returns the graded result: solved,
  tries, the marks of each try, the share text, the answer with dates, and the stats. Each try must
  hold every card once, no try may follow a solved one, and the game must be over (solved, or 4
  tries), or the answer is 400.
- With no puzzle for the date, the puzzle route answers with no cards and the number of trips still
  needed.

## Acceptance criteria

- The same date and library give the same 5 cards in the same shown order.
- A puzzle needs 5 trips with photos, and says how many more are needed otherwise.
- Marks follow the Right, Close, and Wrong rules above.
- A solved puzzle on the second try is stored as solved in 2, and shows in the stats.
- The share text reads like `Fibotrail Timeline #1 2/4`, then one line of marks per try.

## Open questions

- Is 5 cards and 4 tries the right difficulty? Revisit after a week of play.
