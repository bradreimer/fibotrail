namespace Fibotrail.Places.Tests;

/// <summary>Checks on the outlines that ship with Fibotrail. The positions are real places, to the nearest few kilometers.</summary>
public class CountryOutlinesTests
{
    private static readonly Lazy<CountryFinder> Shared = new(CountryOutlines.Load);

    private static CountryFinder Finder => Shared.Value;

    [Fact]
    public void The_embedded_outlines_load_with_every_country_and_territory_of_the_1_to_50_million_data()
    {
        var outlines = CountryOutlines.Read();

        Assert.InRange(outlines.Count, 200, 260);
        Assert.All(outlines, outline => Assert.NotEmpty(outline.Polygons));
        Assert.Equal(outlines.Count, Finder.Count);
        Assert.Contains(outlines, outline => outline.Name == "Sweden" && outline.Id == "752");
    }

    [Theory]
    [InlineData(59.27, 15.21, "Sweden")]
    [InlineData(48.86, 2.35, "France")]
    [InlineData(28.4, -81.55, "United States of America")]
    [InlineData(49.25, -123.1, "Canada")]
    [InlineData(-33.87, 151.21, "Australia")]
    public void A_city_is_in_its_country(double latitude, double longitude, string country)
    {
        var match = Finder.Find(latitude, longitude);

        Assert.Equal((country, 0), (match!.Country.Name, match.DistanceKm));
    }

    [Fact]
    public void Stockholm_sits_in_an_archipelago_the_outline_does_not_cover_and_still_counts_for_Sweden()
    {
        var match = Finder.Find(59.33, 18.07);

        Assert.Equal("Sweden", match!.Country.Name);
        Assert.InRange(match.DistanceKm, 0, 5);
    }

    [Fact]
    public void Lesotho_is_found_inside_the_hole_in_South_Africa()
    {
        Assert.Equal("Lesotho", Finder.Find(-29.6, 28.2)!.Country.Name);
        Assert.Equal("South Africa", Finder.Find(-26.2, 28.0)!.Country.Name);
    }

    [Fact]
    public void A_position_a_few_kilometers_off_a_coast_joins_the_nearest_country_and_one_far_out_at_sea_does_not()
    {
        // West of Cabo da Roca, Portugal: about 9 km off the coast, then about 60 km.
        var near = Finder.Find(38.78, -9.60);
        Assert.Equal("Portugal", near!.Country.Name);
        Assert.InRange(near.DistanceKm, 1, 25);
        Assert.Null(Finder.Find(38.78, -10.2));
        Assert.Null(Finder.Find(0, -30));
    }

    // Russia, Fiji, and Antarctica have rings that cross the antimeridian. Drawn flat, the edge from
    // 179.9 East to 180 West runs across the whole world, and the outline then covers the sea.
    [Theory]
    [InlineData(67.0, -30.0)]
    [InlineData(67.0, 0.0)]
    [InlineData(66.5, -60.0)]
    [InlineData(73.0, -150.0)]
    public void The_open_sea_at_the_latitude_of_Chukotka_belongs_to_no_country(double latitude, double longitude)
    {
        Assert.Null(Finder.Find(latitude, longitude));
    }

    [Theory]
    [InlineData(67.8, -115.1, "Canada")]
    [InlineData(64.2, -51.7, "Greenland")]
    [InlineData(64.84, -147.72, "United States of America")]
    public void Places_in_the_far_north_are_in_their_own_countries_and_not_in_Russia(double latitude, double longitude, string country)
    {
        Assert.Equal(country, Finder.Find(latitude, longitude)!.Country.Name);
    }

    [Theory]
    [InlineData(64.73, 177.5)]
    [InlineData(66.16, -169.8)]
    public void Chukotka_is_in_Russia_on_both_sides_of_the_antimeridian(double latitude, double longitude)
    {
        Assert.Equal("Russia", Finder.Find(latitude, longitude)!.Country.Name);
    }

    [Theory]
    [InlineData(-17.7, 178.0)]
    [InlineData(-16.8, -179.95)]
    public void Fiji_is_found_on_both_sides_of_the_antimeridian(double latitude, double longitude)
    {
        Assert.Equal("Fiji", Finder.Find(latitude, longitude)!.Country.Name);
    }
}
