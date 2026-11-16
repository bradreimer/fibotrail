namespace Fibotrail.Places.Tests;

/// <summary>Checks on the region outlines that ship with Fibotrail. The positions are real places, to the nearest few kilometers. See ADR-012.</summary>
public class RegionOutlinesTests
{
    private static readonly Lazy<RegionFinder> Shared = new(RegionOutlines.Load);

    private static RegionFinder Finder => Shared.Value;

    private const string Canada = "124";
    private const string UnitedStates = "840";

    [Fact]
    public void Canada_has_13_provinces_and_territories_and_the_USA_has_50_states_and_the_District_of_Columbia()
    {
        Assert.Equal([(Canada, "Canada", "provinces and territories"), (UnitedStates, "United States of America", "states")], Finder.DetailCountries.Select(country => (country.Id, country.Name, country.RegionNoun)));
        Assert.Equal(13, Finder.Total(Canada));
        Assert.Equal(51, Finder.Total(UnitedStates));
        Assert.Contains(Finder.Regions(UnitedStates), region => region is { Id: "US-DC", Name: "District of Columbia" });
    }

    [Fact]
    public void Every_region_has_an_ISO_3166_2_code_named_for_its_country_and_a_polygon()
    {
        var (_, regions) = RegionOutlines.Read();

        Assert.All(regions, region =>
        {
            Assert.NotEmpty(region.Outline.Polygons);
            Assert.StartsWith(region.CountryId == Canada ? "CA-" : "US-", region.Outline.Id);
        });
        Assert.Equal(regions.Count, regions.Select(region => region.Outline.Id).Distinct().Count());
    }

    [Fact]
    public void The_territories_of_the_USA_are_not_among_its_regions()
    {
        Assert.DoesNotContain(Finder.Regions(UnitedStates), region => region.Id == "US-PR");
    }

    [Theory]
    [InlineData(Canada, 43.65, -79.38, "CA-ON")]
    [InlineData(Canada, 51.18, -115.57, "CA-AB")]
    [InlineData(Canada, 49.25, -123.1, "CA-BC")]
    [InlineData(Canada, 46.24, -63.13, "CA-PE")]
    [InlineData(Canada, 60.72, -135.05, "CA-YT")]
    [InlineData(UnitedStates, 30.27, -97.74, "US-TX")]
    [InlineData(UnitedStates, 45.52, -122.68, "US-OR")]
    [InlineData(UnitedStates, 28.4, -81.55, "US-FL")]
    [InlineData(UnitedStates, 64.84, -147.72, "US-AK")]
    [InlineData(UnitedStates, 21.31, -157.86, "US-HI")]
    public void A_city_is_in_its_province_or_state(string country, double latitude, double longitude, string region)
    {
        Assert.Equal(region, Finder.Find(country, latitude, longitude)!.Country.Id);
    }

    [Theory]
    [InlineData(38.8977, -77.0365)] // the White House
    [InlineData(38.8899, -77.0091)] // the Capitol
    public void The_District_of_Columbia_is_found_for_stops_in_the_middle_of_the_city(double latitude, double longitude)
    {
        Assert.Equal("US-DC", Finder.Find(UnitedStates, latitude, longitude)!.Country.Id);
    }

    [Fact]
    public void A_stop_in_a_country_that_is_not_a_detail_country_has_no_region()
    {
        Assert.Null(Finder.Find("250", 48.86, 2.35));
        Assert.Null(Finder.Find(null, 48.86, 2.35));
    }

    [Fact]
    public void A_region_is_only_looked_for_in_its_own_country()
    {
        // Toronto is in a Canadian province, so asking the USA for it finds a US state or nothing, never Ontario.
        Assert.NotEqual("CA-ON", Finder.Find(UnitedStates, 43.65, -79.38)?.Country.Id);
    }

    [Fact]
    public void A_position_far_out_at_sea_is_in_no_region()
    {
        Assert.Null(Finder.Find(UnitedStates, 27.0, -90.0));
        Assert.Null(Finder.Find(Canada, 55.0, -45.0));
    }
}
