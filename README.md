# Fibotrail

Fibotrail turns your Immich photo library into a travel journal. It finds your trips, maps them,
and counts them. Immich supplies the photos and the GPS. Fibotrail adds trips, places, and
statistics on top, and keeps them in its own database. **Fibotrail only reads from Immich. It
never changes your library.**

```text
Immich → Scanner → Trip detector → Database → Web server → Phone or iPad
```

Version 0.1 finds trips, lets you review them in an inbox, and shows each trip with a route map,
key numbers, and photos by day. The life map, collections, and statistics come later. See the
[roadmap](docs/roadmap.md).

## Install

You need a running Immich server and Docker with Compose. Fibotrail runs as one container on the
same Docker network as Immich.

1. Create a read-only API key in Immich. The permissions are in
   [API key permissions](#api-key-permissions).
2. Copy `.env.example` to `.env` and fill in `IMMICH_API_KEY`. Check the other values in
   [Configuration](#configuration).
3. Open `docker/compose.example.yml`. Set the network name at the bottom to the network your
   Immich containers use. `docker network ls` lists the networks.
4. Start Fibotrail:

   ```bash
   docker compose -f docker/compose.example.yml up -d
   ```

5. Open `http://localhost:11235` on the host. Fibotrail asks you to set a password. This is the
   first run, and the page is plain HTML from the server.
6. For any other address, put your reverse proxy in front of port 11235. Sign-in needs HTTPS, and
   Fibotrail shows a message when you open it over plain HTTP at an address that isn't
   `localhost`.
7. Choose **Scan now**, then confirm your home location when asked. Fibotrail needs it to know
   when you're away.

The container runs as user 1654, not as root, and it can write only to `/data`. The example compose
file uses a named volume, and Docker sets the owner for you. If you mount a host folder on `/data`
instead, give that folder to the container's user first, or Fibotrail can't open its database and
the log says so:

```bash
sudo chown 1654:1654 /path/to/your/folder
```

On a phone or iPad, open the HTTPS address in Safari and choose **Share**, then **Add to Home
Screen**. The app then opens full screen, like a native app.

The image is `ghcr.io/bradreimer/fibotrail`. It's built for amd64 and arm64. Its tags are:

| Tag | What it is |
| --- | --- |
| `latest` | The newest release. Use this in production |
| `0.1.0` | One release, pinned. Use this to stay on a version |
| `edge` | The newest commit on `main`. For testing, and it can be broken |

The example compose file uses `latest`. The first scan of a
large library takes a while. Fibotrail shows progress, and you can keep using the app.

## Configuration

Every setting is an environment variable. Docker Compose reads them from `.env`. Never commit
`.env`.

| Variable | Default | What it does |
| --- | --- | --- |
| `IMMICH_URL` | `http://immich_server:2283` | The address of Immich as the Fibotrail container sees it |
| `IMMICH_EXTERNAL_URL` | none | The address of Immich as your browser sees it. Fibotrail uses it only to build **Open in Immich** links. Leave it empty to hide them |
| `IMMICH_TIMEOUT_SECONDS` | `30` | Seconds to wait for one Immich request before it fails and is retried |
| `IMMICH_API_KEY` | none | The read-only API key. Fibotrail never shows it in the browser or in logs |
| `MCP_TOKEN` | none | A password of at least 32 characters that turns on the MCP server at `/mcp`, so an AI client can ask questions about your trips. Empty keeps it off. Fibotrail never shows it in the browser or in logs. The **MCP** screen shows how to connect |
| `FIBOTRAIL_DATA_DIR` | `/data` | The folder for Fibotrail's data. Leave it alone in Docker |
| `TZ` | `America/Vancouver` | The time zone of the container, which sets the times in logs |

## API key permissions

Create the key in Immich under **Account Settings**, then **API Keys**. Give it only these
permissions:

| Permission | Why Fibotrail needs it |
| --- | --- |
| `asset.read` | Search your photos for capture time, place, and GPS |
| `asset.view` | Fetch thumbnails to show in the app |
| `asset.statistics` | Read the total photo count, for scan progress |
| `album.read` | Not used by version 0.1. Later versions read albums, so add it now to avoid making a new key |

No write permission is needed, and Fibotrail never asks for one. It makes exactly four requests to
Immich: `GET /api/server/version`, `POST /api/search/metadata`, `GET /api/assets/statistics`, and
`GET /api/assets/{id}/thumbnail`. A guard in the code refuses any other request. See
[ADR-008](docs/adr/ADR-008-a-generated-immich-client-limited-to-allowed-reads.md).

To check this yourself, read the Immich access log after a scan. It shows only those four
requests from Fibotrail.

## Backup

Everything Fibotrail stores is in `/data`. In the example compose file that's the volume
`fibotrail-data`. If you mount a host folder instead, it's that folder:

| Path | What it is | Back it up? |
| --- | --- | --- |
| `fibotrail.db` | Your trips, decisions, and settings | Yes |
| `keys/` | The keys that keep your sign-in sessions valid | Yes |
| `thumbnails/` | A cache of thumbnails | No. Fibotrail rebuilds it |
| `fibotrail-backup-<time>.db` | A copy that Fibotrail makes before it changes the database for a new version | Optional |

Your decisions, such as added, ignored, and renamed trips, live in `fibotrail.db`, and they
always win over detection. A backup keeps them.

To back up, stop the container, copy `/data` to a tar file, and start the container again. The
container stops for about 10 seconds. Stopping first matters, because a copy of a database that's
in use can be inconsistent.

```bash
docker stop fibotrail
docker run --rm --volumes-from fibotrail -v "$PWD":/backup alpine \
  tar czf /backup/fibotrail-data.tar.gz -C /data .
docker start fibotrail
```

The tar file holds your database, so keep it somewhere private. The helper container runs as
root, so the file is owned by root. Use `sudo` to move or delete it.

To restore, extract the tar file into an **empty** folder or volume. Don't extract over a database
that's in use. Run it as root, as below, so each file keeps its owner (user 1654):

```bash
mkdir restored
docker run --rm -v "$PWD/restored":/data -v "$PWD":/backup alpine \
  tar xzf /backup/fibotrail-data.tar.gz -C /data
```

To use the restored data, stop the container, then mount `restored` on `/data` in place of the old
folder or volume, and start it. To check a restored database without starting Fibotrail, open it
read-only. The folder belongs to user 1654, so a plain `sqlite3 restored/fibotrail.db` fails with
"attempt to write a readonly database":

```bash
sqlite3 "file:restored/fibotrail.db?immutable=1" "pragma integrity_check;"
```

It should print `ok`.

Your photos are in Immich, not in Fibotrail. Back up Immich the way Immich describes.

## Privacy

Fibotrail makes no request to any outside service. It talks only to your Immich server. It never
writes to Immich, and it never sends photo data or location data anywhere on its own. The base map
is bundled in the image.

The one door is the optional MCP server. It is off until you set `MCP_TOKEN`. A client that you
connect with that token can ask for counts, dates, distances, and the names of countries,
continents, collections, and parks. It never gets photos, coordinates, trip names, or city names,
and it can't change anything. If the client uses a model in the cloud, its answers go to that
model. To keep them at home, connect a client that uses a model on your own computer. The reasons
are in [ADR-018](docs/adr/ADR-018-an-mcp-server-answers-questions-with-coarse-read-only-data.md).

## Updating and resetting

To update, pull the new image and restart:

```bash
docker compose -f docker/compose.example.yml pull
docker compose -f docker/compose.example.yml up -d
```

Fibotrail copies the database to `fibotrail-backup-<time>.db` in `/data` before it applies a
change to it.

If you forget your password, reset it:

```bash
docker exec -it fibotrail fibotrail reset-password
```

It asks for the new password twice. A password needs at least 12 characters. A shorter one is
refused, and nothing changes: you stay signed in, and the old password still works. When the reset
works, it signs you out everywhere, and the next visit shows the sign-in screen.

## Documentation

- [Design and branding document](docs/knowledge/design-and-branding.md): the brand, the
  experience, and the architecture for version 1.
- [Project guidelines](docs/guidelines.md): the seven rules every change follows.
- [Documentation map](docs/README.md): where specs, decisions, and knowledge live.
- [Roadmap](docs/roadmap.md) and [status](docs/status.md): the plan and the current state.

## Contributing

Fibotrail follows spec-driven, work-item-driven development, and unit tests enforce the project
organization. See [CONTRIBUTING.md](CONTRIBUTING.md). Run the checks with `./scripts/check.sh`.

## License

Fibotrail is MIT licensed. See [LICENSE](LICENSE). Immich is licensed under the AGPL-3.0. Fibotrail
includes no Immich source code, but its generated client repeats names from Immich's API
description. See
[the notice](apps/server/src/Fibotrail.Immich/Generated/NOTICE.md).
