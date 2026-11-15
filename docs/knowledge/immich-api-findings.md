# Immich API findings

This document records what a real Immich server returns for dates, positions, and place names, so
that the scanner and the trip detector are built on facts. It's the result of the spike,
[#12](https://github.com/bradreimer/fibotrail/issues/12).

- **Immich version tested:** 3.4.1, the `release` Docker image.
- **Date of the test:** 2026-11-15.
- **Library tested:** the maintainer's library, 71,898 assets.

The spike ran twice. The first run explored the API with a key that had 28 permissions, and it
sent only read requests. The second run repeated the requests that Fibotrail makes with a
read-only key that has four permissions: `asset.read`, `asset.view`, `album.read`, and
`asset.statistics`. Every request succeeded. See question 6.

## Method

A throwaway Python script sent these requests with the `x-api-key` header:

- `GET /api/server/version` and `GET /api/server/ping`
- `POST /api/search/metadata`, with different parameters
- `GET /api/assets/{id}` and `GET /api/assets/{id}/thumbnail`

The script refused any other `POST`. It printed counts and the shape of each value, with letters
and digits masked, so no real ID, coordinate, or place left the server. The script and its output
are not in the repository.

Two more sources were read:

- Immich's OpenAPI file for the tag `v3.4.1`, for parameter names and permission names.
- The file list of the web app inside the `immich_server` container, for the photo route.

Every ID, time, coordinate, and place in this document is made up.

`IMMICH_URL=http://immich_server:2283` resolves only inside the Immich Docker network. From the
development host, the spike used the container's address on the `immich_default` network.

## Summary

| # | Question | Answer |
| --- | --- | --- |
| 1 | Local capture time and time zone | `localDateTime` holds the local time. `exifInfo.timeZone` holds the zone, and it's empty for 61% of assets |
| 2 | Position and place names | `exifInfo.latitude`, `longitude`, `city`, `state`, and `country` |
| 3 | Changed since, and page size | `filter.updatedAt.gt`, with `cursor` for paging and `size` 1000 |
| 4 | Deleted assets | A trashed asset has `isTrashed` true. A permanent delete leaves no trace, so a full pass must compare IDs |
| 5 | Videos | `type` is `VIDEO`, and `duration` is a whole number of milliseconds. The motion part of a live photo is a hidden video |
| 6 | Permissions | `asset.read` covers the search, `asset.view` covers the thumbnail, and the version needs no key. Confirmed with a read-only key. The library total needs `asset.statistics` |
| 7 | Path in the web app | `/photos/{id}` is right |
| 8 | No position, and listing time | 84.5% of timeline assets have no position. A full listing takes 29 seconds |
| 9 | Version | 3.4.1 |

## 1. Local capture time and time zone

`localDateTime` is the field to store. It holds the wall-clock time where the photo was taken,
written as if it were UTC. `2026-03-17T23:30:00.000Z` means 23:30 at the place of capture, not
23:30 UTC. Read the date and time from it and ignore the `Z`.

| Field | Format | What it holds |
| --- | --- | --- |
| `localDateTime` | `2026-03-17T23:30:00.000Z` | Local wall-clock time, labeled as UTC. Never empty |
| `fileCreatedAt` | `2026-03-18T03:30:00.000Z` | The moment of capture in UTC. Never empty |
| `exifInfo.dateTimeOriginal` | `2026-03-18T03:30:00+00:00` | The same moment as `fileCreatedAt`, always with the offset `+00:00`, with 0 to 3 decimal places |
| `exifInfo.timeZone` | `UTC-4`, `America/New_York`, `UTC`, or null | The zone Immich found in the file |
| `updatedAt` | `2026-09-07T21:52:37.969Z` | When Immich last changed its record of the asset |

Counts across all 71,898 assets:

- `localDateTime`, `fileCreatedAt`, and `exifInfo.dateTimeOriginal` were present on every asset.
  No asset lacked a capture time.
- `exifInfo.timeZone` was null on 44,108 assets (61.3%), `UTC±H` on 23,207, an IANA name such as
  `America/New_York` on 3,962, and `UTC` on 621.
- When the zone is null, `localDateTime` equals `fileCreatedAt`. Immich treats the camera's
  wall-clock time as UTC, so `localDateTime` is still the local time and `fileCreatedAt` is not a
  true UTC moment.
- When the zone is an offset or a name, `localDateTime` minus `fileCreatedAt` matched the zone's offset on
  27,124 of 27,169 assets. The 45 that didn't match weren't looked at one by one.
- Videos never had a null zone. 605 of 2,204 videos had `UTC`, which means their `localDateTime`
  is UTC time and not local time.

What this means for Fibotrail: grouping by local day uses `localDateTime` alone. Don't compute a
local time from `fileCreatedAt` and `timeZone`, because the zone is missing for most assets.

## 2. Position and place names

All five fields are inside `exifInfo`, which is returned only when the request has `withExif` set
to true.

| Field | Type | Notes |
| --- | --- | --- |
| `exifInfo.latitude` | Number or null | Decimal degrees |
| `exifInfo.longitude` | Number or null | Decimal degrees |
| `exifInfo.city` | String or null | From Immich's reverse geocoding |
| `exifInfo.state` | String or null | From Immich's reverse geocoding |
| `exifInfo.country` | String or null | The country name, not a code |

- Latitude and longitude were always both present or both null.
- No asset had a position of exactly 0, 0.
- A place name was never an empty string, and never present without a position.
- Of the 11,531 assets with a position, 63 had no city and no state, and 12 had no country.

## 3. Changed since, and page size

Immich 3.2.0 added `filter`, `orderBy`, and `cursor` to `POST /api/search/metadata` and marked the
older parameters as deprecated, including `page`, `updatedAfter`, and `withDeleted`. Both forms
work in 3.4.1. A request can't mix them: the server answers 400 with "Deprecated field page cannot
be combined with filter/cursor".

The request for a first full scan:

```json
{
  "withExif": true,
  "size": 1000,
  "filter": { "visibility": { "eq": "timeline" } }
}
```

The request for a later scan adds the changed-since filter and, after the first page, the cursor:

```json
{
  "withExif": true,
  "size": 1000,
  "filter": {
    "visibility": { "eq": "timeline" },
    "updatedAt": { "gt": "2026-09-07T21:52:37.969Z" }
  },
  "cursor": "eyJvZmZzZXQiOjEwMDB9"
}
```

What was observed:

- `size` accepts 1 to 1000. The default is 250. A size of 1001 returns 400.
- Larger pages are faster. The first 5,000 assets took 3.8 seconds at size 100, 1.8 seconds at 250,
  1.1 seconds at 500, and 0.6 seconds at 1000. Use 1000.
- `assets.nextCursor` holds the cursor for the next page, and it's null on the last page. With the
  new form, `assets.nextPage` is always null.
- `assets.total` is the number of items in the page, the same as `assets.count`. It isn't the size
  of the library.
- The cursor is an offset. It decodes to `{"offset":1000}`. If an asset is added or removed while
  a scan is paging, later pages shift by one, and an asset can be returned twice or skipped.
- The server doesn't tie a cursor to the request that produced it. A cursor sent with a different
  filter was accepted.
- Results are ordered by `fileCreatedAt`, newest first, with or without the `updatedAt` filter.
  `orderBy` accepts `fileCreatedAt`, `localDateTime`, `fileSizeInBytes`, and `rating`. It can't
  order by `updatedAt`.
- `updatedAt` with `gt` returns the asset whose `updatedAt` equals the given time. The response
  rounds the time to milliseconds, and the comparison doesn't. Store assets by ID, so that a
  repeated asset replaces its row.
- An unknown parameter at the top level is ignored without an error. An unknown key inside
  `filter` returns 400.
- An invalid date returns 400 with "Validation failed".

The older form, for reference: `page` starts at 1, `assets.nextPage` is a string such as `"2"`,
and `updatedAfter` takes the same time format.

## 4. Deleted assets

Immich has two kinds of delete, and a scan sees them differently.

- **Moved to the trash.** The asset is still returned, with `isTrashed` true. The new form with
  `filter` includes trashed assets. The older form leaves them out unless `withDeleted` is true.
  `filter.trashedAt` selects by the time of trashing. The thumbnail of a trashed asset still
  loads.
- **Deleted for good.** The asset is gone from every response. No search parameter reports it.
  `GET /api/assets/{id}` and the thumbnail request answer 400 with "Not found or no asset.read
  access" or "Not found or no asset.view access".

So a scan learns about a permanent delete only by listing every ID and comparing the list with
its own table. That pass is cheap: without `withExif`, listing all 71,898 assets took 7 seconds.

Not known: whether moving an asset to the trash changes its `updatedAt`. Testing that needs a
write to Immich, which the spike doesn't do. Immich also has `POST /api/sync/stream`, which needs
the `sync.stream` permission. The spike didn't call it.

## 5. Videos

| | Image | Video |
| --- | --- | --- |
| `type` | `IMAGE` | `VIDEO` |
| `duration` | null, except for a GIF | Whole number of milliseconds, never null |
| `originalMimeType` | `image/jpeg`, `image/heic`, and others | `video/quicktime`, `video/mp4`, `video/avi`, `video/x-m4v` |
| `exifInfo.timeZone` | Null on 63% | Never null |
| `exifInfo.fNumber`, `iso`, and other camera fields | Usually present | Usually null |
| Position | Present on 14.6% | Present on 62.9% |
| Thumbnail | WebP or JPEG | WebP or JPEG still frame |

- The library has 69,694 images and 2,204 videos. `type` can also be `AUDIO` or `OTHER`, and the
  library has none of those.
- 3 images had a duration. All 3 were GIFs.
- `duration` was a string such as `0:00:08.836` in older Immich versions. In 3.4.1 it's a number.
  The values ran from 98 to 323,200, and the OpenAPI file says milliseconds.

**Live photos.** A live photo is two assets: the image, and a video that holds the motion. The
image has `livePhotoVideoId` set to the ID of the video. The video has `visibility` set to
`hidden`. The library has 652 live photos and 584 hidden videos. A search with no visibility
filter returns the hidden videos, and their thumbnail request answers 404. Filter on
`visibility` equal to `timeline` so that a live photo isn't counted twice.

`visibility` can be `timeline`, `hidden`, `archive`, or `locked`. A search for `locked` answers
"Elevated permission is required". The library has no archived assets, so it's not known whether
a search with no filter returns them.

## 6. Permissions

| Request | Permission in the OpenAPI file | Observed |
| --- | --- | --- |
| `GET /api/server/version` | None, and no key needed | 200 without a key |
| `GET /api/server/ping` | None, and no key needed | 200 without a key |
| `POST /api/search/metadata` | `asset.read` | 401 without a key |
| `GET /api/assets/{id}/thumbnail` | `asset.view` | 401 without a key |
| `GET /api/assets/statistics` | `asset.statistics` | 401 without a key |
| `GET /api/server/about` | `server.about` | 401 without a key |

- `asset.read` and `asset.view` cover every request that the scanning spec allowed before the
  spike. No request needs `album.read`. The maintainer keeps it for later use.
- The library total needs a fourth permission. `assets.total` isn't the total.
  `GET /api/assets/statistics` returns `images`, `videos`, and `total`, and it needs
  `asset.statistics`. Its total was 71,314, which is the timeline assets without the 1 trashed
  asset.
- The server version needs no key. A successful version request shows that the address is right.
  It doesn't show that the key is valid. A search with `size` set to 1 does.

The second run used a key with `asset.read`, `asset.view`, `album.read`, and `asset.statistics`:

| Request | Result |
| --- | --- |
| `GET /api/server/version` and `GET /api/server/ping` | 200 |
| A search with `size` set to 1 | 200 |
| A full scan with `withExif`, the `timeline` filter, and `cursor` | 200 on all 72 pages, 28 seconds |
| A changed-since search with `filter.updatedAt.gt` | 200 |
| A thumbnail and a preview, for an image and for a video | 200 |
| `GET /api/assets/statistics` | 200 |
| `GET /api/albums` | 200 |
| `GET /api/assets/{id}/original` | 403, "Missing required permission: asset.download" |
| `GET /api/server/about` | 403, "Missing required permission: server.about" |

Not tested: a key with exactly three permissions. Each request needs one named permission, and the
403 message names it, so `asset.statistics` doesn't affect the search or the thumbnail.

Error responses are JSON with a `message`:

| Case | Status | Message |
| --- | --- | --- |
| No key | 401 | "Authentication required" |
| Wrong key | 401 | "Invalid API key" |
| Key lacks a permission | 403 | "Missing required permission: user.read" |
| Invalid request body | 400 | "Validation failed", with an `errors` list |
| Unknown asset ID | 400 | "Not found or no asset.read access" |

## 7. Path in the web app

`/photos/{id}` is right. The web app in the 3.4.1 container has the route
`/(user)/photos/[[assetId=id]]`, which the browser sees as `/photos/{id}`. The route was read from
the built files. The link wasn't opened in a browser.

## 8. Assets with no position, and listing time

| Group | Assets | No position | Share |
| --- | --- | --- | --- |
| Images | 69,694 | 59,550 | 85.4% |
| Videos, including hidden | 2,204 | 817 | 37.1% |
| Timeline assets | 71,314 | 60,270 | 84.5% |
| All assets | 71,898 | 60,367 | 84.0% |

Only 11,044 timeline assets have a position. Trip detection will depend on attaching photos by
date, as the [roadmap](../roadmap.md#risks) expects. The spike didn't measure how the
positions are spread over the years.

Listing time, on the same machine as Immich:

| Request | Pages | Time |
| --- | --- | --- |
| All assets with `withExif`, size 1000 | 72 | 29 seconds |
| All assets without `withExif`, size 1000 | 72 | 7 seconds |

A page with `withExif` took 0.4 seconds on average and 1.15 seconds at most. The default timeout
of 30 seconds leaves a wide margin.

## Thumbnails

`GET /api/assets/{id}/thumbnail` takes a `size` parameter.

| `size` | Content type | Sample size, image | Sample size, video |
| --- | --- | --- | --- |
| `thumbnail`, the default | `image/webp` | 14 KB | 20 KB |
| `preview` | `image/jpeg` | 284 KB | 126 KB |
| `fullsize` | `image/jpeg` | 284 KB | 126 KB |

Each figure is from one asset. `fullsize` returned the same bytes as `preview` for both. An
unknown size returns 400. The proxy's two sizes are `thumbnail` and `preview`.

## Made-up responses for the scanner's tests

These have the shape of real responses from 3.4.1, and every value is invented. Copy them into
the scanner's test project in [#7](https://github.com/bradreimer/fibotrail/issues/7). An asset has
29 fields and `exifInfo` has 22. Every asset in the library had the same set.

**A first page.** It holds an image with a position and a zone, an image with neither, a video,
and a live photo.

```json
{
  "albums": { "total": 0, "count": 0, "items": [], "facets": [] },
  "assets": {
    "total": 4,
    "count": 4,
    "items": [
      {
        "id": "11111111-1111-4111-8111-111111111111",
        "createdAt": "2026-04-02T18:00:00.000Z",
        "ownerId": "99999999-9999-4999-8999-999999999999",
        "libraryId": null,
        "type": "IMAGE",
        "originalPath": "/data/library/example/2026/2026-03/IMG_0001.HEIC",
        "originalFileName": "IMG_0001.HEIC",
        "originalMimeType": "image/heic",
        "thumbhash": "EXAMPLEthumbhash0001==",
        "fileCreatedAt": "2026-03-18T03:30:00.000Z",
        "fileModifiedAt": "2026-03-18T03:30:00.000Z",
        "localDateTime": "2026-03-17T23:30:00.000Z",
        "updatedAt": "2026-04-02T18:05:00.123Z",
        "isFavorite": false,
        "isArchived": false,
        "isTrashed": false,
        "visibility": "timeline",
        "duration": null,
        "exifInfo": {
          "make": "Example",
          "model": "Example Phone 1",
          "exifImageWidth": 4032,
          "exifImageHeight": 3024,
          "fileSizeInByte": 2500000,
          "orientation": "1",
          "dateTimeOriginal": "2026-03-18T03:30:00.12+00:00",
          "modifyDate": "2026-03-18T03:30:00+00:00",
          "timeZone": "UTC-4",
          "lensModel": "Example Phone 1 back camera 5.1mm f/1.8",
          "fNumber": 1.8,
          "focalLength": 5.1,
          "iso": 64,
          "exposureTime": "1/120",
          "latitude": 10.5,
          "longitude": 20.25,
          "city": "Port Example",
          "state": "Example Province",
          "country": "Exampleland",
          "description": "",
          "projectionType": null,
          "rating": null
        },
        "livePhotoVideoId": null,
        "people": [],
        "checksum": "EXAMPLEchecksum0000000001=",
        "isOffline": false,
        "hasMetadata": true,
        "duplicateId": null,
        "resized": true,
        "width": 4032,
        "height": 3024,
        "isEdited": false
      },
      {
        "id": "22222222-2222-4222-8222-222222222222",
        "createdAt": "2026-04-02T18:00:01.000Z",
        "ownerId": "99999999-9999-4999-8999-999999999999",
        "libraryId": null,
        "type": "IMAGE",
        "originalPath": "/data/library/example/2026/2026-03/DSC_0002.JPG",
        "originalFileName": "DSC_0002.JPG",
        "originalMimeType": "image/jpeg",
        "thumbhash": "EXAMPLEthumbhash0002==",
        "fileCreatedAt": "2026-03-17T14:00:00.000Z",
        "fileModifiedAt": "2026-03-17T14:00:00.000Z",
        "localDateTime": "2026-03-17T14:00:00.000Z",
        "updatedAt": "2026-04-02T18:05:01.456Z",
        "isFavorite": false,
        "isArchived": false,
        "isTrashed": false,
        "visibility": "timeline",
        "duration": null,
        "exifInfo": {
          "make": "Example",
          "model": "Example Camera 2",
          "exifImageWidth": 6000,
          "exifImageHeight": 4000,
          "fileSizeInByte": 7500000,
          "orientation": "1",
          "dateTimeOriginal": "2026-03-17T14:00:00+00:00",
          "modifyDate": "2026-03-17T14:00:00+00:00",
          "timeZone": null,
          "lensModel": null,
          "fNumber": 8,
          "focalLength": 35,
          "iso": 200,
          "exposureTime": "1/500",
          "latitude": null,
          "longitude": null,
          "city": null,
          "state": null,
          "country": null,
          "description": "",
          "projectionType": null,
          "rating": null
        },
        "livePhotoVideoId": null,
        "people": [],
        "checksum": "EXAMPLEchecksum0000000002=",
        "isOffline": false,
        "hasMetadata": true,
        "duplicateId": null,
        "resized": true,
        "width": 6000,
        "height": 4000,
        "isEdited": false
      },
      {
        "id": "33333333-3333-4333-8333-333333333333",
        "createdAt": "2026-04-02T18:00:02.000Z",
        "ownerId": "99999999-9999-4999-8999-999999999999",
        "libraryId": null,
        "type": "VIDEO",
        "originalPath": "/data/library/example/2026/2026-03/IMG_0003.MOV",
        "originalFileName": "IMG_0003.MOV",
        "originalMimeType": "video/quicktime",
        "thumbhash": "EXAMPLEthumbhash0003==",
        "fileCreatedAt": "2026-03-16T16:15:00.000Z",
        "fileModifiedAt": "2026-03-16T16:15:09.000Z",
        "localDateTime": "2026-03-16T12:15:00.000Z",
        "updatedAt": "2026-04-02T18:05:02.789Z",
        "isFavorite": false,
        "isArchived": false,
        "isTrashed": false,
        "visibility": "timeline",
        "duration": 8836,
        "exifInfo": {
          "make": "Example",
          "model": "Example Phone 1",
          "exifImageWidth": 1920,
          "exifImageHeight": 1080,
          "fileSizeInByte": 18000000,
          "orientation": "1",
          "dateTimeOriginal": "2026-03-16T16:15:00+00:00",
          "modifyDate": "2026-03-16T16:15:09+00:00",
          "timeZone": "America/New_York",
          "lensModel": null,
          "fNumber": null,
          "focalLength": null,
          "iso": null,
          "exposureTime": null,
          "latitude": 10.75,
          "longitude": 20.5,
          "city": "Example Springs",
          "state": "Example Province",
          "country": "Exampleland",
          "description": "",
          "projectionType": null,
          "rating": null
        },
        "livePhotoVideoId": null,
        "people": [],
        "checksum": "EXAMPLEchecksum0000000003=",
        "isOffline": false,
        "hasMetadata": true,
        "duplicateId": null,
        "resized": true,
        "width": 1920,
        "height": 1080,
        "isEdited": false
      },
      {
        "id": "44444444-4444-4444-8444-444444444444",
        "createdAt": "2026-04-02T18:00:03.000Z",
        "ownerId": "99999999-9999-4999-8999-999999999999",
        "libraryId": null,
        "type": "IMAGE",
        "originalPath": "/data/library/example/2026/2026-03/IMG_0004.HEIC",
        "originalFileName": "IMG_0004.HEIC",
        "originalMimeType": "image/heic",
        "thumbhash": "EXAMPLEthumbhash0004==",
        "fileCreatedAt": "2026-03-15T19:45:00.000Z",
        "fileModifiedAt": "2026-03-15T19:45:00.000Z",
        "localDateTime": "2026-03-15T15:45:00.000Z",
        "updatedAt": "2026-04-02T18:05:03.000Z",
        "isFavorite": false,
        "isArchived": false,
        "isTrashed": false,
        "visibility": "timeline",
        "duration": null,
        "exifInfo": {
          "make": "Example",
          "model": "Example Phone 1",
          "exifImageWidth": 4032,
          "exifImageHeight": 3024,
          "fileSizeInByte": 2100000,
          "orientation": "6",
          "dateTimeOriginal": "2026-03-15T19:45:00.5+00:00",
          "modifyDate": "2026-03-15T19:45:00+00:00",
          "timeZone": "UTC-4",
          "lensModel": "Example Phone 1 back camera 5.1mm f/1.8",
          "fNumber": 1.8,
          "focalLength": 5.1,
          "iso": 50,
          "exposureTime": "1/250",
          "latitude": 10.5,
          "longitude": 20.25,
          "city": "Port Example",
          "state": "Example Province",
          "country": "Exampleland",
          "description": "",
          "projectionType": null,
          "rating": null
        },
        "livePhotoVideoId": "55555555-5555-4555-8555-555555555555",
        "people": [],
        "checksum": "EXAMPLEchecksum0000000004=",
        "isOffline": false,
        "hasMetadata": true,
        "duplicateId": null,
        "resized": true,
        "width": 4032,
        "height": 3024,
        "isEdited": false
      }
    ],
    "facets": [],
    "nextPage": null,
    "nextCursor": "eyJvZmZzZXQiOjR9"
  }
}
```

**A last page.** It holds one trashed asset, with a position and no place names. `nextCursor` is
null. Only the fields that differ from the first image above are listed. A test fills in the rest.

```json
{
  "albums": { "total": 0, "count": 0, "items": [], "facets": [] },
  "assets": {
    "total": 1,
    "count": 1,
    "items": [
      {
        "id": "66666666-6666-4666-8666-666666666666",
        "type": "IMAGE",
        "localDateTime": "2026-03-14T09:00:00.000Z",
        "updatedAt": "2026-04-03T08:00:00.000Z",
        "isTrashed": true,
        "visibility": "timeline",
        "duration": null,
        "exifInfo": {
          "timeZone": null,
          "latitude": -5.125,
          "longitude": 30.5,
          "city": null,
          "state": null,
          "country": null
        },
        "livePhotoVideoId": null
      }
    ],
    "facets": [],
    "nextPage": null,
    "nextCursor": null
  }
}
```

**An empty page.** This is the answer when nothing changed since the last scan.

```json
{
  "albums": { "total": 0, "count": 0, "items": [], "facets": [] },
  "assets": {
    "total": 0,
    "count": 0,
    "items": [],
    "facets": [],
    "nextPage": null,
    "nextCursor": null
  }
}
```

**The server version.**

```json
{ "major": 3, "minor": 4, "patch": 1, "prerelease": null, "featureLevel": 2 }
```

**Errors.**

```json
{ "message": "Invalid API key" }
```

```json
{
  "message": "Validation failed",
  "errors": [
    {
      "origin": "number",
      "code": "too_big",
      "maximum": 1000,
      "inclusive": true,
      "path": ["size"],
      "message": "Too big: expected number to be <=1000"
    }
  ]
}
```

## What isn't known yet

1. Whether moving an asset to the trash, or restoring it, changes `updatedAt`.
2. Whether a search with no visibility filter returns archived assets.
3. Why 45 assets have a zone that doesn't match their local time.
4. How any of this differs in Immich versions before 3.2.0, which don't have `filter` and
   `cursor`.
