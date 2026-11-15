# First run and sign-in

Tracking issue: [#5](https://github.com/bradreimer/fibotrail/issues/5).

## Purpose

This spec defines how Fibotrail starts as a Docker container and how one person gets access to it.
Your travel history is personal, so nothing is shown until you've set a password and signed in.

It covers story U1 in the [roadmap](../roadmap.md#mvp-version-01), MVP step 2a.

## User story

As the owner of an Immich server, I want to start Fibotrail with Docker Compose and set a password
on first run, so that my travel history is running next to Immich and only I can open it.

## Goals

- Setup is three steps: copy `.env.example` to `.env`, fill in the Immich key, and run
  `docker compose up`.
- A missing setting stops the app with a message that names it.
- One password protects everything except the health check.
- A forgotten password is fixed from the host in one command, with no email and no data loss.

## Non-goals

- More than one user, or a read-only login for a household member.
- Docker secrets. `.env` is the starting point, per
  [ADR-005](../adr/ADR-005-secrets-come-from-the-environment.md).
- Requests to Immich. See [immich-scanning.md](immich-scanning.md).
- HTTPS. Your reverse proxy provides it.

## Requirements

### Container

- Fibotrail runs as one container, per
  [ADR-006](../adr/ADR-006-one-container-on-the-immich-network.md). The image has two build stages
  and holds the ASP.NET Core runtime only, per [ADR-004](../adr/ADR-004-technology-stack.md).
- The container runs as a non-root user with a read-only root file system. `/data` is the one
  writable volume.
- Inside the container, the server listens on port 8080 and serves the built UI and the API from
  the same origin.
- `GET /healthz` returns 200 when the server is up and the database opens. It needs no session.
- `docker/` holds the Dockerfile and an example compose file that joins the Immich network and
  loads `.env`.
- The example compose file publishes the container's port 8080 on host port 11235, as
  `11235:8080`. With the default setup, Fibotrail is at `http://<host>:11235`. The host port is
  yours to change in the compose file. The Docker health check runs inside the container and
  uses port 8080.

### Configuration

- The server reads its settings from environment variables through ASP.NET Core's configuration
  system.
- `IMMICH_URL` and `IMMICH_API_KEY` are required. When one is missing, the server exits at startup
  and logs a message that names the variable.
- No secret is written to a log.

### Database

- The database is one SQLite file in `/data`, in WAL mode, accessed through Entity Framework Core.
- Migrations run at startup. Before a migration changes an existing database, the server saves a
  copy of the file in `/data`.

### First run

- When no password is stored, every page leads to a setup screen that asks you to choose a
  password and to type it twice.
- A password has at least 12 characters. No other composition rule applies.
- The password is stored only as an Argon2id hash.
- The setup route stops working once a password is stored.

### Sign-in and sessions

- When a password is stored and you have no session, every page leads to the sign-in screen.
- A session is an HttpOnly, Secure, SameSite=Lax cookie. It lasts 30 days from its last use.
- Every request that changes state is protected against cross-site request forgery.
- After 5 failed sign-in attempts from one address in 15 minutes, each further attempt waits 30
  seconds before it's answered.
- A failed attempt says "That password isn't right." It never says more.
- **Sign out** ends the session on the server and clears the cookie.
- Every route needs a session, except `/healthz`, the setup and sign-in routes, and the static
  files of the UI.

### Password reset

- The image provides a `fibotrail reset-password` command. You run it on the host with
  `docker exec -it fibotrail fibotrail reset-password`.
- The command asks for the new password twice and doesn't show what you type. It never takes the
  password as an argument, so the password doesn't land in the shell history.
- The same 12-character rule applies. The new password is stored as an Argon2id hash.
- The command ends every session and clears the sign-in delay.
- It works while the server runs, and the server needs no restart.
- It changes nothing but the password and the sessions. Trips and decisions are untouched.
- The sign-in screen has a **Forgot your password?** link that shows the command.

## Acceptance criteria

- `docker compose up` with a filled-in `.env` starts one container, and
  `http://localhost:11235/healthz` returns 200 on the host.
- With `IMMICH_API_KEY` removed from `.env`, the container exits and the log names
  `IMMICH_API_KEY`.
- On a new `/data` volume, the first visit shows the setup screen. After setup, a visit with no
  session shows the sign-in screen.
- A request to an API route with no session returns 401.
- A sixth failed sign-in attempt within 15 minutes takes at least 30 seconds to answer.
- After `fibotrail reset-password`, the old password fails, the new one works, and a browser
  that was signed in is asked to sign in again.
- The reset command with an 11-character password changes nothing and says why.
- The database file holds no plain-text password, and no log line holds the Immich key.
- `./scripts/check.sh` runs the .NET steps from the [quality gates spec](quality-gates.md) and
  passes.

## Open questions

None.
