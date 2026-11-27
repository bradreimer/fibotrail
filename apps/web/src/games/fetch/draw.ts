import type { Course, Flight } from './flight';

/** Hundredths of a pixel in one pixel, as in the course. */
const UNIT = 100;

/**
 * The world's colors: the brand palette from tokens.css. The world is a picture, so it keeps the
 * same colors in dark mode, as a photo does.
 */
const COLOR = {
  skyTop: '#9fc9d8',
  skyBottom: '#f7f3ea',
  hills: '#7bae7f',
  far: '#a9cbb0',
  gate: '#146560',
  gateEdge: '#0b3c49',
  cap: '#e0a526',
  ground: '#0b3c49',
  groundText: '#f7f3ea',
  treat: '#e0a526',
  flag: '#e4684a',
} as const;

/** What the drawing needs besides the flight. */
export interface Scene {
  course: Course;
  /** The schnauzer sprite: 4 frames of 64 px, side by side. Null until it loads. */
  sprite: HTMLImageElement | null;
  /** Treat photos by ID, once loaded. A treat with no photo yet is a gold circle. */
  photos: ReadonlyMap<string, HTMLImageElement>;
}

/**
 * Draws one frame. The flight's numbers are in hundredths of a pixel; the canvas is set up in
 * world pixels, 400 by 700. `seconds` drives the wing flap and changes nothing in the physics.
 */
export function drawFrame(
  ctx: CanvasRenderingContext2D,
  scene: Scene,
  flight: Flight,
  seconds: number,
) {
  const { course } = scene;
  const { physics } = course;
  const width = physics.worldWidth / UNIT;
  const height = physics.worldHeight / UNIT;
  const ground = physics.ground / UNIT;
  const scroll = flight.scroll / UNIT;

  const sky = ctx.createLinearGradient(0, 0, 0, ground);
  sky.addColorStop(0, COLOR.skyTop);
  sky.addColorStop(1, COLOR.skyBottom);
  ctx.fillStyle = sky;
  ctx.fillRect(0, 0, width, ground);

  // Two rows of hills, the far one slower, which reads as depth.
  hills(ctx, width, ground, scroll * 0.2, 70, 0.011, COLOR.far);
  hills(ctx, width, ground, scroll * 0.5, 40, 0.019, COLOR.hills);

  const gateWidth = physics.gateWidth / UNIT;
  for (const gate of course.gates) {
    const x = gate.x / UNIT - scroll;
    if (x > width || x + gateWidth < 0) continue;
    post(ctx, x, 0, gateWidth, gate.gapTop / UNIT, 'top');
    post(ctx, x, gate.gapBottom / UNIT, gateWidth, ground - gate.gapBottom / UNIT, 'bottom');
  }

  const radius = physics.treatRadius / UNIT;
  course.treats.forEach((treat, index) => {
    if (flight.caught.has(index)) return;
    const x = treat.x / UNIT - scroll;
    if (x < -radius || x > width + radius) return;
    const y = treat.y / UNIT;
    const photo = scene.photos.get(treat.photoId);
    ctx.save();
    ctx.beginPath();
    ctx.arc(x, y, radius, 0, Math.PI * 2);
    ctx.fillStyle = COLOR.treat;
    ctx.fill();
    if (photo) {
      ctx.clip();
      // Cover the circle with the middle of the photo.
      const side = Math.min(photo.naturalWidth, photo.naturalHeight);
      ctx.drawImage(
        photo,
        (photo.naturalWidth - side) / 2,
        (photo.naturalHeight - side) / 2,
        side,
        side,
        x - radius,
        y - radius,
        radius * 2,
        radius * 2,
      );
    }
    ctx.restore();
    ctx.beginPath();
    ctx.arc(x, y, radius, 0, Math.PI * 2);
    ctx.lineWidth = 3;
    ctx.strokeStyle = COLOR.treat;
    ctx.stroke();
  });

  const finish = course.finishX / UNIT - scroll;
  if (finish < width + 20) {
    ctx.fillStyle = COLOR.gateEdge;
    ctx.fillRect(finish, ground - 120, 4, 120);
    ctx.fillStyle = COLOR.flag;
    ctx.beginPath();
    ctx.moveTo(finish + 4, ground - 120);
    ctx.lineTo(finish + 40, ground - 105);
    ctx.lineTo(finish + 4, ground - 90);
    ctx.fill();
  }

  ctx.fillStyle = COLOR.ground;
  ctx.fillRect(0, ground, width, height - ground);
  ctx.fillStyle = COLOR.groundText;
  ctx.font = '600 16px Inter Variable, system-ui, sans-serif';
  ctx.textAlign = 'center';
  for (const gate of course.gates) {
    const x = gate.x / UNIT - scroll + gateWidth / 2;
    if (x > -40 && x < width + 40) ctx.fillText(`Day ${gate.day}`, x, ground + 34);
  }

  dog(
    ctx,
    scene.sprite,
    physics.dogX / UNIT,
    flight.y / UNIT,
    physics.dogSize / UNIT,
    flight,
    seconds,
  );
}

function hills(
  ctx: CanvasRenderingContext2D,
  width: number,
  ground: number,
  offset: number,
  rise: number,
  wave: number,
  color: string,
) {
  ctx.fillStyle = color;
  ctx.beginPath();
  ctx.moveTo(0, ground);
  for (let x = 0; x <= width; x += 8) {
    const t = (x + offset) * wave;
    ctx.lineTo(x, ground - rise * (0.6 + 0.4 * Math.sin(t) * Math.cos(t * 0.37)));
  }
  ctx.lineTo(width, ground);
  ctx.fill();
}

/** A trail post with a gold cap at the gap's edge. */
function post(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  width: number,
  height: number,
  side: 'top' | 'bottom',
) {
  if (height <= 0) return;
  ctx.fillStyle = COLOR.gate;
  ctx.fillRect(x + 6, y, width - 12, height);
  ctx.fillStyle = COLOR.gateEdge;
  ctx.fillRect(x + 6, y, 4, height);
  const capY = side === 'top' ? y + height - 18 : y;
  ctx.fillStyle = COLOR.cap;
  ctx.fillRect(x, capY, width, 18);
}

function dog(
  ctx: CanvasRenderingContext2D,
  sprite: HTMLImageElement | null,
  x: number,
  y: number,
  size: number,
  flight: Flight,
  seconds: number,
) {
  // Tilt up after a flap and nose down when falling, up to about 25°.
  const tilt = Math.max(-0.45, Math.min(0.45, flight.velocity / 1600));
  ctx.save();
  ctx.translate(x + size / 2, y + size / 2);
  ctx.rotate(flight.end === 'finish' || flight.tick === 0 ? 0 : tilt);
  if (sprite) {
    const frame = flight.end ? 0 : Math.floor(seconds * 10) % 4;
    ctx.drawImage(sprite, frame * 64, 0, 64, 64, -size / 2, -size / 2, size, size);
  } else {
    ctx.fillStyle = COLOR.gateEdge;
    ctx.fillRect(-size / 2 + 6, -12, size - 12, 24);
  }
  ctx.restore();
}
