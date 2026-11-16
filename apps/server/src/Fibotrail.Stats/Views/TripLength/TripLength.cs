namespace Fibotrail.Stats.Views.TripLength;

/// <summary>One bin of the histogram.</summary>
public sealed record LengthBin(string Label, int MinDays, int? MaxDays, int Count);

/// <summary>The "Trip length" view: trips by days, in five bins.</summary>
public static class TripLengthView
{
    private static readonly (string Label, int Min, int? Max)[] Bins =
    [
        ("2 days", 0, 2),
        ("3 days", 3, 3),
        ("4 to 5 days", 4, 5),
        ("6 to 8 days", 6, 8),
        ("9 or more days", 9, null),
    ];

    public static IReadOnlyList<LengthBin> Compute(StatsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        return [.. Bins.Select(bin => new LengthBin(
            bin.Label,
            bin.Min,
            bin.Max,
            library.Trips.Count(trip => trip.Days >= bin.Min && (bin.Max is null || trip.Days <= bin.Max))))];
    }
}
