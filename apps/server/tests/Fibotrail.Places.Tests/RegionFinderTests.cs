namespace Fibotrail.Places.Tests;

public class RegionFinderTests
{
    private static IReadOnlyList<(double, double)> Square(double west, double south, double east, double north) =>
        [(west, south), (east, south), (east, north), (west, north), (west, south)];

    /// <summary>Alpha (001) has two regions side by side. Beta (002) has one. Gamma (003) is not a detail country.</summary>
    private static RegionFinder Finder() => new(
        [new DetailCountry("001", "Alpha", "regions"), new DetailCountry("002", "Beta", "districts")],
        [
            new RegionOutline("001", new CountryOutline("AA-W", "West", [[Square(0, 0, 5, 10)]])),
            new RegionOutline("001", new CountryOutline("AA-E", "East", [[Square(5, 0, 10, 10)]])),
            new RegionOutline("002", new CountryOutline("BB-ONLY", "Only", [[Square(20, 0, 30, 10)]])),
        ]);

    [Fact]
    public void A_position_inside_a_region_is_in_that_region()
    {
        Assert.Equal(("AA-E", 0), (Finder().Find("001", 5, 7)!.Country.Id, Finder().Find("001", 5, 7)!.DistanceKm));
        Assert.Equal("AA-W", Finder().Find("001", 5, 2)!.Country.Id);
    }

    [Fact]
    public void The_totals_and_names_are_those_of_the_country_asked_about()
    {
        var finder = Finder();

        Assert.Equal((2, 1, 0), (finder.Total("001"), finder.Total("002"), finder.Total("003")));
        Assert.Equal(["East", "West"], finder.Regions("001").Select(region => region.Name).Order(StringComparer.Ordinal));
        Assert.Empty(finder.Regions("003"));
    }

    [Fact]
    public void A_region_is_only_looked_for_in_its_own_country()
    {
        // 5, 25 is inside Beta's region. Asked about Alpha, it is 15 degrees from any region of Alpha.
        Assert.Null(Finder().Find("001", 5, 25));
        Assert.Equal("BB-ONLY", Finder().Find("002", 5, 25)!.Country.Id);
    }

    [Fact]
    public void A_country_that_is_not_a_detail_country_has_no_region()
    {
        Assert.Null(Finder().Find("003", 5, 2));
        Assert.Null(Finder().Find(null, 5, 2));
    }

    [Fact]
    public void A_position_just_outside_every_region_joins_the_nearest_one_within_the_limit()
    {
        // 0.1 degrees off the east edge of East: about 11 km.
        var match = Finder().Find("001", 5, 10.1);

        Assert.Equal("AA-E", match!.Country.Id);
        Assert.InRange(match.DistanceKm, 10, 12);
    }

    [Fact]
    public void A_position_farther_than_the_limit_from_every_region_counts_for_none()
    {
        // 0.4 degrees is about 44 km, past the 25 km limit.
        Assert.Null(Finder().Find("001", 5, 10.4));
        Assert.NotNull(Finder().Find("001", 5, 10.4, nearestKm: 50));
    }
}
