namespace Fibotrail.Stats.Views.TripsPerYear;

/// <summary>One bar of the chart: the added trips that started in a year.</summary>
public sealed record YearCount(int Year, IReadOnlyList<TripRef> Trips)
{
    public int Count => Trips.Count;
}

/// <summary>The "Trips per year" view. Years with no trips between the first and last year show as zero bars.</summary>
public static class TripsPerYearView
{
    public static IReadOnlyList<YearCount> Compute(StatsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (library.Trips.Count == 0) return [];

        var byYear = library.Trips.GroupBy(trip => trip.StartDate.Year)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<TripRef>)[.. group.Select(trip => trip.ToRef())]);
        var first = byYear.Keys.Min();
        var last = byYear.Keys.Max();
        return [.. Enumerable.Range(first, last - first + 1)
            .Select(year => new YearCount(year, byYear.TryGetValue(year, out var trips) ? trips : []))];
    }

}
