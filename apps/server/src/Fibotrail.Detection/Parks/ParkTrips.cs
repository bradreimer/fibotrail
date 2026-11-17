namespace Fibotrail.Detection.Parks;

/// <summary>A trip that visits some of a set of parks, with the names of those parks.</summary>
/// <param name="FirstDay">The earliest day of the trip in any of the parks. Marked-only visits use the trip's first day.</param>
public sealed record TripParks(int TripId, string Name, DateOnly FirstDay, IReadOnlyList<string> Parks);

/// <summary>
/// Counts distinct trips across several parks. A trip that visits two parks counts once, so the
/// count of a resort or a collection is never the sum of its parks. It reads the visits that
/// <see cref="ParkDays"/> already worked out, so every level agrees with the park's own trips.
/// See docs/specs/collections.md.
/// </summary>
public static class ParkTrips
{
    /// <summary>The number of distinct trips that visit at least one of the parks.</summary>
    public static int Distinct(IEnumerable<ParkStats> parks)
    {
        ArgumentNullException.ThrowIfNull(parks);
        return parks.SelectMany(park => park.Trips).Select(visit => visit.TripId).Distinct().Count();
    }

    /// <summary>
    /// Each distinct trip that visits at least one of the parks, with the parks it visited in the
    /// order given, newest first. A trip that visits two parks appears once, so the list is as long
    /// as <see cref="Distinct"/>.
    /// </summary>
    public static IReadOnlyList<TripParks> ByTrip(IEnumerable<ParkStats> parks)
    {
        ArgumentNullException.ThrowIfNull(parks);
        return [.. parks
            .SelectMany(park => park.Trips.Select(visit => (Visit: visit, Park: park.Place.Name)))
            .GroupBy(item => item.Visit.TripId)
            .Select(group => new TripParks(
                group.Key,
                group.First().Visit.Name,
                group.Min(item => item.Visit.FirstDay),
                [.. group.Select(item => item.Park)]))
            .OrderByDescending(trip => trip.FirstDay)
            .ThenBy(trip => trip.TripId)];
    }
}
