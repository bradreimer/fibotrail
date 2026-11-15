# ADR-009: A bundled Natural Earth outline is the base map

## Status

Accepted

## Context

The home location step, the route preview in the inbox, and the trip page each need a map. A map
needs a base layer to draw under the pins and the route.

Most web maps load their base layer as tiles from a map service. That sends the area you look at,
and so roughly where you've been, to that service. The design and branding document sets "Private
by default" as a principle: the app makes no outside calls unless you turn them on. The
[app shell spec](../specs/app-shell.md) requires that the browser talks only to Fibotrail.

The [roadmap](../roadmap.md#decisions-the-mvp-still-needs) listed the base map source as a
decision the MVP still needed.

## Decision

Fibotrail draws its maps with MapLibre GL JS on a base map that is part of the app.

- **The data** is the Natural Earth country outlines at 1:50 million scale, from the `world-atlas`
  npm package. Natural Earth is in the public domain. The file is 756 KB, and about 240 KB when
  sent compressed.
- **The style** has a background for water, a fill for land, and a line for borders. Its colors
  come from the design tokens, so the map follows light and dark mode.
- **No labels.** A label needs font files in a format that MapLibre loads from a server. Pins and
  the text next to the map name the places.
- **No tiles and no outside host.** The style has no tile source, no font address, and no image
  address. A test fails if one is added.
- **Loaded on first use.** MapLibre is 1.1 MB, larger than the rest of the app. It loads when a
  screen first shows a map.
- **The MapLibre worker is a file of the app.** By default MapLibre builds its worker from a blob
  address, which the Content Security Policy doesn't allow. Fibotrail gives it the address of the
  worker file.

An optional tile address, which you'd turn on in Settings, isn't part of this decision. It needs
its own work item, because the Content Security Policy must then allow that one host.

## Alternatives considered

- **Tiles from a public map service.** A detailed map with streets and labels, for no storage.
  Set aside because every map view would tell an outside service where you've been.
- **Self-hosted vector tiles of the whole world.** Full detail with no outside calls. Set aside
  because the data is tens of gigabytes, far more than the rest of Fibotrail.
- **Natural Earth at 1:10 million scale.** Coasts that hold up when you zoom in to a region. Set
  aside for now because the file is 3.7 MB, five times the size, and still shows no streets.
- **No base map: pins and a line on a plain background.** The least data. Set aside because a
  route with no coast or border around it doesn't say where in the world it is.

## Consequences

- With the default setup, Fibotrail makes no outside request for a map.
- The map is coarse. It shows which country and which coast a trip was on. At the scale of one
  city, it shows the stops and the route on plain land, with no streets. A single point is shown
  from far enough away that the coast around it is recognizable.
- The life map, planned for phase 4, can fill visited countries from the same file.
- Small islands and narrow straits are missing or simplified at this scale. A pin can look as if
  it sits in the water.
- The first map view downloads about 600 KB of code and data, compressed. After that, the
  service worker serves it from its cache.
