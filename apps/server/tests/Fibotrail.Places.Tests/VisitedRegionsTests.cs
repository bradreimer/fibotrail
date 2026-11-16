namespace Fibotrail.Places.Tests;

public class VisitedRegionsTests
{
    private static IReadOnlyList<(double, double)> Square(double west, double south, double east, double north) =>
        [(west, south), (east, south), (east, north), (west, north), (west, south)];

    /// <summary>Alpha (0 to 10) and Beta (20 to 30) are detail countries, and Gamma (40 to 50) is not.</summary>
    private static CountryFinder Countries() => new(
    [
        new CountryOutline("001", "Alpha", [[Square(0, 0, 10, 10)]]),
        new CountryOutline("002", "Beta", [[Square(20, 0, 30, 10)]]),
        new CountryOutline("003", "Gamma", [[Square(40, 0, 50, 10)]]),
    ]);

    /// <summary>Alpha has West (0 to 5) and East (5 to 10). Beta has one region.</summary>
    private static RegionFinder Regions() => new(
        [new DetailCountry("001", "Alpha", "regions"), new DetailCountry("002", "Beta", "districts")],
        [
            new RegionOutline("001", new CountryOutline("AA-W", "West", [[Square(0, 0, 5, 10)]])),
            new RegionOutline("001", new CountryOutline("AA-E", "East", [[Square(5, 0, 10, 10)]])),
            new RegionOutline("002", new CountryOutline("BB-ONLY", "Only", [[Square(20, 0, 30, 10)]])),
        ]);

    private static TripStops Trip(int id, string name, int month, params (double Latitude, double Longitude)[] stops) =>
        new(id, name, new DateOnly(2027, month, 1), new DateOnly(2027, month, 9), stops);

    private static IReadOnlyList<VisitedDetailCountry> Find(params TripStops[] trips) =>
        VisitedRegions.Find(trips, Countries(), Regions());

    [Fact]
    public void No_trips_visit_no_detail_country()
    {
        Assert.Empty(Find());
    }

    [Fact]
    public void A_stop_in_a_region_makes_it_visited_with_its_trip_and_first_visit_and_the_country_total()
    {
        var alpha = Assert.Single(Find(Trip(1, "West trip", 3, (5, 2))));

        Assert.Equal(("Alpha", 2), (alpha.Country.Name, alpha.Total));
        var region = Assert.Single(alpha.Regions);
        Assert.Equal(("West", "AA-W", new DateOnly(2027, 3, 1)), (region.Country.Name, region.Country.Id, region.FirstVisit));
        Assert.Equal([new VisitedTrip(1, "West trip", new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 9))], region.Trips);
    }

    [Fact]
    public void A_trip_with_stops_in_two_regions_counts_for_both_and_once_for_each()
    {
        var alpha = Assert.Single(Find(Trip(1, "Both", 3, (5, 2), (6, 3), (5, 7))));

        // Both have the same first visit, so they are listed by name.
        Assert.Equal(["East", "West"], alpha.Regions.Select(region => region.Country.Name));
        Assert.All(alpha.Regions, region => Assert.Single(region.Trips));
    }

    [Fact]
    public void The_first_visit_is_the_earliest_trip_and_regions_are_listed_earliest_first()
    {
        var alpha = Assert.Single(Find(
            Trip(2, "Later in the east", 9, (5, 7)),
            Trip(1, "Earlier in the west", 3, (5, 2)),
            Trip(3, "Later in the west", 10, (5, 2))));

        Assert.Equal(["West", "East"], alpha.Regions.Select(region => region.Country.Name));
        var west = alpha.Regions[0];
        Assert.Equal(new DateOnly(2027, 3, 1), west.FirstVisit);
        Assert.Equal([1, 3], west.Trips.Select(trip => trip.TripId));
    }

    [Fact]
    public void A_country_that_is_not_a_detail_country_is_not_listed()
    {
        Assert.Empty(Find(Trip(1, "Gamma trip", 3, (5, 45))));
    }

    [Fact]
    public void A_detail_country_is_listed_with_no_regions_when_its_stop_is_in_none()
    {
        // The stop is in Alpha's outline, but Alpha's only region covers the south of it, far from the stop.
        var countries = new CountryFinder([new CountryOutline("001", "Alpha", [[Square(0, 0, 10, 20)]])]);
        var regions = new RegionFinder(
            [new DetailCountry("001", "Alpha", "regions")],
            [new RegionOutline("001", new CountryOutline("AA-W", "West", [[Square(0, 0, 5, 10)]]))]);

        var alpha = Assert.Single(VisitedRegions.Find([Trip(1, "Gap", 3, (18, 8))], countries, regions));

        Assert.Equal(("Alpha", 1), (alpha.Country.Name, alpha.Total));
        Assert.Empty(alpha.Regions);
    }

    [Fact]
    public void Each_detail_country_lists_its_own_regions()
    {
        var visited = Find(Trip(1, "Alpha", 3, (5, 2)), Trip(2, "Beta", 4, (5, 25)));

        Assert.Equal([("Alpha", "West"), ("Beta", "Only")], visited.Select(country => (country.Country.Name, country.Regions.Single().Country.Name)));
    }
}
