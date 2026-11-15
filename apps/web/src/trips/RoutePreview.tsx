import type { Position } from '../map/mapStyle';

const WIDTH = 160;
const HEIGHT = 100;
const PADDING = 16;

/** Places the points in the drawing: north up, with the true shape at the route's latitude. */
export function projectRoute(points: readonly Position[]): { x: number; y: number }[] {
  if (points.length === 0) return [];
  const middle = points.reduce((sum, point) => sum + point.latitude, 0) / points.length;
  // A degree of longitude is shorter than a degree of latitude, by the cosine of the latitude.
  const squeeze = Math.cos((middle * Math.PI) / 180);
  const xs = points.map((point) => point.longitude * squeeze);
  const ys = points.map((point) => -point.latitude);
  const minX = Math.min(...xs);
  const minY = Math.min(...ys);
  const spanX = Math.max(...xs) - minX;
  const spanY = Math.max(...ys) - minY;
  const scale = Math.min(
    (WIDTH - 2 * PADDING) / (spanX || 1),
    (HEIGHT - 2 * PADDING) / (spanY || 1),
  );
  // With one stop, or stops in a straight line, the drawing is centered.
  const offsetX = (WIDTH - spanX * scale) / 2;
  const offsetY = (HEIGHT - spanY * scale) / 2;
  return xs.map((x, index) => ({
    x: Math.round((offsetX + (x - minX) * scale) * 10) / 10,
    y: Math.round((offsetY + (ys[index]! - minY) * scale) * 10) / 10,
  }));
}

/**
 * A small drawing of a route: the stops and the lines between them, in the colors of the trip
 * page's map. It's a plain SVG and not a map, because a browser allows only about 16 maps on one
 * page, and the inbox can hold more suggestions than that.
 */
export function RoutePreview({ route }: { route: readonly Position[] }) {
  const points = projectRoute(route);
  const line = points.map((point) => `${point.x},${point.y}`).join(' ');
  const stops = route.length === 1 ? '1 stop' : `${route.length} stops`;
  return (
    <svg
      className="route-preview"
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      role="img"
      aria-label={`Photo route with ${stops}`}
    >
      {points.length > 1 && (
        <>
          <polyline className="route-preview__outline" points={line} />
          <polyline className="route-preview__line" points={line} />
        </>
      )}
      {points.slice(1).map((point, index) => (
        <circle key={index} className="route-preview__stop" cx={point.x} cy={point.y} r="5" />
      ))}
      {points[0] && (
        <circle className="route-preview__start" cx={points[0].x} cy={points[0].y} r="6" />
      )}
    </svg>
  );
}
