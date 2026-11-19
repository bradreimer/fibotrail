# Timeline game

Tracking issue: [#166](https://github.com/bradreimer/fibotrail/issues/166).

Dragging, bigger tiles, and hiding trip names until the end came in
[#170](https://github.com/bradreimer/fibotrail/issues/170).

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

- Hints of any kind while you play: no trip name, place, date, or number. The photo is the only
  hint, because a trip name such as "Orlando, March 2024" often gives the order away.
- Choosing how many cards or tries.

## Requirements

### The puzzle

- A puzzle has **5 cards**. Each card is one photo from a different added trip.
- Only trips with at least one photo in the [game library](games.md#the-game-library-the-games-api)
  can be picked. With fewer than 5 such trips, there's no puzzle, and the game says how many more
  trips it needs.
- From the date's seed, the game picks 5 of those trips, then one photo of each.
- The cards are shown in a shuffled order that's never already the answer.
- The answer is the cards sorted by the local time of their photo. Trips don't overlap, so no two
  cards share a time.

### Playing

- You have **4 tries**. Check sends the order.
- While you play, a card shows only its photo and, after a check, its mark. It shows no trip name
  and no number, because the order on screen already says where it is.
- A card is a large photo: the full width of a phone, at 3:2, and no wider than about 500 px on a
  wide screen. Five cards are taller than a phone screen, so the page scrolls.
- You drag a card to a new place. The other cards shift to make room, so a drop moves the card
  there, and doesn't swap it. Dragging uses pointer events, so touch and mouse work the same.
- On a touch screen, you press and hold a card for 250 ms to lift it, as on an iOS home screen. A
  finger that moves more than 8 px first is a swipe, and scrolls the page. With a mouse, the drag
  starts at once.
- While a card is lifted, the page doesn't scroll under the finger. Near the top or bottom 72 px of
  the scroll area, it scrolls on its own, faster closer to the edge.
- For a keyboard or VoiceOver, each card also has Move up and Move down buttons. They're hidden
  until focused, and named by place, such as "Move card 2 up".
- After each try, each position gets a mark:
  - **Right** 🟩: the card is in its place.
  - **Close** 🟨: the card is one place from its place.
  - **Wrong** ⬜: the card is two or more places away.
- A card marked Right stays in place and can't be dragged or moved. No card can land in its place;
  the other cards flow around it.
- The game ends when every card is Right (solved), or after the fourth try (not solved).
- At the end, solved or not, the cards show in the right order with each trip's name and the date.
  Each card links to its trip page. The stats and Share show too.

### The server

- `GET /api/games/timeline/puzzle?date=YYYY-MM-DD` returns the puzzle number, the cards in shown
  order with only their photo IDs (no trip name, place, or date), the tries allowed, and, when that
  day is played, its result.
- `POST /api/games/timeline/check` with a date and an order of photo IDs returns the marks.
- `POST /api/games/timeline/results` with a date and every try returns the graded result: solved,
  tries, the marks of each try, the share text, the answer with each card's trip ID, trip name, and
  date, and the stats. Each try must
  hold every card once, no try may follow a solved one, and the game must be over (solved, or 4
  tries), or the answer is 400.
- With no puzzle for the date, the puzzle route answers with no cards and the number of trips still
  needed.

## Acceptance criteria

- The same date and library give the same 5 cards in the same shown order.
- While you play, no trip name or number shows, and the puzzle route sends no trip name.
- Dragging a card drops it in the new place, and locked cards don't move.
- At the end, each card shows its trip name and date and opens its trip page.
- A puzzle needs 5 trips with photos, and says how many more are needed otherwise.
- Marks follow the Right, Close, and Wrong rules above.
- A solved puzzle on the second try is stored as solved in 2, and shows in the stats.
- The share text reads like `Fibotrail Timeline #1 2/4`, then one line of marks per try.

## Open questions

- Is 5 cards and 4 tries the right difficulty? Revisit after a week of play.
