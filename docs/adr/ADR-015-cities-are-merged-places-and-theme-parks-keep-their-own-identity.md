# ADR-015: Cities are merged places, and theme parks keep their own identity

## Status

Proposed

## Context

[#48](https://github.com/bradreimer/fibotrail/issues/48) asks for the life map to zoom to cities,
and the [Stats screen spec](../specs/stats-screen.md) leaves out a count of cities because of one
problem: Immich's city names are granular. A neighborhood can be a "city", so Tokyo could count as
ten cities, and a count would overstate how many you've seen.

What the app stores today is enough to work from. Each added trip stores its stops, and each stop
has a center, a place name from Immich, a photo count, and first and last times
([ADR-010](ADR-010-stops-and-routes-are-stored-with-the-trip.md)). Stop names come from the city
with the most photos at the stop, and the main destination already picks the city with the most days
([main destination](../specs/main-destination.md)).

Theme parks are a second problem. A park such as Magic Kingdom sits inside a metropolitan area, so
merging by distance would fold it into "Orlando". But [collections](../specs/collections.md) treat a
park as its own place with its own days and trips, and you want to see "Universal Studios Florida"
and not only "Orlando".

Not known yet: how many places the merge gives on the maintainer's 46 added trips, and whether 25 km
is the right distance. The maintainer's library has been used for similar measurements before, such
as `fibotrail stops`.

## Decision

A **place** is a group of stops from added trips that lie close together. A theme park is a place of
its own and is never grouped with a city.

- **Grouping.** Stops of added trips are grouped with the same function that groups photos into
  stops, `Geo.Group`, with a radius of **25 km**. The stops are taken oldest first, so the same
  input always gives the same groups. The radius starts at 25 km, the same distance the life map
  uses for a stop just off a coast, and is checked on real data before release. It isn't a setting.
- **Naming.** A place takes the name of the stop in its group with the most photos, and the earliest
  one on a tie. No outside list of cities is used and nothing leaves the device.
- **Counting.** A place's trips are the distinct added trips with a stop in it, and its first visit
  is the start date of the earliest of those trips. The count of cities is the number of places that
  aren't parks.
- **Parks keep their identity.** A stop that falls inside an open place of a collection, by the
  collection's boundary and buffer, is a **park place**. It takes the park's name, stays out of the
  city grouping, and doesn't count toward the count of cities. Collections and Stats count it, as in
  [ADR-013](ADR-013-park-days-are-derived-from-stored-photo-positions.md). The city around a park is
  formed by the trip's other stops, such as a hotel or an airport.
- **Derived, not stored.** Places are worked out on the server on each request from the stored
  stops, with no table, as in [ADR-011](ADR-011-visits-are-derived-from-added-trips-on-the-server.md)
  and [ADR-014](ADR-014-stats-views-are-self-contained-modules.md). Photos on the way aren't in the
  stored stops, so a layover city doesn't count.
- **Regions outside Canada and the USA are not decided here.** They need boundary data, which is a
  question of size, and the choice of countries. The recommendation is the same short list of
  countries as [ADR-012](ADR-012-detail-regions-are-bundled-outlines-for-a-short-list-of-countries.md),
  extended by a few the maintainer chooses. This ADR stays `Proposed` until that is answered, and
  the regions work waits for it.

## Alternatives considered

- **Use Immich's names as they are.** No new logic, but the count is wrong whenever a trip has
  stops in several neighborhoods of one city. Set aside.
- **Snap each stop to a bundled list of real cities.** Names and counts would be consistent, but it
  adds data, and a small town that isn't in the list would be missed or snapped to a far city. Set
  aside for now. It stays an option if the distance rule gives poor results.
- **Merge parks into their city.** The simplest rule, but "Orlando" would hide the days you spent in
  each park, and Collections would need to say the same thing twice. Set aside.
- **Let a park also count as a city.** It would keep one rule, but a trip made only of park days
  would add a city that has no hotel or street in it. Set aside, with the cost below.

## Consequences

- A count of cities and a city layer on the life map become possible without new data.
- The count follows the radius: a larger one merges two nearby cities, such as Seattle and Tacoma,
  and a smaller one splits one metropolitan area. 25 km is a first guess, measured before release.
- A trip made entirely of park stops, such as a week at Walt Disney World with no stop elsewhere,
  adds no city. The park and its trip still show in Collections and Stats. If that reads wrongly on
  real data, a park could also add its Immich city name. Nothing here prevents that change.
- Each request groups every stop of every added trip. With tens of trips and a few hundred stops
  this should be fast, but that is an estimate, and the first implementation measures it and
  records the result here.
- Collection files decide what is a park, so adding a collection changes which stops are parks
  without a code change. A stop right on a boundary can go either way, and the buffer is the only
  tolerance.
