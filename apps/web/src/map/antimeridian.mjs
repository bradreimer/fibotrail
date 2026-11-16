// Cuts polygon rings at the antimeridian, so a flat map and a flat point-in-polygon test see the
// right shape. Intention: the Natural Earth outlines of Russia and Fiji have rings that cross
// longitude 180. Their points jump from about 179.9 East to 180 West, and drawn or tested as flat
// polygons, that edge runs across the whole world and the country covers the sea in between.
// Method: unwrap the ring so its longitudes are continuous, cut it into one piece for each
// 360 degree window, and move each piece back into -180 to 180. A ring that winds around a pole,
// like Antarctica's, can't be unwrapped into a closed ring and is left as it is: its long edge
// along the pole is right on a flat map.
//
// A plain .mjs file, so the browser build and the "npm run outlines" script share one copy.

/** Positions that are closer than this, in degrees, are the same position. */
const EPSILON = 1e-9;

/** True when a ring has an edge that jumps across the antimeridian. */
function jumps(ring) {
  for (let i = 1; i < ring.length; i++) {
    if (Math.abs(ring[i][0] - ring[i - 1][0]) > 180) return true;
  }
  return false;
}

/** Makes the longitudes of a ring continuous, so the edges that jumped across 180 now run past it. */
function unwrap(ring) {
  const out = [[ring[0][0], ring[0][1]]];
  let offset = 0;
  for (let i = 1; i < ring.length; i++) {
    const delta = ring[i][0] - ring[i - 1][0];
    if (delta > 180) offset -= 360;
    else if (delta < -180) offset += 360;
    out.push([ring[i][0] + offset, ring[i][1]]);
  }
  return out;
}

/** Keeps the part of a ring on one side of a vertical line (Sutherland-Hodgman). The ring is open. */
function clip(ring, inside, cross) {
  const out = [];
  for (let i = 0; i < ring.length; i++) {
    const current = ring[i];
    const previous = ring[(i + ring.length - 1) % ring.length];
    if (inside(current)) {
      if (!inside(previous)) out.push(cross(previous, current));
      out.push(current);
    } else if (inside(previous)) {
      out.push(cross(previous, current));
    }
  }
  return out;
}

const crossAt = (x) => (a, b) => [x, a[1] + ((b[1] - a[1]) * (x - a[0])) / (b[0] - a[0])];

/** The part of an unwrapped ring inside the 360 degree window `k`, moved back into -180 to 180. Null when nothing is left. */
function piece(ring, k) {
  const west = -180 + 360 * k;
  const east = 180 + 360 * k;
  let open = ring.slice(0, -1);
  open = clip(open, (p) => p[0] >= west - EPSILON, crossAt(west));
  open = clip(open, (p) => p[0] <= east + EPSILON, crossAt(east));
  const moved = [];
  for (const p of open) {
    const point = [p[0] - 360 * k, p[1]];
    const last = moved[moved.length - 1];
    if (!last || Math.abs(last[0] - point[0]) > EPSILON || Math.abs(last[1] - point[1]) > EPSILON)
      moved.push(point);
  }
  if (moved.length < 3 || Math.abs(area(moved)) < EPSILON) return null;
  return [...moved, moved[0]];
}

/** The signed area of an open ring, in square degrees. */
function area(ring) {
  let sum = 0;
  for (let i = 0; i < ring.length; i++) {
    const [x1, y1] = ring[i];
    const [x2, y2] = ring[(i + 1) % ring.length];
    sum += x1 * y2 - x2 * y1;
  }
  return sum / 2;
}

/**
 * One polygon, as a list of rings with the outer ring first, as a list of polygons. A polygon
 * that never crosses the antimeridian comes back as it was.
 */
export function cutPolygon(rings) {
  if (!rings.some(jumps)) return [rings];
  const unwrapped = rings.map(unwrap);
  // A ring that doesn't close once unwrapped winds around a pole. Leave the polygon as it is.
  if (unwrapped.some((ring) => Math.abs(ring[ring.length - 1][0] - ring[0][0]) > EPSILON))
    return [rings];

  const longitudes = unwrapped[0].map((p) => p[0]);
  const first = Math.floor((Math.min(...longitudes) + 180) / 360);
  const last = Math.ceil((Math.max(...longitudes) + 180) / 360) - 1;
  const polygons = [];
  for (let k = first; k <= last; k++) {
    const outer = piece(unwrapped[0], k);
    if (!outer) continue;
    const holes = unwrapped
      .slice(1)
      .map((ring) => piece(ring, k))
      .filter(Boolean);
    polygons.push([outer, ...holes]);
  }
  return polygons.length > 0 ? polygons : [rings];
}

/** Cuts every polygon of a list of polygons. */
export function cutPolygons(polygons) {
  return polygons.flatMap(cutPolygon);
}

/** A GeoJSON Polygon or MultiPolygon with its rings cut at the antimeridian. */
export function cutGeometry(geometry) {
  const polygons = geometry.type === 'Polygon' ? [geometry.coordinates] : geometry.coordinates;
  const cut = cutPolygons(polygons);
  return cut.length === 1
    ? { type: 'Polygon', coordinates: cut[0] }
    : { type: 'MultiPolygon', coordinates: cut };
}
