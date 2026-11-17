using Fibotrail.Detection.Cities;
using Fibotrail.Detection.Parks;

namespace Fibotrail.Detection.Tests;

/// <summary>Measuring the city merge radius. See ADR-015.</summary>
public class CityRadiusReportTests
{
    private static readonly DateOnly Start = new(2024, 5, 1);

    private static PlaceStop Stop(string name, double latitude, double longitude, int photos, int trip = 1) =>
        new(trip, $"Trip {trip}", Start, Start.AddDays(4), name, latitude, longitude, photos);

    // Seattle and Tacoma are about 41 km apart. Shibuya and Shinjuku are about 3 km apart.
    private static readonly PlaceStop[] Stops =
    [
        Stop("Seattle", 47.61, -122.33, 50),
        Stop("Tacoma", 47.25, -122.44, 20),
        Stop("Shibuya", 35.66, 139.70, 80, 2),
        Stop("Shinjuku", 35.69, 139.70, 30, 2),
    ];

    [Fact]
    public void Each_radius_gives_its_own_count_of_cities()
    {
        var results = CityRadiusReport.Measure(Stops, null, [1, 10, 25, 50]);

        Assert.Equal([4, 3, 3, 2], results.Select(result => result.Cities.Count));
        Assert.Equal([1d, 10d, 25d, 50d], results.Select(result => result.RadiusKm));
        Assert.All(results, result => Assert.True(result.Time >= TimeSpan.Zero));
    }

    [Fact]
    public void Merges_are_the_cities_that_a_larger_radius_absorbs()
    {
        var results = CityRadiusReport.Measure(Stops, null, [1, 10, 25, 50]);

        var from1To10 = CityRadiusReport.MergesBetween(results[0], results[1]);
        var from25To50 = CityRadiusReport.MergesBetween(results[2], results[3]);

        var tokyo = Assert.Single(from1To10);
        Assert.Equal("Shibuya", tokyo.Into);
        Assert.Equal(["Shibuya", "Shinjuku"], tokyo.Absorbed.Order());
        var seattle = Assert.Single(from25To50);
        Assert.Equal("Seattle", seattle.Into);
        Assert.Equal(["Seattle", "Tacoma"], seattle.Absorbed.Order());
        Assert.Empty(CityRadiusReport.MergesBetween(results[1], results[2]));
    }

    [Fact]
    public void Parks_are_counted_apart_from_cities()
    {
        var park = new ParkPlace("p", "Test Park", "Resort", 3, 50, [[[new(47.60, -122.34), new(47.60, -122.32), new(47.62, -122.32), new(47.62, -122.34), new(47.60, -122.34)]]]);

        var result = Assert.Single(CityRadiusReport.Measure(Stops, new ParkLookup([park]), [25]));

        Assert.Equal(["Test Park"], result.Parks.Select(place => place.Name));
        Assert.DoesNotContain(result.Cities, city => city.Name == "Seattle");
    }

    [Fact]
    public void No_stops_give_no_cities() =>
        Assert.Empty(Assert.Single(CityRadiusReport.Measure([], null, [25])).Cities);
}
