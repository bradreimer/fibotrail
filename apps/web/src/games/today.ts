/**
 * Today's date in this device's time zone, as YYYY-MM-DD. A daily puzzle follows your own day, not
 * the server's. See docs/specs/games.md#results-and-stats.
 */
export function localDate(now: Date = new Date()): string {
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}
