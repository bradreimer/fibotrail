# First scan and replay checklist

Intention: run Fibotrail against the maintainer's real Immich library once, and record what
happens. This closes the open criteria of [#7](https://github.com/bradreimer/fibotrail/issues/7)
and the gate of MVP step 3a, the review gate. It's the last evidence needed before the `v0.1.0`
tag in [#24](https://github.com/bradreimer/fibotrail/issues/24).

Method: five parts, in order. Part 1 prepares, part 2 scans, part 3 proves Immich is only read,
part 4 sets the home location, and part 5 reviews the suggestions it found. Write the result of each line
in the Result column. The scanner has only run against a stand-in for Immich so far, and the
detector only against made-up photos, so expect to find something. A finding goes to a GitHub
issue, not into this file.

## Part 1. Prepare

| # | Step | Result |
| --- | --- | --- |
| 1.1 | In Immich, create an API key with `asset.read`, `asset.view`, `asset.statistics`, and `album.read`, and no other permission. See the [README](../../README.md#api-key-permissions) | |
| 1.2 | Copy `.env.example` to `.env`. Set `IMMICH_API_KEY`, `IMMICH_URL` (the address the container uses, such as `http://immich_server:2283`), and `IMMICH_EXTERNAL_URL` (the address your browser uses) | |
| 1.3 | In `docker/compose.example.yml`, set the network name to the one your Immich containers use. `docker network ls` lists them | |
| 1.4 | Start it: `docker compose -f docker/compose.example.yml up -d`. Until `v0.1.0` is tagged, `latest` is a stale build, so change the image line in the compose file to `ghcr.io/bradreimer/fibotrail:edge` first. To test your working copy instead, run `docker compose -f docker/compose.example.yml up -d --build` | |
| 1.5 | `docker ps` shows `fibotrail` as `healthy` within 1 minute | |
| 1.6 | Open `http://localhost:11235` on the host, set the password, and sign in | |
| 1.7 | In Settings, the Immich line says "Connected to Immich" with the version. Note the version here: Settings warns when it isn't 3.4 | |
| 1.8 | Check the wrong-address message: set `IMMICH_URL` to a bad address, restart, and confirm Settings says "Can't reach Immich". Put the right address back | |

## Part 2. First scan

Record the date, the number of photos in Immich (the total on the Immich server page), and the
time you start.

| # | Step | Expected | Result |
| --- | --- | --- | --- |
| 2.1 | In Settings, choose **Scan now** | The banner reads "Scanning your library: N of TOTAL photos", and N rises | |
| 2.2 | Use the app while it scans: open Trips and Settings | Screens respond, and nothing freezes | |
| 2.3 | Watch the count for a few minutes. Write down photos per minute | Steady progress. This tells you how long the full scan takes | |
| 2.4 | Mid-scan, run `docker restart fibotrail` | The scan restarts by itself and continues from the saved cursor, not from 0 | |
| 2.5 | Wait for the scan to finish | Settings shows the time and result of the last scan | |
| 2.6 | Compare Fibotrail's photo count with Immich's total | They match, except for trashed photos and photos with no capture time, which aren't stored | |
| 2.7 | Choose **Scan now** again with no changes in Immich | It finishes in seconds and the photo count doesn't change | |
| 2.8 | Add one new photo in Immich, then scan | The count rises by 1, and only changed photos are read | |
| 2.9 | Open a trip or the inbox and look at the thumbnails | Photos load through Fibotrail. `/data/thumbnails` starts to fill | |

If a scan fails, copy the message from the banner and the output of `docker logs fibotrail`, and
open a **Bug Report**. A failed scan keeps its progress, so **Try again** continues it.

## Part 3. Prove that Immich is only read

The criterion: the Immich access log for a full scan shows only the four allowed requests.

- `GET /api/server/version`
- `POST /api/search/metadata`
- `GET /api/assets/statistics`
- `GET /api/assets/{id}/thumbnail`

Fibotrail talks to Immich over the Docker network, so a reverse proxy in front of Immich doesn't
see it. Use the method that works for you. Which of them shows requests in your Immich version
isn't known yet, so record what you used.

| Method | How |
| --- | --- |
| Immich's own log | Set `IMMICH_LOG_LEVEL=verbose` on the Immich server, restart it, run a scan, and read `docker logs immich_server`. Check first that an ordinary request from your browser shows up, or an empty result proves nothing |
| A logging proxy in between | Run a small nginx container on the Immich network, with `proxy_pass` to Immich and an access log. Set `IMMICH_URL` to the nginx address, scan, and read the log. This always works |

| # | Step | Expected | Result |
| --- | --- | --- | --- |
| 3.1 | Check that your log method shows a request you make yourself, such as opening Immich in a browser | You see it | |
| 3.2 | Set the log method up before step 2.1, so the first full scan is the one you log. Don't wipe the volume to repeat it: that also deletes your password | The first scan is in the log | |
| 3.3 | List the distinct method and path pairs in the log for the requests from Fibotrail | Only the four above, with asset IDs in place of `{id}` | |
| 3.4 | Look for any `POST`, `PUT`, `PATCH`, or `DELETE` other than `POST /api/search/metadata` | None | |
| 3.5 | `docker logs fibotrail \| grep -c "<first 8 characters of the key>"` | 0 | |
| 3.6 | In the browser developer tools, search the network responses and the page source for the key | Not found | |

## Part 4. Home location

| # | Step | Expected | Result |
| --- | --- | --- | --- |
| 4.1 | Open the Inbox after the scan | A home location step shows a suggestion, by name and on a map | |
| 4.2 | Compare the suggestion with where you live | It's right, or close. If not, note where it was and how far off | |
| 4.3 | Choose **Use this location**, or **Choose another** and search or move the pin | The step says how many trips it found | |
| 4.4 | Check the Inbox | Suggestion cards appear, with a photo strip and route preview | |

## Part 5. The review gate

You don't need a list of trips for this. The inbox is the test: you judge the app's suggestions,
and the app does the remembering. The rules are in the
[review gate](../specs/trip-detection.md#review-gate) of the trip detection spec.

**5a. Accuracy.** Open the Inbox. Take the first 10 High-confidence suggestions, newest first, or
all of them if there are fewer.

| # | Step | Expected | Result |
| --- | --- | --- | --- |
| 5.1 | For each of the 10, decide: would you accept it with **Add trip**? Dates and photos right means yes. A different name or cover doesn't count against it | A yes or no for each | |
| 5.2 | Count the yes answers | At least 8 of 10 (80 percent, when there are fewer than 10) | |
| 5.3 | For each no, write the reason: merged with another trip, split in two, not a trip, or wrong dates | A reason for each | |

**5b. Misses.** Without opening your library, write down 3 to 5 trips you remember.

| # | Step | Expected | Result |
| --- | --- | --- | --- |
| 5.4 | For each, look for a suggestion or a trip whose dates cover its days. Search the Inbox, the Ignored tab, and the library | Found | |
| 5.5 | Count the missing ones | At most 1 | |
| 5.6 | For each missing trip, note why. See the table below | A reason for each | |

Reading a miss or a no:

| What you see | Likely cause | Spec question |
| --- | --- | --- |
| Missing, and the trip has few photos with GPS | Only away photos count toward the 20 minimum, and 84.5 percent of photos have no position | 3 |
| One trip shown as two | A photo at home, from someone else or from you, ended the trip. Or a gap of 72 hours or more | 2 |
| Two trips shown as one | Two trips less than 72 hours apart with no photo at home between them | none, note it |
| Dates off by more than one day | Time zones, or photos with no position joining the wrong trip | none, note it |
| High confidence, but you'd call it Medium or Low, or the reverse | The confidence thresholds | 1 |
| The home suggestion was wrong | A move, a second home, or a long stay somewhere else | 4 |

Names and destinations that aren't right are not a failure of this gate. Note them under
[#31](https://github.com/bradreimer/fibotrail/issues/31), which chooses the main destination.

**If it passes**, write the numbers down: how many of the 10 you accepted, how many remembered
trips you found, and the reasons for each no. Answer the open questions in the spec with what you
saw.

**If it fails**, change one default at a time. The detection settings aren't editable in the app
in version 0.1. The defaults are in `apps/server/src/Fibotrail.Detection/Models.cs`. Rebuild with
`docker compose -f docker/compose.example.yml up -d --build`, scan again, and review again. Record
each try: the value, and what the review showed. Don't change a default to match one trip. If the
trips you miss share a cause, such as no GPS, the fix may be a rule change, not a number. Open a
**Bug Report** for it.

### Optional: the replay command

Use this when you want a number after changing a rule. Pick 5 to 10 trips you know you took.
Choose a mix: a long trip, a weekend, one with few photos, one abroad, one where someone else
added photos at home, and one taken with a camera that records no GPS.

1. Write `known-trips.json`, outside the repository, because it holds private data:

   ```json
   [
     { "name": "Orlando", "start": "2027-03-15", "end": "2027-03-24" },
     { "name": "Okanagan weekend", "start": "2027-07-03", "end": "2027-07-06" }
   ]
   ```

   Dates are `YYYY-MM-DD`, in local time, and `start` and `end` are the first and last days you
   have photos from the trip.

2. Copy it into the data volume:

   ```bash
   docker cp known-trips.json fibotrail:/data/known-trips.json
   ```

   If the copy fails because the container's root is read-only, use a throwaway container:
   `docker run --rm --volumes-from fibotrail -v "$PWD":/in alpine cp /in/known-trips.json /data/`.

3. Run it: `docker exec -it fibotrail fibotrail replay`.

| # | Step | Expected | Result |
| --- | --- | --- | --- |
| R.1 | The command prints each known trip as matched or missed | One line per trip | |
| R.2 | It lists detected trips that match no known trip | Review each. A real trip you forgot to list is fine | |
| R.3 | The exit code: `echo $?` | 0, which means at least 8 of every 10 matched. This is a tuning number, not the gate | |
| R.4 | The command changed nothing: Trips and the Inbox look the same as before | Same | |

A known trip is matched when a detected trip starts and ends within one day of it.

#### Reading a miss

For each missed known trip, find the cause in this table. It maps to the open questions of the
[trip detection spec](../specs/trip-detection.md#open-questions).

| What you see | Likely cause | Spec question |
| --- | --- | --- |
| Missed, and the trip has few photos with GPS | Only away photos count toward the 20 minimum, and 84.5 percent of photos have no position | 3 |
| One known trip detected as two trips | A photo at home, from someone else or from you, ended the trip. Or a gap of 72 hours or more | 2 |
| Two known trips detected as one | Two trips less than 72 hours apart with no photo at home between them | none, note it |
| Dates off by more than one day | Time zones, or photos with no position joining the wrong trip | none, note it |
| Detected, but marked Low or Medium when you'd call it High | The confidence thresholds | 1 |
| The home suggestion was wrong | A move, a second home, or a long stay somewhere else | 4 |
| A detected trip you didn't list | Fine, unless it isn't a trip. Count these | none |

#### If the replay passes

Write the numbers down: trips matched out of known, detected trips not on your list, and how many
High-confidence suggestions you'd accept without edits. The MVP aim is to accept most High
suggestions. Then answer each open question in the spec with what you saw, and remove the
question from the list.

#### If a gate or the replay fails

The detection settings aren't editable in the app in version 0.1. The defaults are in
`apps/server/src/Fibotrail.Detection/Models.cs`. Change one value at a time, rebuild with
`docker compose -f docker/compose.example.yml up -d --build`, and run `replay` and the review again. Record each
try: the value, and what the review showed. Stop when the gate is met, and record the
reason for each changed default in the spec. Don't change a default to match one trip. If the
trips you miss share a cause, such as no GPS, the fix may be a rule change, not a number. Open a
**Bug Report** for it.

## Part 6. Record the result

| # | Where | What to write |
| --- | --- | --- |
| 6.1 | [#7](https://github.com/bradreimer/fibotrail/issues/7) | The full scan finished, the photo count, the time it took, and which method showed only allowed requests |
| 6.2 | [Immich scanning spec](../specs/immich-scanning.md#acceptance-criteria) | Tick the two criteria that were open |
| 6.3 | [trip-detection spec](../specs/trip-detection.md#open-questions) | An answer for each open question the review settled |
| 6.4 | [status.md](../status.md) | The gate of step 3a is met, with the accepted count out of 10, the missing count, and the date. Remove the two Next items |
| 6.5 | [#24](https://github.com/bradreimer/fibotrail/issues/24) | The first two gates before the tag are met |
