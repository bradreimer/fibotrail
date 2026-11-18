# ADR-015: Cities are merged places, and theme parks keep their own identity

## Status

Accepted

## Context

[#48](https://github.com/bradreimer/fibotrail/issues/48) asks for the life map to zoom to cities,
and the Stats screen spec (removed in #151) leaves out a count of cities because of one
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
is the right distance, since the first version was only tested on made-up stops. The maintainer's library has been used for similar measurements before, such
as `fibotrail stops`.

## Decision

A **place** is a group of stops from added trips that lie close together. A theme park is a place of
its own and is never grouped with a city.

- **Grouping.** Stops of added trips are grouped with the same function that groups photos into
  stops, `Geo.Group`, with a radius of **25 km**. The stops are taken oldest first, so the same
  input always gives the same groups. The radius starts at 25 km, the same distance the life map
  uses for a stop just off a coast, and is checked on real data before release. It isn't a setting.
- **Naming.** A place takes the stop name found on the most distinct trips in its group, then the
  name with the most photos, then the earliest. No outside list of cities is used and nothing leaves
  the device. The first rule was "the stop with the most photos", and the first run on real data
  showed it names a metro area after a suburb (see the result below).
- **Stops with no city name.** Immich leaves the city empty in open country and gives the country
  instead, such as "Canada". A stop whose name is blank, or is the country's name or its first words
  ("United States" for "United States of America"), has no city name. It still joins a nearby city
  but never names one, and a group made only of such stops isn't a city.
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
- **Regions stay at Canada and the USA.** The maintainer decided on 2026-11-16 that every other
  country is a single unit, with no region outlines. That is the short list of
  [ADR-012](ADR-012-detail-regions-are-bundled-outlines-for-a-short-list-of-countries.md), so no
  new boundary data is bundled and the app doesn't grow. A country outside the two shows as one
  fill, and its cities show as pins when the map is zoomed in.

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

### Result of the first run

Run on 2026-11-17 on the maintainer's library with `fibotrail places`: 52 added trips and 288
stops. The grouping took 4.9 ms at 25 km, so speed isn't a concern.

| Radius | Cities | Parks | Time |
| --- | --- | --- | --- |
| 10 km | 116 | 10 | 11.8 ms |
| 25 km | 89 | 10 | 4.9 ms |
| 50 km | 68 | 10 | 3.1 ms |

- **25 km stays.** It merges one metro area correctly (London, San Francisco, Washington DC,
  Colmar). 10 km splits them, such as Etobicoke from Toronto. 50 km merges separate towns, such as
  Whistler with Squamish.
- **Two problems that aren't about the radius.** About 10 of the 89 cities were country names
  ("Canada" five times, "United States") or "Unnamed place". And the most-photos rule named Paris
  "Neuilly-sur-Seine", Washington "Woodley Park", and Victoria "Metchosin". The two rules above
  answer them. Whether the new naming rule picks the right name for every city isn't known until
  `fibotrail places` is run again.
- **The merge list is by name.** Two stops with the same name far apart, such as the two
  Williamsburgs, show up as one name in the report.

### Result of the second run

Run on 2026-11-17 after the two rules above: 52 trips, 288 stops.

| Radius | Cities | Parks | Time |
| --- | --- | --- | --- |
| 10 km | 104 | 10 | 8.8 ms |
| 25 km | 80 | 10 | 5.3 ms |
| 50 km | 60 | 10 | 4.0 ms |

- **Country names are gone.** No "Canada", "United States", or "Unnamed place" is left as a city.
- **Naming is better in some places and not others.** Victoria was "Metchosin" and is now
  "Victoria". Washington is "Downtown DC", not "Washington". Paris is still "Neuilly-sur-Seine" and
  Saskatoon is still "Central Business District". Orlando got worse: it is now "Celebration",
  because the maintainer stayed there on more trips than in Orlando itself.
- **Why.** Trips and photos don't say which stop is the main city. Only outside data, such as
  population, would. About 5 of 80 names are off.
- **Decision.** On 2026-11-17 the maintainer chose to leave the names as they are. A rename by hand
  (stored apart from detected data, as in ADR-002) and a bundled city list stay open if the names
  bother anyone later.
- **Same name, two places.** Assiniboia, Kindersley, and Maple Creek each appear twice, and so does
  Williamsburg. These are the same name at places more than 25 km apart, so they stay separate.
