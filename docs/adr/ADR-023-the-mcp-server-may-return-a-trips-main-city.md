# ADR-023: The MCP server may return a trip's main city

## Status

Accepted

## Context

[ADR-018](ADR-018-an-mcp-server-answers-questions-with-coarse-read-only-data.md) lets an MCP client
ask Fibotrail questions, and limits every tool result to coarse data: counts, dates, country,
continent, collection, and park names, and trip IDs. It leaves out every city name, because a city
is finer than a country.

That limit makes some questions impossible. A trip in a result is an ID, dates, and countries, so
"When was I last in Orlando?" or "Which trips went to Tokyo?" has no answer, and the model can only
say "trip 42, United States, 6 days".

What exists today:

- Every trip already has a **main place**, chosen by the
  [main destination](../specs/main-destination.md) rule: the city with the most distinct days that
  have an away photo, with a tie going to the city with more away photos, then to the earlier first
  photo. When no photo has a city, the main place is the state, then the country. It is stored on
  the trip and shown on the trip card and the inbox card.
- Each trip also stores its stops and route, with a center and a city name for each stop
  ([ADR-010](ADR-010-stops-and-routes-are-stored-with-the-trip.md)).
- City names come from Immich and are granular. A neighborhood can be a "city"
  ([ADR-015](ADR-015-cities-are-merged-places-and-theme-parks-keep-their-own-identity.md)).

Not known yet: how often the main city is a neighborhood instead of the city a person would name,
on the maintainer's library.

## Decision

The MCP server may return **one city per trip: its main city**. Everything else in ADR-018 still
holds: the route, the token, the read-only rule, the "added trips only" rule, the alternatives, and
the test that enforces the vocabulary. This ADR replaces only the vocabulary.

```
Trip ──▶ main destination rule ──▶ main place ──▶ is it a city? ──yes──▶ mainCity: "Orlando"
         (days, then photos)                             │
                                                         └──no───▶ mainCity: null
```

- **Which city.** The main city is the trip's main place, chosen by the main destination rule, the
  same value the trip card shows. The MCP server doesn't rank cities with a rule of its own, so the
  app gives one answer to one question.
- **Only a city.** When no photo of the trip has a city, the main place is a state or a country, and
  `mainCity` is empty. The country is already in the result.
- **Where it appears.** In `list_trips` and `get_trip`, as one field on each trip. A tool may filter
  trips by main city, matched ignoring case, so "Which trips went to Orlando?" is one call.
- **The vocabulary.** This is the whole table now, and the test enforces it:

  | Allowed | Examples |
  | --- | --- |
  | Counts and durations | 5 trips, 19 days |
  | Dates, to the day | first visit 2019-07-03 |
  | Country and continent names | Sweden, Europe |
  | Collection and park names | Disney parks, Magic Kingdom |
  | A trip's main city | Orlando |
  | Trip IDs | 42 |

  | Never returned | Why |
  | --- | --- |
  | Latitude, longitude, routes, stops, stop centers | Precise location, and the path of a trip |
  | Any city other than a trip's main city | A list of cities traces a route |
  | Stop names, states, and place names below a park | Finer than a main city |
  | Photo IDs, thumbnails, file names, camera data | Photo data |
  | Trip names and notes | Free text you typed, which can hold anything |
  | Immich URLs, keys, and user names | Secrets and identity |

## Alternatives considered

- **Every city of a trip.** A trip would list each city it visited. Set aside because the list, in
  order, is the route in words, and the maintainer wants no route to leave the network.
- **The city with the most photos.** The MCP server would rank cities by photos. Set aside because
  the main destination spec moved from photos to days on 2026-11-16, after photo counts named trips
  "Horizon West" and "Paradise". A second rule would give a city that differs from the trip card.
- **The state or country when there is no city.** `mainCity` would fall back as the main place
  does. Set aside because the country is already in the result, and a state under a field named for
  a city would mislead the model.
- **Keep cities out.** ADR-018 as it was. Set aside because a trip with only an ID, dates, and
  countries can't answer a question about a city, and one city per trip names where you went without
  how you got there.

## Consequences

- "When was I last in Orlando?" and "Which trips went to Tokyo?" can be answered with one
  `list_trips` call.
- A trip's main city leaves the home network when the client is a cloud model. The maintainer
  accepted this on 2026-11-27. The CLAUDE.md rule "never send photo data or location data to an
  outside service" is now read to mean photos, coordinates, routes, and every place name finer than
  a park, except a trip's main city.
- The vocabulary test gains one kind, `City`, allowed only on the `mainCity` field of a trip result.
  A city on any other field, such as a stop, fails the test, so a later change can't leak a route
  in words by accident.
- The main city can be a neighborhood, because Immich names it. The MCP answer then matches the
  trip card, which is the point, and a better name comes from a fix to the main destination rule,
  not here.
- A main city can be a small town where family lives, which says more about a person than a
  country does. We treat that as acceptable for a private, single-user app, as ADR-018 does for park
  visits, and revisit it if Fibotrail becomes multi-user.
- The [MCP server spec](../specs/mcp-server.md) changes its vocabulary, its trip results, and its
  "What it can't answer" list, and the code follows it.
