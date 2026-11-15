# ADR-007: Native iOS feel through a progressive web app

## Status

Accepted

## Context

The maintainer wants Fibotrail on a phone to feel like a native iOS app. The design and branding
document rules out native iOS and Android apps for version 1, and the UI is a TypeScript web app
(see [ADR-004](ADR-004-technology-stack.md)).

A web app in Safari doesn't feel native by default: it shows browser bars, bounces as a page,
highlights taps in gray, and ignores the notch and the home indicator.

## Decision

The UI is a progressive web app, installed from Safari with **Add to Home Screen**, and built to
meet the following behaviors on an iPhone.

| Area | Behavior |
| --- | --- |
| Launch | Opens from a home screen icon in standalone mode, with no browser bars, and shows a splash screen in the brand colors |
| Screen edges | Content respects the safe areas, so nothing sits under the notch, the Dynamic Island, or the home indicator |
| Navigation | A bottom tab bar with four tabs, and a back gesture that returns to the previous screen with its scroll position kept |
| Touch | Tap targets are at least 44 by 44 points, with no gray tap highlight and no 300 ms delay |
| Scrolling | Only content areas scroll. The page itself doesn't bounce or pull to refresh, and headers and the tab bar stay fixed |
| Text | Text isn't selectable in controls, and inputs use a font size of at least 16 px so the page doesn't zoom on focus |
| Motion | Screen changes use the durations in the design document, and respect the reduce-motion setting |
| Appearance | Follows the system light and dark setting, and the status bar matches the screen behind it |
| Speed | The app shell loads from a service worker cache, so the first screen appears without waiting for the network |

The check for this decision is a manual run-through on a real iPhone, installed to the home
screen, for every work item that changes the UI.

## Alternatives considered

- **A native Swift app.** The best possible feel. Set aside because it needs a second codebase, an
  Apple developer account, and App Store review, for an app with one household of users.
- **A web UI wrapped in a native shell, such as Capacitor.** This adds native APIs, but it still
  needs the App Store or a re-signed sideload every week.
- **A responsive website with no app behaviors.** The least work. Set aside because it fails the
  guideline: it looks like a website.

## Consequences

- Installing takes one manual step in Safari. Fibotrail shows a one-time hint that explains it.
- Some native features aren't available to a web app on iOS, such as haptic feedback and
  background scans on the phone. Scans run on the server, so the second one doesn't matter.
- The app must be served over HTTPS for the service worker to run, which the reverse proxy
  provides.
- iPad and desktop get the same app with a sidebar in place of the tab bar.
