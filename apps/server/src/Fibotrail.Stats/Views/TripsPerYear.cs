namespace Fibotrail.Stats.Views;

/// <summary>A trip as a list row.</summary>
public sealed record TripRef(int Id, string Name, DateOnly StartDate, DateOnly EndDate, int Days);

/// <summary>One bar of the chart: the added trips that started in a year.</summary>
public sealed record YearCount(int Year, IReadOnlyList<TripRef> Trips)
{
    public int Count => Trips.Count;
}

/// <summary>The "Trips per year" view. Years with no trips between the first and last year show as zero bars.</summary>
public static class TripsPerYear
{
    public static IReadOnlyList<YearCount> Compute(StatsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (library.Trips.Count == 0) return [];

        var byYear = library.Trips.GroupBy(trip => trip.StartDate.Year)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<TripRef>)[.. group.Select(ToRef)]);
        var first = byYear.Keys.Min();
        var last = byYear.Keys.Max();
        return [.. Enumerable.Range(first, last - first + 1)
            .Select(year => new YearCount(year, byYear.TryGetValue(year, out var trips) ? trips : []))];
    }

    internal static TripRef ToRef(StatsTrip trip) => new(trip.Id, trip.Name, trip.StartDate, trip.EndDate, trip.Days);
}
