# App shell

Tracking issue: [#6](https://github.com/bradreimer/fibotrail/issues/6).

## Purpose

This spec defines the frame that every Fibotrail screen sits in: installation to the home screen,
navigation, the design tokens, and the four screen states. It's where the "native iOS feel"
guideline is met once, so each screen doesn't have to meet it again.

It covers story U10 in the [roadmap](../roadmap.md#mvp-version-01), MVP step 2a.

## User story

As a person on the couch with a phone or iPad, I want Fibotrail to open from my home screen and
behave like an app, so that browsing my trips feels as comfortable as a native iOS app.

## Goals

- On an iPhone, the app meets every row of the table in
  [ADR-007](../adr/ADR-007-native-ios-feel-through-a-progressive-web-app.md).
- Every screen uses the same tokens, navigation, and states, so a new screen is quick to build.
- The browser talks only to Fibotrail.

## Non-goals

- The content of the Inbox, Trips, and trip page screens. Each has its own spec.
- The Map, Collections, and Stats screens.
- Signature animations: the route that draws itself and numbers that count up.
- Offline use. The service worker caches the app shell, not your trips or photos.

## Requirements

### Project

- The UI is a TypeScript, React, and Vite project in `apps/web`, per
  [ADR-004](../adr/ADR-004-technology-stack.md). The server serves the built files.
- The TypeScript types for the API are generated from the OpenAPI document that the server
  produces. The document is kept in `apps/web/src/api/openapi.json`. A server test fails when it
  differs from what the server produces, and a UI check fails when the types differ from it.
- Every address that isn't a file, an API route, or a sign-in route gets the page of the web app,
  and needs a session like any other route.
- The setup and sign-in screens aren't part of the web app. The server sends them as plain HTML,
  per the [first run and sign-in spec](first-run-and-sign-in.md), so they work before a session
  exists and without script.

### Installation

- The app has a web app manifest with the name "Fibotrail", standalone display mode, the brand
  colors, and icons for the iPhone and iPad home screens.
- iOS shows a launch image in the brand colors while the app opens. Each iPhone and iPad screen
  size needs its own image, and `apps/web/splash-screens.json` lists the sizes that have one. A
  device that isn't listed opens with a blank screen for a moment.
- A service worker caches the app shell, so the first screen appears without waiting for the
  network. It never caches an API answer, a photo, or the sign-in screen. Files with a hash in
  their names are kept by the browser for a year, and the page itself is checked on each visit.
- In Safari on an iPhone or iPad, when the app isn't installed, a hint explains **Add to Home
  Screen**. It shows once, and you can dismiss it.

### Navigation

| Width | Navigation | Layout |
| --- | --- | --- |
| Under 600 px | Bottom tab bar | One column |
| 600 to 1023 px | Left sidebar that collapses to icons | Two columns where a screen defines them |
| 1024 px and up | Left sidebar with labels | List on the left, detail on the right |

- The MVP destinations are Trips, Inbox, and Settings, per ADR-007. The four destinations of the
  design document (Trips, Map, Collections, and Stats) replace them when the Map screen ships, and
  Inbox and Settings move to the top bar. The [life map spec](life-map.md) says what changes in
  the first release: the tab bar holds Trips and Map until the other two screens exist, Inbox and
  Settings are icon buttons in the top bar under 600 px, and the sidebar from 600 px holds all four.
  Collections ([collections.md](collections.md)) is the third tab and the Stats screen
  ([stats-screen.md](stats-screen.md)) the fourth, so the tab bar holds the four destinations of the
  design document, and the sidebar holds those four plus Inbox and Settings.
- The active item is Trail Teal with a Golden Gold marker. Each item has an icon and a label.
- Going back returns to the previous screen at the scroll position you left.
- Every detail page opened from a list starts with a back link: a back arrow and the name of its
  parent, such as **Disney parks** on a resort page. The `BackLink` component draws it. It goes
  to the parent route, has a tap target at least 44 px high, and hides the arrow from a screen
  reader. The six screens in the bottom bar and the top bar have no parent, so they have none.
  Tracking issue: [#131](https://github.com/bradreimer/fibotrail/issues/131).

| Page | Back link |
| --- | --- |
| Trip | Trips |
| Collection | Collections |
| Resort | The collection's name |
| Stamp book | The collection's name |
| Continent | Stats |
| Country | Its continent's name |
- The MVP Settings screen shows the Immich connection status, the home location, the **Scan now**
  button, and **Sign out**. Other specs define each of these, and each appears on the screen when
  its spec is built. **Sign out** is there from the start.
- The header is Deep Ocean in light and dark mode, so the white status bar text of an installed
  app stays readable.

### Look

- The design tokens from the
  [design and branding document](../knowledge/design-and-branding.md#design-tokens) live in one
  CSS file, `apps/web/src/styles/tokens.css`. Colors, spacing, radius, type sizes, and durations
  come only from tokens. A test fails when another style sheet holds a color or a length of its
  own.
- The app follows the system light and dark setting by swapping token values.
- Fraunces and Inter are served from the container.
- Screen changes take 377 ms, and small transitions take 233 ms. With the system reduce-motion
  setting on, the app shows the final state with no animation.

### iOS behavior

- Content respects the safe areas at every edge.
- Tap targets are at least 44 by 44 points, with no gray tap highlight.
- Only content areas scroll. The page doesn't bounce or pull to refresh, and the header and tab
  bar stay fixed.
- Inputs use a font size of at least 16 px, so the page doesn't zoom on focus.
- Text in controls isn't selectable.

### Screen states

Shared components provide the four states, and every screen uses them.

| State | What you see |
| --- | --- |
| Loading | Gray skeleton shapes in the final layout |
| Scanning | A slim banner under the header with progress. The app stays usable |
| Empty | A message and one next step |
| Error | A plain explanation and a button to retry or open Settings |

### Privacy and accessibility

- A Content Security Policy allows scripts, styles, fonts, and images from Fibotrail only.
- Text and key controls meet WCAG 2.2 AA contrast with the palette in the design and branding
  document.
- The whole shell works with a keyboard and a screen reader, and honors the system text size.

## Acceptance criteria

- On an iPhone, the app installs with **Add to Home Screen** and opens with no browser bars.
- A run-through on a real iPhone passes every row of the ADR-007 table.
- At 599 px the tab bar shows, and at 600 px the sidebar shows.
- Switching the system between light and dark changes the app without a reload.
- With reduce motion on, no animation plays.
- The browser's network log shows no request to a host other than Fibotrail.
- `./scripts/check.sh` runs the UI steps from the [quality gates spec](quality-gates.md) and
  passes.

## Open questions

None.
