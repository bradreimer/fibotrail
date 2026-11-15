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
- Trip detection. See [trip-detection.md](trip-detection.md).
- A settings screen for the Immich address or key. Both come from `.env`.
- Copies of original photos or videos.

## Requirements

### Configuration

| Variable | Required | Default | Purpose |
| --- | --- | --- | --- |
| `IMMICH_URL` | Yes | None | The address the container uses to reach Immich |
| `IMMICH_API_KEY` | Yes | None | A key with `asset.read`, `asset.view`, and `album.read` only |
| `IMMICH_EXTERNAL_URL` | No | Empty | The address your browser uses to reach Immich, for **Open in Immich** links |
| `IMMICH_TIMEOUT_SECONDS` | No | 30 | How long one Immich request may take |

- The key is sent only in the `x-api-key` header of requests to `IMMICH_URL`.
- `IMMICH_TIMEOUT_SECONDS` must be a whole number from 1 to 600. Any other value stops the server
  at startup with a message that names the variable.

### Client

- The Immich client is C# code generated from Immich's OpenAPI file, in its own project.
- The server calls only an allowed list of Immich endpoints: the server version, the metadata
  search, and the asset thumbnail. A test fails if a call is made outside the list.
- `POST /api/search/metadata` is a read. It's the only request that isn't a `GET`.

### Connection check

- The server checks the connection at startup and whenever Settings opens. Settings shows the
  result and the Immich version.
- When the check fails, the message is "Can't reach Immich. Check `IMMICH_URL` and
  `IMMICH_API_KEY` in your `.env` file."
- When the Immich version is outside the tested range, Settings shows a warning and scanning still
  runs.

### Scanner

- A scan pages through `POST /api/search/metadata` with `withExif` set to true.
- For each asset, the `photo` table stores the Immich asset ID, the local capture time, latitude,
  longitude, city, state, country, and Immich's updated time. Nothing else is stored.
- An asset with no capture time is skipped. A position of exactly 0, 0 is stored as no position,
  so the photo can still join a trip by date.
- After each page, the scanner saves its cursor and the count of photos seen to `scan_run`.
- A scan that stops for any reason resumes from the saved cursor the next time it starts.
- After the first complete scan, a scan requests only assets added or changed since the last
  complete scan.
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
- The app stays usable during a scan. Screens show the data scanned so far.
- Settings shows the time and result of the last scan.

### Thumbnail proxy

- The browser requests images from Fibotrail, and Fibotrail fetches them from
  `GET /api/assets/{id}/thumbnail` on `IMMICH_URL`.
- The proxy serves two sizes: a thumbnail for grids and a preview for the photo viewer.
- The proxy serves only asset IDs that are in the `photo` table, and only to a signed-in session.
- Fetched images are cached in `/data`. The cache can be deleted at any time and refills on
  demand.

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

## Open questions

The spike, roadmap step 1, answers the first three.

1. Which fields hold the local capture time and its time zone?
2. Which request parameter returns only assets changed since a given time, and what page size
   works well?
3. How does a scan learn that a photo was deleted from Immich? The recommendation is a full pass
   that compares IDs, run from a **Rebuild from Immich** button, after the MVP.
4. Do videos count? The recommendation is yes, because a video has a time and a place like a
   photo, and the trip page shows its thumbnail.
