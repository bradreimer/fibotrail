/**
 * Today's date in this device's time zone, as YYYY-MM-DD. A daily puzzle follows your own day, not
 * the server's. See docs/specs/games.md#results-and-stats.
 */
export function localDate(now: Date = new Date()): string {
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

/** The first day of games, puzzle #1, as on the server. See docs/specs/games.md#sharing. */
const FIRST_DAY = Date.UTC(2026, 10, 19);

/** The puzzle number of a date (YYYY-MM-DD): 1 on 2026-11-19, 2 the day after, and so on. */
export function puzzleNumber(date: string): number {
  const [year, month, day] = date.split('-').map(Number);
  const days = (Date.UTC(year ?? 2026, (month ?? 11) - 1, day ?? 19) - FIRST_DAY) / 86_400_000;
  return Math.round(days) + 1;
}
