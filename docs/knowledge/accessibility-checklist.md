# Accessibility checklist

Intention: record the accessibility checks that a test can't make, so version 0.1 ships with a
known result for each. The rules come from the
[design and branding document](design-and-branding.md#accessibility) and the
[release spec](../specs/release.md).

Method: run each line on a real iPhone with the app installed to the home screen, using real data.
Write the date, the iOS version, and Pass, Fail, or Known gap in the Result column. A Fail is fixed
before the `v0.1.0` tag. A Known gap names the screen and what's wrong, and gets its own GitHub
issue.

## What tests already check

These run in `./scripts/check.sh`. They don't need a phone.

| Rule | Test | Where |
| --- | --- | --- |
| Text and key controls meet WCAG 2.2 AA contrast: 4.5:1 for text, 3:1 for control edges | Computes the ratio for each text and fill pair in the light and dark tokens, and for Paper text on the photo scrim over a white and a black photo | `apps/web/src/accessibility/contrast.test.ts` |
| The focus ring shows on any background | Checks that the ring is Gold with an Ocean halo, and that the pair is at least 3:1 | `apps/web/src/accessibility/contrast.test.ts` |
| Every image has alt text, and a photo with empty alt text sits inside a named control | Checks the rendered inbox, trip library, trip page, stop sheet, photo viewer, and Settings | `apps/web/src/accessibility/accessibility.ts`, called from the screen tests |
| Every button, link, and input has an accessible name | Same check | Same files |
| Reduce motion turns animation off | Checks that every duration is a token, that the tokens are 0 under reduce motion, and that a repeating animation is switched off by name | `apps/web/src/accessibility/motion.test.ts` |
| Other markup rules, such as labels and roles | The `jsx-a11y` lint rules | `npm run lint` |

The tests can't see layout, so they don't check tab order, focus rings, text size, or touch
targets. The manual lines below do.

## Manual checks

Run each on the five main screens: Inbox, Trips, a trip page, the photo viewer, and Settings.

| # | Check | How | Result |
| --- | --- | --- | --- |
| 1 | VoiceOver reads each screen in a sensible order | Turn on VoiceOver. Swipe right through each screen from the top. The order matches the visual order | |
| 2 | VoiceOver names every control | No control is read as only "button" or "link" | |
| 3 | VoiceOver reads a trip card once | The card reads as the name, dates, and destination, in one swipe | |
| 4 | The photo viewer announces itself | Opening a photo reads "Photo 3 of 6", and focus starts on **Close** | |
| 5 | A dialog traps focus and returns it | In the stop sheet, the add-trip sheet, and the viewer, swipe doesn't leave the dialog. Closing returns focus to where you were | |
| 6 | The largest iOS text size fits | In Settings, Display and Brightness, Text Size, choose the largest. No text is cut off or overlaps, and every button stays tappable | |
| 7 | Bold Text and Larger Accessibility Sizes don't break a screen | Turn on Bold Text, then the Larger Accessibility Sizes. The five screens still work | |
| 8 | Reduce Motion stops the motion | In Settings, Accessibility, Motion, turn on Reduce Motion. Screens appear with no slide, the skeleton doesn't pulse, and the map doesn't fly | |
| 9 | Dark mode keeps text readable | Switch the phone to dark mode. Every screen is readable, including text over photos | |
| 10 | Touch targets are at least 44 by 44 points | Tap each button and tab with a thumb. None needs a second try | |
| 11 | A keyboard reaches everything | On an iPad with a keyboard, or on a desktop browser, Tab reaches every control in a sensible order, and a focus ring always shows | |
| 12 | The map has a text alternative | On a trip page, **Stops in order** lists every stop, and VoiceOver can reach it | |

## Result

Run by the maintainer on an iPhone on 2026-11-16, for version 0.1.0. Overall result: passed, apart
from one finding. The thumbnails in the trip timeline were too wide for their photos
([#41](https://github.com/bradreimer/fibotrail/issues/41)). It was fixed and the maintainer
confirmed the new tile shapes on the phone. The iOS version and the result of each line weren't
written down, so the table above is blank. Fill it in at the next run, for version 0.2.
