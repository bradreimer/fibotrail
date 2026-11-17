namespace Fibotrail.Detection.Parks;

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
}
