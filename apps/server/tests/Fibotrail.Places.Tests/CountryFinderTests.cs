namespace Fibotrail.Places.Tests;

public class CountryFinderTests
{
    /// <summary>A square from (west, south) to (east, north), as one ring.</summary>
    private static IReadOnlyList<(double, double)> Square(double west, double south, double east, double north) =>
        [(west, south), (east, south), (east, north), (west, north), (west, south)];

    /// <summary>Country A is a 10 degree square with a hole. Country B fills the hole. Country C is far away and has no code.</summary>
    private static CountryFinder MadeUp() => new(
    [
        new CountryOutline("001", "Alpha", [[Square(0, 0, 10, 10), Square(4, 4, 6, 6)]]),
        new CountryOutline("002", "Beta", [[Square(4, 4, 6, 6)]]),
        new CountryOutline(null, "Gamma", [[Square(30, 0, 40, 10)]]),
    ]);

    [Fact]
    public void A_position_inside_an_outline_is_in_that_country()
    {
        var match = MadeUp().Find(2, 2);

        Assert.Equal(("Alpha", 0), (match!.Country.Name, match.DistanceKm));
    }

    [Fact]
    public void A_country_inside_the_hole_of_another_is_found_and_the_hole_is_not_the_outer_country()
    {
        var finder = MadeUp();

        Assert.Equal("Beta", finder.Find(5, 5)!.Country.Name);
        // A position in the hole that the inner country doesn't cover is in neither.
        var edge = new CountryFinder([new CountryOutline("001", "Alpha", [[Square(0, 0, 10, 10), Square(4, 4, 6, 6)]])]);
        Assert.Null(edge.Find(5, 5, nearestKm: 10));
    }

    [Fact]
    public void A_position_just_outside_a_coast_joins_the_nearest_country_and_says_how_far()
    {
        // 0.1 degrees east of Alpha's east edge, at the equator: about 11 km.
        var match = MadeUp().Find(2, 10.1);

        Assert.Equal("Alpha", match!.Country.Name);
        Assert.InRange(match.DistanceKm, 10, 12);
    }

    [Theory]
    [InlineData(10.4)]
    [InlineData(15)]
    public void A_position_farther_than_the_limit_from_every_country_counts_for_none(double longitude)
    {
        // 0.4 degrees is about 44 km, which is past the 25 km limit.
        Assert.Null(MadeUp().Find(2, longitude));
    }

    [Fact]
    public void The_limit_can_be_changed()
    {
        Assert.Null(MadeUp().Find(2, 10.1, nearestKm: 5));
        Assert.NotNull(MadeUp().Find(2, 10.3, nearestKm: 40));
    }

    [Fact]
    public void The_nearest_of_two_countries_wins()
    {
        // Between Alpha (east edge at 10) and Gamma (west edge at 30), nearer to Alpha.
        var finder = new CountryFinder(
        [
            new CountryOutline("001", "Alpha", [[Square(0, 0, 10, 10)]]),
            new CountryOutline("003", "Gamma", [[Square(10.4, 0, 20, 10)]]),
        ]);

        Assert.Equal("Alpha", finder.Find(5, 10.1)!.Country.Name);
        Assert.Equal("Gamma", finder.Find(5, 10.3)!.Country.Name);
    }

    [Fact]
    public void A_country_with_no_code_is_told_apart_by_its_name()
    {
        var finder = MadeUp();

        var gamma = finder.Find(5, 35)!.Country;

        Assert.Null(gamma.Id);
        Assert.Equal("Gamma", gamma.Key);
        Assert.Equal("001", finder.Find(2, 2)!.Country.Key);
    }

    [Fact]
    public void Distance_between_two_positions_is_the_great_circle_distance()
    {
        Assert.Equal(111.2, CountryFinder.DistanceKm(0, 0, 1, 0), 0.1);
        Assert.Equal(0, CountryFinder.DistanceKm(10, 20, 10, 20));
    }
}
