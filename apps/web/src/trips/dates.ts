const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

interface Day {
  year: number;
  month: number;
  day: number;
}

/** Reads a date such as "2027-03-15". It has no time and no zone, so none is applied. */
function parse(date: string): Day {
  const [year, month, day] = date.split('-').map(Number) as [number, number, number];
  return { year, month, day };
}

/**
 * A date range in the fewest words that stay clear, such as "Mar 15–24, 2027",
 * "Mar 28–Apr 3, 2027", or "Dec 28, 2026–Jan 3, 2027". The year is always shown, because a
 * library holds trips from many years.
 */
export function formatDateRange(start: string, end: string): string {
  const from = parse(start);
  const to = parse(end);
  const fromMonth = MONTHS[from.month - 1];
  const toMonth = MONTHS[to.month - 1];
  if (from.year !== to.year) {
    return `${fromMonth} ${from.day}, ${from.year}–${toMonth} ${to.day}, ${to.year}`;
  }
  if (from.month !== to.month) return `${fromMonth} ${from.day}–${toMonth} ${to.day}, ${to.year}`;
  if (from.day !== to.day) return `${fromMonth} ${from.day}–${to.day}, ${to.year}`;
  return `${fromMonth} ${from.day}, ${from.year}`;
}

/** A single day, such as "Monday, March 15, 2027". */
export function formatDay(date: string): string {
  const { year, month, day } = parse(date);
  return new Intl.DateTimeFormat('en-US', {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(year, month - 1, day)));
}

const number = new Intl.NumberFormat('en-US');

/** A count with its noun, such as "1 photo" or "312 photos". */
export function countOf(count: number, singular: string, plural = `${singular}s`): string {
  return `${number.format(count)} ${count === 1 ? singular : plural}`;
}
