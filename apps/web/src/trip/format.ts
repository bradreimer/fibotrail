const number = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const decimal = new Intl.NumberFormat('en-US', { maximumFractionDigits: 1 });

/** A distance in kilometers: one decimal under 10 km, whole numbers above. */
export function formatKm(km: number): string {
  return `${km < 10 ? decimal.format(km) : number.format(km)} km`;
}

/** The length of a video, such as "0:09" or "5:23". */
export function formatDuration(ms: number): string {
  const seconds = Math.round(ms / 1000);
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
}

/** A local wall-clock time such as "2027-03-16T23:30:00", shown as it is, with no zone applied. */
function parts(localTime: string) {
  const [date, time = '00:00'] = localTime.split('T') as [string, string?];
  const [year, month, day] = date.split('-').map(Number) as [number, number, number];
  const [hour, minute] = time.split(':').map(Number) as [number, number];
  return new Date(Date.UTC(year, month - 1, day, hour, minute));
}

/** "Tuesday, March 16, 2027 at 11:30 PM", in the local time of the place. */
export function formatPhotoTime(localTime: string): string {
  return new Intl.DateTimeFormat('en-US', {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    timeZone: 'UTC',
  }).format(parts(localTime));
}

/** A day heading, such as "Day 3 · Wed, Mar 17 · Orlando". */
export function dayHeading(number: number, date: string, place: string | null): string {
  const label = new Intl.DateTimeFormat('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  }).format(parts(date));
  return [`Day ${number}`, label, place].filter(Boolean).join(' · ');
}
