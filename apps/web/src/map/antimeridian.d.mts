type Position = number[];
type Ring = Position[];
type Polygon = Ring[];

/** One polygon (outer ring first) as the polygons that its rings make once cut at the antimeridian. */
export function cutPolygon(rings: Polygon): Polygon[];

/** Cuts every polygon of a list at the antimeridian. */
export function cutPolygons(polygons: Polygon[]): Polygon[];

/** A GeoJSON Polygon or MultiPolygon, cut at the antimeridian. */
export function cutGeometry(
  geometry: GeoJSON.Polygon | GeoJSON.MultiPolygon,
): GeoJSON.Polygon | GeoJSON.MultiPolygon;
