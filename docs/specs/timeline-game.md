# Timeline game

Tracking issue: [#166](https://github.com/bradreimer/fibotrail/issues/166).

Dragging, bigger tiles, and hiding trip names until the end came in
[#170](https://github.com/bradreimer/fibotrail/issues/170). A twist for each day of the week came in
[#177](https://github.com/bradreimer/fibotrail/issues/177).

## Purpose

Timeline is the first game on the [Games tab](games.md). It shows five photos from five of your
trips, and you put them in the order you took them. It tests how well you remember your own travel.
Each day of the week has a [twist](#daily-twists) that changes the photos or the marks, so the game
doesn't play the same way every day.

## User story

As a person with a library of trips, I want to put five of my own photos in date order each day,
so that I remember my trips and can share how I did.

## Goals

- One puzzle a day, the same on every device.
- A game takes under a minute, and works with one thumb on a phone.
- The marks after each try help you without giving the answer away.
- Each weekday plays a little differently, and you still win most days.

## Non-goals

- Hints of any kind while you play: no trip name, place, date, or number. The photo is the only
  hint, because a trip name such as "Orlando, March 2024" often gives the order away.
- Choosing the twist, the cards, or the tries. The weekday decides.

## Requirements

### The puzzle

- A puzzle has **5 cards**, or 6 with the [Six cards](#daily-twists) twist. Each card is one photo
  from a different added trip, except with the One trip twist.
- Only trips with at least one photo in the [game library](games.md#the-game-library-the-games-api)
  can be picked. With fewer than 5 such trips, there's no puzzle, and the game says how many more
  trips it needs.
- From the date's seed, the game picks 5 of those trips, then one photo of each. Some twists pick
  differently.
- The cards are shown in a shuffled order that's never already the answer.
- The answer is the cards sorted by the local time of their photo. Trips don't overlap, so no two
  cards share a time.

### Playing

- You have **4 tries**, or 6 with the Count only twist. Check sends the order.
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
- The Arrows and Count only twists change these marks. See [Daily twists](#daily-twists).
- The game ends when every card is Right (solved), or after the last try (not solved).
- At the end, solved or not, the cards show in the right order with each trip's name and the date.
  Each card's name and date link to its trip page. Its photo opens that photo in Immich, in a new
  tab, at the `IMMICH_EXTERNAL_URL` address, and a line says "Tap a photo to open it in Immich."
  With that address empty, the photo links to the trip page instead
  ([#181](https://github.com/bradreimer/fibotrail/issues/181)). The stats and Share show too.

### Daily twists

The puzzle date's weekday picks the twist. The date is the browser's own, as for the puzzle.

| Day | Twist | What changes | Cards | Tries |
| --- | --- | --- | --- | --- |
| Monday | Classic | Nothing. The game as described above. | 5 | 4 |
| Tuesday | Arrows | A card that isn't Right says **Earlier** ⬆️ or **Later** ⬇️, by which way its place is. There's no Close or Wrong. | 5 | 4 |
| Wednesday | Neighbors | The trips are 5 in a row by start date, from a random place in the list, so their dates are close. | 5 | 4 |
| Thursday | Classic | Nothing. | 5 | 4 |
| Friday | Six cards | 6 trips instead of 5. | 6 | 4 |
| Saturday | One trip | 5 photos from one trip, each taken on a different local day. The trip is picked from those with photos on 5 days or more. | 5 | 4 |
| Sunday | Count only | A check says only how many cards are Right, such as "3 of 5 in place". No card gets a mark, and no card locks. | 5 | 6 |

- **Why it changes.** With Classic marks, the first try usually leaves one or two orders. In a
  simulation of 3,000 games that guess at random among the orders still possible, with no memory of
  the photos, Classic is solved by try 2 in 72% of games and by try 3 in 100%.
- **How hard each one is**, in the same simulation: Arrows 50% by try 2 and 96% by try 3; Six cards
  41% and 99%; Count only 43% by try 4, 82% by try 5, and 97% by try 6. Count only gets 6 tries
  because with 4 it would lose most Sundays. Neighbors and One trip use Classic marks; they make
  the first try harder, which the simulation doesn't measure. Your memory makes every twist easier
  than these numbers.
- **Fallback.** When the library can't support the day's twist, the day is Classic, and the rule
  says why. Six cards needs 6 trips with photos. One trip needs a trip with photos on 5 days.
  Neighbors needs only 5 trips, so it never falls back. Arrows and Count only change only the marks.
- **Count only rows.** A Count only row holds the Right marks first and then Wrong, so the marks
  that are stored and shared don't say which cards were in place.
- **What you see.** Under the title, the twist's name and a one-line rule, such as "Arrows: A photo
  out of place shows whether it goes earlier or later." On Count only, the last count shows above
  the tries, as "Last check: 3 of 5 in place."
- **Stats.** The stats count wins by tries up to 6, the most any twist allows.

### The server

- `GET /api/games/timeline/puzzle?date=YYYY-MM-DD` returns the puzzle number, the twist (its ID,
  the weekday's planned twist, its name, and its rule), the cards in shown order with only their
  photo IDs (no trip name, place, or date), the tries allowed, and, when that day is played, its
  result.
- `POST /api/games/timeline/check` with a date and an order of photo IDs returns the marks.
- `POST /api/games/timeline/results` with a date and every try returns the graded result: solved,
  tries, the marks of each try, the share text, the answer with each card's trip ID, trip name,
  date, and Immich address (null when `IMMICH_EXTERNAL_URL` is empty), and the stats. Each try must
  hold every card once, no try may follow a solved one, and the game must be over (solved, or 4
  tries), or the answer is 400. The share text names the twist after the puzzle number, except
  on Classic.
- With no puzzle for the date, the puzzle route answers with no cards and the number of trips still
  needed.

## Acceptance criteria

- The same date and library give the same 5 cards in the same shown order.
- While you play, no trip name or number shows, and the puzzle route sends no trip name.
- Dragging a card drops it in the new place, and locked cards don't move.
- At the end, each card shows its trip name and date and opens its trip page.
- At the end, tapping a card's photo opens it in Immich, when `IMMICH_EXTERNAL_URL` is set.
- A puzzle needs 5 trips with photos, and says how many more are needed otherwise.
- Marks follow the Right, Close, and Wrong rules above.
- A solved puzzle on the second try is stored as solved in 2, and shows in the stats.
- The share text reads like `Fibotrail Timeline #1 2/4`, then one line of marks per try. With a
  twist, it reads like `Fibotrail Timeline #4 Count only 4/6`.
- Each weekday gives the twist in the table, and the puzzle screen shows its name and rule.
- On an Arrows day, a card out of place says Earlier or Later.
- On a Count only day, no card gets a mark or locks, the check says only how many are in place,
  and there are 6 tries.
- Friday has 6 cards, Saturday has 5 photos of one trip from 5 days, and Wednesday's 5 trips are
  in a row by start date.
- A twist the library can't support plays as Classic, and the rule says why.

## Open questions

- Is each twist the right difficulty? Revisit after a few weeks of play. Count only is the one to
  watch: 6 tries is a guess from the simulation, not from real play.
- Should Neighbors and One trip use Arrows marks, to make the second try harder too? Not until a
  few weeks of play say they're too easy.
