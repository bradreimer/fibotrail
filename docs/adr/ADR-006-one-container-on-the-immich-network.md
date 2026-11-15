# ADR-006: One container on the Immich network

## Status

Accepted

## Context

The maintainer runs Immich in Docker and wants Fibotrail hosted in a Docker container with local
access to the Immich container. Fibotrail reads photo metadata and thumbnails from Immich often,
so the path between them should be short and private.

## Decision

Fibotrail ships as one Docker image and runs as one container on the same Docker network as
Immich.

- The container joins Immich's existing Docker network as an external network in the compose
  file.
- Fibotrail calls Immich by its container name over that network, for example
  `http://immich_server:2283`. The address is set by the `IMMICH_URL` variable.
- The one container holds the backend, the built UI, the scanner, and the trip detector. There is
  no separate database container, because the database is a SQLite file.
- All state lives in one volume mounted at `/data`.
- The container publishes one HTTP port. HTTPS and access from outside the home network are the
  job of the reverse proxy the maintainer already runs.
- The container runs as a non-root user with a read-only root file system, and exposes `/healthz`
  for the Docker health check.

## Alternatives considered

- **Reach Immich through its public address.** This works without a shared network, but every
  thumbnail request would leave the host and come back through the reverse proxy.
- **Separate containers for the API, the UI, and the scanner.** This allows independent scaling,
  which a single-household app doesn't need. It triples what must be configured.
- **Run on the host without Docker.** Set aside because the maintainer's services run in Docker,
  and a container is simpler to upgrade and remove.

## Consequences

- Installing Fibotrail is: add one service to a compose file, name the Immich network, and supply
  `.env`.
- Traffic between Fibotrail and Immich never leaves the Docker host.
- Fibotrail must be on the same Docker host as Immich, or the network must be extended some other
  way. A remote Immich still works by setting `IMMICH_URL` to its address.
- The scanner shares CPU and memory with the web server, so scans run in background batches.
