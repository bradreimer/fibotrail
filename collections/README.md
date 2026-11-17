# Collections

A collection is a named set of places, such as "Disney parks". Each collection is one YAML file in
this folder, and each place has a GeoJSON boundary in `boundaries/`. Adding a collection needs no
code. The rules are in [docs/specs/collections.md](../docs/specs/collections.md) and
[ADR-003](../docs/adr/ADR-003-collections-are-data-files.md).

**Status:** `disney-parks.yaml` has all 12 Disney parks, and `universal-parks.yaml` has all 7 Universal
parks. Each boundary comes from OpenStreetMap and should be checked by hand against satellite imagery.
To add a park, export its outline from [Overpass Turbo](https://overpass-turbo.eu) as GeoJSON, save it
as `boundaries/<id>.geojson`, and add its entry to the YAML file.

## File format

```yaml
id: disney-parks                 # lowercase, unique across files
name: Disney parks
description: Every Disney theme park in the world
rule:
  min_photos: 3                  # photos of one trip needed inside a place to count it (default 3)
  buffer_m: 50                   # allowance for GPS error, in meters (default 50)
places:
  - id: magic-kingdom            # unique across all files
    name: Magic Kingdom
    group: Walt Disney World Resort
    status: open                 # open or planned (default open)
    boundary: boundaries/magic-kingdom.geojson
    buffer_m: 25                 # optional: this place's own buffer
```

A boundary is a GeoJSON `Polygon` or `MultiPolygon`, or a `Feature` or `FeatureCollection` of them.
Each ring must be closed. A `planned` place shows as "Coming" and matches no photo.

The server checks every file when it starts. A missing boundary, a ring that isn't closed, a
duplicate `id`, or a bad value stops the app with a message that names the file. Nothing is skipped
silently.

## Attribution

Park boundaries come from OpenStreetMap and are © OpenStreetMap contributors, under the
[Open Database License](https://www.openstreetmap.org/copyright). The app shows this credit wherever
it shows a collection.
