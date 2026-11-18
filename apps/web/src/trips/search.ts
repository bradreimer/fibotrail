import type { TripSummary } from './TripCard';

/** Lowercase, with accents removed, so "Île" and "ile" are the same word. */
export function normalize(text: string): string {
  return text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();
}

/** Every year a trip touches, so a trip from Dec 28, 2026 to Jan 3, 2027 is in both. */
function yearsOf(trip: TripSummary): string[] {
  const first = Number(trip.startDate.slice(0, 4));
  const last = Number(trip.endDate.slice(0, 4));
  return Array.from({ length: last - first + 1 }, (_, index) => String(first + index));
}

/** The text search looks in: the name, the destination, the years, and the keywords. */
export function searchTextOf(trip: TripSummary): string {
  return normalize([trip.name, trip.place ?? '', ...yearsOf(trip), ...trip.keywords].join('\n'));
}

/**
 * The trips that match a search. A trip matches when every word of the search appears in its
 * text, so "magic 2027" finds a 2027 trip to Magic Kingdom. An empty search matches every trip.
 */
export function searchTrips(trips: readonly TripSummary[], query: string): TripSummary[] {
  const words = normalize(query).split(/\s+/).filter(Boolean);
  if (words.length === 0) return [...trips];
  return trips.filter((trip) => {
    const text = searchTextOf(trip);
    return words.every((word) => text.includes(word));
  });
}
