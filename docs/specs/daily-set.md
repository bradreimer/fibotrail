# Daily set

Tracking issue: [#194](https://github.com/bradreimer/fibotrail/issues/194).

## Purpose

The Games tab has five daily games, and each one stands alone. The Games screen doesn't show how
much of today you've played, sharing a full day takes five messages, and each game ends
differently, so you go back to the tab to start the next one. This spec makes the day's games feel
like one set: a progress ring on the Games screen, one "Share today" message, and a shared end
screen with a link to the next game.

Think of a newspaper's puzzle page: one page, a few puzzles, and you can see at a glance which ones
you've filled in.

```text
 Games tab                       End of any game
 ┌─────────────────────────┐      ┌─────────────────────────┐
 │  ◔ 3 of 5 done today    │      │ Solved in 2:41          │
 │  [Share today]          │      │ [photos]                │
 │  Timeline ✓  Slide ✓    │ ──▶  │ [Share]  Stats          │
 │  Pin ✓  Fetch ·  Stamps │      │ [Next game: Fetch →]    │
 └─────────────────────────┘      └─────────────────────────┘
```

It's built in three stories:

| Part | Story | Server change |
| --- | --- | --- |
| [The progress ring](#the-progress-ring) | [#195](https://github.com/bradreimer/fibotrail/issues/195) | None |
| [Share today](#share-today) | [#196](https://github.com/bradreimer/fibotrail/issues/196) | A `share` column, and a `share` field in the games list |
| [The shared end screen](#the-shared-end-screen) | [#197](https://github.com/bradreimer/fibotrail/issues/197) | None |

## User story

As a person who plays the daily games, I want to see today's progress, share the whole day at once,
and go straight from one game to the next, so that the day's games feel like one short session.

## Goals

- At a glance, the Games screen says how many of today's games are played.
- One tap shares every game played today, in one message.
- Every game ends the same way, and one tap opens the next unplayed game.
- Adding or removing a game still touches only its own folders and one registry line on each side
  ([ADR-022](../adr/ADR-022-games-are-plug-in-modules-behind-a-games-api.md)).

## Non-goals

- Confetti, sounds, or a reward for finishing every game. They clash with the calm tone of the
  games.
- A streak across all games, or a shared stats page. Each game keeps its own stats.
- A morning notification. Web push works only after the app is added to the home screen, so it
  needs its own request.
- "On this day" lines after a game. They need new facts in the game library.
- Rebuilding share texts for results stored before this change.

## Requirements

### Which games count

- "Today's games" are the games that both the server's `GameRegistry` and the web registry list,
  as for the cards on the [Games screen](games.md#screens). Their order is the Games screen order.
- A game is **played today** when `GET /api/games` says `playedToday`, solved or not. A lost game
  (out of tries) counts as played, because the day is over for it.

### The progress ring

- At the top of the Games screen, above the cards, a ring shows the share of today's games played.
- Next to the ring, visible text says the same thing: "0 of 5 done today", "3 of 5 done today".
  When every game is played, the text is "All done for today ✨".
- The ring is drawn in SVG and is `aria-hidden`, because the text already says it.
- The ring is `--fibo-teal` on a `--fibo-mist` track, so it follows light and dark mode.
- When the screen opens, the ring fills from 0 over `--fibo-dur-signature` (610 ms). With reduce
  motion on, that duration is 0, so it shows full at once.
- It needs no server change: `GET /api/games` already returns `playedToday`.

### Share today

- **Stored share text.** `game_result` gains a nullable `share` column, added by a migration. When
  a game stores a result, it also stores the share text it already builds for that result. Rows
  from before the migration keep an empty value.
- **Why it's stored.** Some share texts can't be rebuilt from a stored row alone. Timeline's twist
  falls back to Classic when the library can't support it, and Fetch's text needs the day's puzzle.
  Storing the text when the result is graded is simple and always matches what the game showed.
- **The games list.** Each entry of `GET /api/games` adds `share`: today's stored share text, or
  null when today isn't played or the row has no text.
- **The button.** "Share today" shows under the ring when at least one game has a share text. It
  uses the same share sheet or clipboard as each game's own Share button, and its label changes to
  "Copied" or "Couldn't share. Try again." in the same way.
- **The text.** One summary line, then each game's share text in Games screen order, with a blank
  line between games:

  ```text
  Fibotrail #10 · 3 of 5 games

  Fibotrail Timeline #10 3/4
  🟨🟩⬜🟩🟩
  🟩🟩🟩🟩🟩

  Fibotrail Slide #10 42 moves

  Fibotrail Stamps #10 Sat 8×8 4:12 💡0
  ```

- The puzzle number is the same for every game on a day, so the summary line uses it once.
- Like every share text, it holds no trip name, place, date, or photo.

### The shared end screen

- Every game's end screen keeps its own verdict and photos at the top, then shows, in this order:
  Share, the stats panel, and the **next-game link**.
- A shared component, `apps/web/src/games/GameEnd.tsx`, draws those three. Each game passes it the
  share text and the stats, and drops its own Share button, stats panel, and closing line, such as
  "A new puzzle comes tomorrow." Games import this shared component, never each other, which keeps
  to ADR-022.
- **Next game.** Once the result shows, `GameEnd` reads `GET /api/games?date=` and links to the
  first of today's games that isn't played and isn't the current game: "Next game: Pin →".
- When every game is played, or the games list can't be read, the link is "Back to Games" and opens
  `/games`.
- The link is a normal link to `/games/<id>`, so the back button returns to the Games tab through
  the browser's own history.

## Acceptance criteria

- The Games screen says "0 of 5 done today", "3 of 5 done today", or "All done for today ✨", and a
  game only one registry lists doesn't count.
- A lost game counts as played.
- After a game is played, `GET /api/games` returns its share text for today, and it matches what
  the game showed.
- "Share today" joins the texts in Games screen order under one summary line, and isn't shown when
  no game is played today.
- The combined text has no trip name, place, date, or photo.
- All five games end with the verdict, the photos, Share, the stats, then the next-game link.
- Next game skips the current game and played games, and reads "Back to Games" when none are left
  or the games list fails.
- Removing a game's line from both registries removes it from the ring, Share today, and Next game,
  and no other test fails.

## Open questions

- Should "Share today" follow the Games screen order, or the order you played in? This spec uses
  the Games screen order, because it's the same every day and needs no new data. Revisit if it
  reads oddly in practice.
- Is "3 of 5 done today" the right words? "Played" is more exact for a lost game, but "done" reads
  better. Revisit with real use.
