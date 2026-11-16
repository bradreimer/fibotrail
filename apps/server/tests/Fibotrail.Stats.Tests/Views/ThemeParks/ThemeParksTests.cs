using Fibotrail.Detection;
using Fibotrail.Detection.Parks;
using Fibotrail.Places;
using Fibotrail.Stats.Views.ThemeParks;

namespace Fibotrail.Stats.Tests.Views.ThemeParks;

public class ThemeParksTests
{
    private static ParkPlace Square(string id, double west) =>
        new(id, id.ToUpperInvariant(), "Resort", 3, 50, [[[new(0, west), new(0, west + 0.01), new(0.01, west + 0.01), new(0.01, west), new(0, west)]]]);

    private static readonly ParkPlace A = Square("a", 0);
    private static readonly ParkPlace B = Square("b", 0.5);
    private static readonly ParkPlace C = Square("c", 1);

    private static StatsLibrary Library(params StatsTrip[] trips)
    {
        CountryOutline[] outlines = [];
        return StatsLibrary.Build(trips, new CountryFinder(outlines), outlines, null, new ParkLookup([A, B, C]),
            [new ParkSet("one", "One parks", [A, B]), new ParkSet("two", "Two parks", [C])]);
    }

    private static StatsTrip Trip(int id, DateOnly start, params (double Lat, double Lon, int Day)[] photos) =>
        new(id, $"Trip {id}", start, start.AddDays(5), 6, 0, 0, [],
            [.. photos.Select(photo => new PhotoPosition(start.AddDays(photo.Day), photo.Lat, photo.Lon))]);

    [Fact]
    public void Every_park_is_listed_with_days_and_trips_and_an_unvisited_park_shows_zero()
    {
        var library = Library(Trip(1, new DateOnly(2026, 3, 1), (0.005, 0.005, 0), (0.005, 0.005, 1), (0.005, 0.005, 2)));

        var groups = ThemeParksView.Compute(library);

        Assert.Equal(["One parks", "Two parks"], groups.Select(group => group.Name));
        var one = groups[0];
        Assert.Equal((3, 1), (one.TotalDays, one.Visited));
        Assert.Equal([("A", 3, 1), ("B", 0, 0)], one.Parks.Select(park => (park.Name, park.Days, park.Trips)));
        Assert.Null(one.Parks[1].FirstVisit);
        Assert.Equal(new DateOnly(2026, 3, 1), one.Parks[0].FirstVisit);
    }

    [Fact]
    public void The_view_and_the_library_give_the_same_days_for_every_park()
    {
        var library = Library(
            Trip(1, new DateOnly(2026, 3, 1), (0.005, 0.005, 0), (0.005, 0.005, 1), (0.005, 0.005, 2), (0.005, 1.005, 3), (0.005, 1.005, 3), (0.005, 1.005, 4)),
            Trip(2, new DateOnly(2026, 6, 1), (0.005, 0.505, 0), (0.005, 0.505, 0), (0.005, 0.505, 1)));

        var fromView = ThemeParksView.Compute(library).SelectMany(group => group.Parks).ToDictionary(park => park.Id, park => park.Days);

        Assert.All(library.Parks, stats => Assert.Equal(stats.Days, fromView[stats.Place.Id]));
        Assert.Equal(3, fromView["a"]);
        Assert.Equal(2, fromView["b"]);
        Assert.Equal(2, fromView["c"]);
    }


    [Fact]
    public void With_no_collection_files_there_are_no_groups()
    {
        CountryOutline[] outlines = [];
        var library = StatsLibrary.Build([], new CountryFinder(outlines), outlines);

        Assert.Empty(ThemeParksView.Compute(library));
    }
}
