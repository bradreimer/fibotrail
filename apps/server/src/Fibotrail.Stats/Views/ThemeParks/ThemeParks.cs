namespace Fibotrail.Stats.Views.ThemeParks;

/// <summary>A park with its numbers. A park nobody visited has 0 days, 0 trips, and no first visit.</summary>
public sealed record ThemePark(string Id, string Name, string Group, int Days, int Trips, DateOnly? FirstVisit);

/// <summary>A collection of parks, such as "Disney parks", with the days across its parks.</summary>
public sealed record ThemeParkGroup(string CollectionId, string Name, int TotalDays, int Visited, IReadOnlyList<ThemePark> Parks);

/// <summary>
/// The "Theme parks" view: days and trips in each park, grouped by collection. It reads the park
/// days in the stats library, the same ones Collections shows. See docs/specs/theme-parks-view.md.
/// </summary>
public static class ThemeParksView
{
    public static IReadOnlyList<ThemeParkGroup> Compute(StatsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        var visited = library.Parks.ToDictionary(stats => stats.Place.Id, StringComparer.Ordinal);
        return [.. library.ParkSets.Select(collection =>
        {
            var parks = collection.Places
                .Select(place => visited.TryGetValue(place.Id, out var stats)
                    ? new ThemePark(place.Id, place.Name, place.Group, stats.Days, stats.Trips.Count, stats.FirstVisit)
                    : new ThemePark(place.Id, place.Name, place.Group, 0, 0, null))
                .OrderByDescending(park => park.Days)
                .ThenBy(park => park.Name, StringComparer.Ordinal)
                .ToList();
            return new ThemeParkGroup(collection.Id, collection.Name, parks.Sum(park => park.Days), parks.Count(park => park.Days > 0), parks);
        })];
    }
}
