namespace Fibotrail.Detection;

/// <summary>Something stored that a detected trip might be: a trip row, or a decision.</summary>
/// <typeparam name="TKey">What identifies the stored item.</typeparam>
public sealed record StoredTrip<TKey>(TKey Key, string Fingerprint, DateOnly StartDate, DateOnly EndDate);

/// <summary>
/// Rule 11: recognizes a detected trip as one that is already stored, so that a rescan never asks
/// the same question twice. See ADR-002.
/// </summary>
public static class Reconciler
{
    /// <summary>
    /// Pairs each detected trip with a stored item, or with none. A fingerprint match comes first.
    /// When the fingerprint changed, because an early photo arrived late and moved the start date,
    /// the trip still matches a stored item whose dates cover more than half of its days. A stored
    /// item is matched at most once.
    /// </summary>
    /// <returns>One entry for each detected trip, in the same order. The entry is the key of the match, or null.</returns>
    public static IReadOnlyList<TKey?> Match<TKey>(IReadOnlyList<DetectedTrip> detected, IEnumerable<StoredTrip<TKey>> stored)
        where TKey : struct
    {
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(stored);

        var unused = stored.ToList();
        var matches = new TKey?[detected.Count];

        for (var index = 0; index < detected.Count; index++)
        {
            var same = unused.FirstOrDefault(candidate => candidate.Fingerprint == detected[index].Fingerprint);
            if (same is not null)
            {
                matches[index] = same.Key;
                unused.Remove(same);
            }
        }

        for (var index = 0; index < detected.Count; index++)
        {
            if (matches[index] is not null)
            {
                continue;
            }

            var trip = detected[index];
            var overlapping = unused
                .Select(candidate => (Candidate: candidate, Days: OverlapDays(trip.StartDate, trip.EndDate, candidate.StartDate, candidate.EndDate)))
                .Where(pair => pair.Days * 2 > trip.Days)
                .OrderByDescending(pair => pair.Days)
                .ThenBy(pair => pair.Candidate.StartDate)
                .Select(pair => pair.Candidate)
                .FirstOrDefault();
            if (overlapping is not null)
            {
                matches[index] = overlapping.Key;
                unused.Remove(overlapping);
            }
        }

        return matches;
    }

    /// <summary>The number of calendar days that two date ranges share.</summary>
    public static int OverlapDays(DateOnly start1, DateOnly end1, DateOnly start2, DateOnly end2)
    {
        var start = start1 > start2 ? start1 : start2;
        var end = end1 < end2 ? end1 : end2;
        return Math.Max(0, end.DayNumber - start.DayNumber + 1);
    }
}
