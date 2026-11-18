# Immich scanning

Tracking issue: [#7](https://github.com/bradreimer/fibotrail/issues/7).

## Purpose

This spec defines how Fibotrail reads your Immich library. The scanner copies the small amount of
metadata that trip detection needs (when and where each photo was taken) into Fibotrail's own
database, and the thumbnail proxy shows your photos without giving the browser the Immich key.

It covers stories U2 and U4 in the [roadmap](../roadmap.md#mvp-version-01), MVP step 2b.

## User story

As the owner of an Immich server, I want Fibotrail to read my library with a read-only key and
show its progress, so that it knows where and when my photos were taken without ever changing
them.

## Goals

- Fibotrail never writes to Immich, per [ADR-001](../adr/ADR-001-immich-is-read-only.md).
- The first scan of a large library runs in the background and can be interrupted and resumed.
- Later scans read only what changed.
- A slow or unreachable Immich produces a plain message, never a hang.

## Non-goals

- Scheduled scans. The MVP scans when you select **Scan now**.
- Limiting a rescan to a date range. The command rereads the whole library.
- Trip detection. See [trip-detection.md](trip-detection.md).
- A settings screen for the Immich address or key. Both come from `.env`.
- Copies of original photos or videos, and video playback. A video plays in Immich.
- Removing photos that were deleted from Immich for good. Immich doesn't report them, so this
  needs a full pass that compares IDs. It arrives after the MVP, as a **Rebuild from Immich**
  button.

## Requirements

### Configuration

| Variable | Required | Default | Purpose |
| --- | --- | --- | --- |
| `IMMICH_URL` | Yes | None | The address the container uses to reach Immich |
| `IMMICH_API_KEY` | Yes | None | A key with `asset.read`, `asset.view`, `album.read`, and `asset.statistics` only |
| `IMMICH_EXTERNAL_URL` | No | Empty | The address your browser uses to reach Immich, for **Open in Immich** links |
| `IMMICH_TIMEOUT_SECONDS` | No | 30 | How long one Immich request may take |

- The key is sent only in the `x-api-key` header of requests to `IMMICH_URL`.
- `IMMICH_TIMEOUT_SECONDS` must be a whole number from 1 to 600. Any other value stops the server
  at startup with a message that names the variable.

### Client

- The Immich client is C# code generated from Immich's OpenAPI file, in its own project,
  `Fibotrail.Immich`. `scripts/generate-immich-client.sh` writes it.
- The server calls only an allowed list of Immich endpoints: the server version, the metadata
  search, the asset statistics, and the asset thumbnail. A test fails if a call is made outside the list.
- The generated client holds only the requests on the list, and a guard stops any other request
  before it leaves, per
  [ADR-008](../adr/ADR-008-a-generated-immich-client-limited-to-allowed-reads.md).
- `POST /api/search/metadata` is a read. It's the only request that isn't a `GET`.
- The field names and parameters below were confirmed against Immich 3.4.1. See
  [immich-api-findings.md](../knowledge/immich-api-findings.md).

### Connection check

- The server checks the connection at startup and whenever Settings opens. Settings shows the
  result and the Immich version.
- The check makes two requests: the server version, and a metadata search with `size` set to 1.
  Immich answers the version request without a key, so only the search shows that the key works.
- When the check fails, the message is "Can't reach Immich. Check `IMMICH_URL` and
  `IMMICH_API_KEY` in your `.env` file."
- When the Immich version is outside the tested range, Settings shows a warning and scanning still
  runs. The tested range is 3.4, the version of the spike.
- A connection check isn't retried, so Settings gets its answer within two timeouts. When the
  check fails, Settings offers **Check again**.
- The result of the check at startup is written to the log. The server starts either way.

### Scanner

- The scanner reads image assets and video assets. In the MVP specs, "photo" means either one. A
  video has a capture time and a position like an image, and it counts toward a trip the same way.
- The `photo` table also stores whether the asset is a video.
- A scan pages through `POST /api/search/metadata` with `withExif` set to true, `size` set to
  1000, and a `filter` on `visibility` equal to `timeline`. The filter leaves out the hidden
  video that holds the motion of a live photo, so a live photo counts once.
- The cursor is the `nextCursor` value of the last page read. A null `nextCursor` ends the scan.
- For each asset, the `photo` table stores these values. Nothing else is stored.

  | Value | Immich field |
  | --- | --- |
  | Asset ID | `id` |
  | Local capture time | `localDateTime`, read as a wall-clock time with no zone |
  | Latitude and longitude | `exifInfo.latitude` and `exifInfo.longitude` |
  | City, state, and country | `exifInfo.city`, `exifInfo.state`, and `exifInfo.country` |
  | Immich's updated time | `updatedAt` |
  | Whether it's a video | `type` equal to `VIDEO` |
  | Length of a video, in milliseconds | `duration` |
  | Thumbnail version | `thumbhash`, which Immich makes again with each new thumbnail |

- Rows are keyed by asset ID. An asset that Immich returns again replaces its row.
- An asset with `isTrashed` set to true isn't stored, and its row is removed if it has one.
- An asset with no capture time is skipped. A position of exactly 0, 0 is stored as no position,
  so the photo can still join a trip by date.
- After each page, the scanner saves its cursor and the count of photos seen to `scan_run`.
- A scan that stops for any reason resumes from the saved cursor the next time it starts. A scan
  that a restart interrupted starts again by itself when the server starts. A scan that failed
  waits for **Try again** or **Scan now**.
- After the first complete scan, a scan requests only assets added or changed since the last
  complete scan. It adds `updatedAt` with `gt` to the `filter`, set to the latest updated time in
  the `photo` table when that scan completed.
- One scan runs at a time. **Scan now** is disabled while a scan runs.
- When a scan completes, trip detection runs.

### Timeouts and retries

- Every Immich request has a timeout of `IMMICH_TIMEOUT_SECONDS`.
- A request that times out or fails is retried 3 times, after 1, 5, and 15 seconds.
- When the retries are used up, the scan stops with the status "failed", and the banner says
  "Immich didn't answer in time. Your progress is saved." with a **Try again** button.

### Progress

- While a scan runs, the banner shows a count, such as "Scanning your library: 12,400 of 48,000
  photos."
- The total comes from `GET /api/assets/statistics`, read when the scan starts. The search
  response doesn't give the size of the library.
- A scan of changes only has no total, because Immich doesn't say how many assets changed. Its
  banner shows a count alone, such as "Scanning your library: 250 photos so far."
- The banner asks the server for progress every 2 seconds while a scan runs.
- No request uses `album.read` in the MVP. The key has it for later use.
- The app stays usable during a scan. Screens show the data scanned so far.
- Settings shows the time and result of the last scan.

### Rescan command

Tracking issue: [#120](https://github.com/bradreimer/fibotrail/issues/120).

A normal scan reads only the assets that Immich says changed. If you edit a photo's location in
Immich and Immich doesn't change the photo's `updatedAt`, a normal scan never sees the edit. The
rescan command reads everything, so it always does. It is the command-line form of the
**Rebuild from Immich** button, minus the removal of deleted photos.

- The command is `docker exec -it fibotrail fibotrail rescan`. It takes no arguments and needs the
  same `IMMICH_URL` and `IMMICH_API_KEY` as the server.
- It runs a new scan with no `updatedAt` filter, pages and retries as in the Scanner section, and
  stores each asset by ID. A changed position or place name replaces the old one.
- When the scan completes, trip detection runs, as after any scan. Your decisions are kept, per
  [ADR-002](../adr/ADR-002-decisions-are-stored-separately-from-detected-data.md). A trip marked
  **Not a trip** stays hidden.
- It prints a progress line about every 5,000 photos, then the photos read, the rows changed, and
  how many new suggestions reached the inbox.
- It refuses to start when the latest scan is "running", and exits with 1. If no scan is really
  running, for example because the command's terminal was killed, restarting the container resumes
  the scan.
- Ctrl+C stops it after the page in hand. The scan is marked "failed" with its progress saved, and
  the next scan, normal or rescan, can continue.
- Exit codes: 0 when the scan completes, 1 when it fails or can't start, 2 for arguments.
- **Scan now** is refused with 409 "A scan is already running." while the latest scan is "running"
  and the server isn't scanning itself, which is the command's case
  ([#124](https://github.com/bradreimer/fibotrail/issues/124)).
- Settings has a **Rescan everything** button next to **Scan now**
  ([#138](https://github.com/bradreimer/fibotrail/issues/138)). It calls `POST /api/rescan`, which
  starts the same full pass in the server. The banner shows the progress. It is disabled while a scan
  runs, and the route returns 409 "A scan is already running." in the same cases as **Scan now**.
  If the server stops during the pass, the run stays "running" and resumes at the next start.
- Not covered: a photo deleted from Immich for good stays in Fibotrail until **Rebuild from
  Immich** exists.

### Thumbnail proxy

- The browser requests images from Fibotrail, and Fibotrail fetches them from
  `GET /api/assets/{id}/thumbnail` on `IMMICH_URL`.
- The proxy serves two sizes: a thumbnail for grids and a preview for the photo viewer, which are
  Immich's `size=thumbnail` and `size=preview`. For a video, both are still frames.
- The proxy asks for `edited=true`, so an edit made in Immich, such as a rotation, shows. Immich
  sends the original when a photo has no edit.
- The proxy serves only asset IDs that are in the `photo` table, and only to a signed-in session.
- Each photo's image has a version: 16 hex digits of a SHA-256 of its stored `thumbhash` and
  `updatedAt`. When a photo changes in Immich, such as after a rotation, the next scan stores the
  new values, so the version changes. **Scan now** reads the change when Immich gives it a new
  `updatedAt`. **Rescan everything** and `fibotrail rescan` read it either way
  ([#161](https://github.com/bradreimer/fibotrail/issues/161)).
- Fetched images are cached in `/data/thumbnails`, under the photo ID and its version. An image of
  an older version is fetched again, and the older file is deleted. The cache can be deleted at any
  time and refills on demand.
- The browser keeps an image in its private cache, but asks again each time
  (`Cache-Control: private, no-cache`). The answer has the version as its `ETag`. While the version
  is the same, the proxy answers 304 with no body and doesn't ask Immich.

## Acceptance criteria

- With a wrong `IMMICH_URL`, Settings shows the "Can't reach Immich" message.
- A full scan of the maintainer's library finishes, and the banner count rises while it runs.
- Stopping the container mid-scan and starting it again continues from the saved cursor.
- A second scan, with no changes in Immich, stores no new rows.
- With `IMMICH_TIMEOUT_SECONDS=1` and a slow Immich, the scan fails with the timeout message and
  keeps its progress.
- The Immich access log for a full scan shows only requests on the allowed list.
- The key appears in no log line, no API response, and no file served to the browser.
- A thumbnail request for an ID that isn't in the `photo` table returns 404.
- After a photo is rotated in Immich, **Rescan everything** makes the trip show the rotated
  thumbnail, preview, and cover, with no other step.
- An image request with the current `ETag` in `If-None-Match` returns 304 and makes no request to
  Immich.
- A video taken away from home counts toward the minimum photos of a trip.
- After a photo's position changes in Immich without a new `updatedAt`, `fibotrail rescan` stores
  the new position and **Scan now** doesn't.
- `fibotrail rescan` on an unchanged library reports 0 changed.
- While a rescan's run is "running", **Scan now** returns 409 and makes no request to Immich.
- **Rescan everything** stores a position edited in Immich without a new `updatedAt`, sends no
  `updatedAt` filter, and returns 409 while a scan runs.
- `fibotrail rescan` sends no `updatedAt` filter, refuses while a scan runs, and exits with 1 and
  the "Can't reach Immich" message when Immich rejects the key.

## Open questions

None.
