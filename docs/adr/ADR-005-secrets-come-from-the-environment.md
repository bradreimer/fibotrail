# ADR-005: Secrets come from the environment, starting with .env

## Status

Accepted

## Context

Fibotrail holds two kinds of secret: the Immich API key, and its own sign-in material (the
password hash and the key that protects session cookies). A leaked Immich key exposes a personal
photo library.

The design and branding document supplied the Immich key as a Docker secret file. The maintainer
wants secrets handled through proper authentication policies, starting with a `.env` file.

## Decision

Secrets reach the app as environment variables, and a `.env` file is where they start.

- Docker Compose loads `.env` from the folder that holds the compose file and passes the values to
  the container. The same file is used for local development.
- `.env` is never committed. `.gitignore` ignores it.
- `.env.example` is committed. It lists every variable the app reads. Variables that hold a secret
  have an empty value.
- The app reads configuration through ASP.NET Core's configuration system, so any source of
  environment variables works without a code change.
- A missing required secret stops the app at startup with a message that names the variable.
- No secret is written to a log, returned by an API, or sent to the browser. The server fetches
  thumbnails from Immich on the browser's behalf.

The authentication policies are:

| Credential | Policy |
| --- | --- |
| Immich API key | A dedicated key with read permissions only: `asset.read`, `asset.view`, `album.read`, and `asset.statistics` |
| Fibotrail password | Set on first run, stored only as an Argon2id hash in the database |
| Session | HttpOnly, Secure, SameSite cookie, with protection against cross-site request forgery |
| Sign-in attempts | Rate limited, with a lockout delay after repeated failures |

## Alternatives considered

- **Docker secrets from the start.** The design document's approach. A secret file is harder to
  leak through `docker inspect` than an environment variable. Set aside as the starting point
  because `.env` is simpler to set up and is how Immich itself is configured. It remains the next
  step.
- **Secrets in a configuration file inside the data volume.** Set aside because the secret would
  sit next to the database in every backup.

## Consequences

- Setup is: copy `.env.example` to `.env`, then fill in the Immich key.
- Anyone who can run `docker inspect` on the host can read the key. That's acceptable for a home
  server where the host is already trusted.
- Moving to Docker secrets later needs no change to how the app reads configuration, only an
  added `_FILE` variant for each secret.
- Tests fail if `.env` is tracked, if `.gitignore` stops ignoring it, or if `.env.example`
  holds a value for a secret.
