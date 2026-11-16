namespace Fibotrail.Places.Tests;

public class ContinentsTests
{
    [Fact]
    public void Every_bundled_outline_has_a_continent()
    {
        var outlines = CountryOutlines.Read();

        var missing = outlines.Where(outline => Continents.Of(new Country(outline.Id, outline.Name)) is null).Select(outline => outline.Name);

        Assert.Empty(missing);
    }

    [Theory]
    [InlineData("752", Continent.Europe)]
    [InlineData("643", Continent.Europe)]
    [InlineData("792", Continent.Asia)]
    [InlineData("840", Continent.NorthAmerica)]
    [InlineData("036", Continent.Oceania)]
    [InlineData("010", Continent.Antarctica)]
    [InlineData("076", Continent.SouthAmerica)]
    [InlineData("818", Continent.Africa)]
    public void A_country_belongs_to_its_continent(string id, Continent expected) =>
        Assert.Equal(expected, Continents.Of(new Country(id, "x")));

    [Fact]
    public void A_country_with_no_code_is_found_by_name() =>
        Assert.Equal(Continent.Europe, Continents.Of(new Country(null, "Kosovo")));

    [Fact]
    public void Every_continent_has_at_least_one_country_and_slugs_round_trip()
    {
        var totals = Continents.Totals(CountryOutlines.Read());

        Assert.All(Continents.All, continent =>
        {
            Assert.True(totals[continent] > 0);
            Assert.Equal(continent, Continents.FromSlug(Continents.Slug(continent)));
        });
        Assert.Equal("north-america", Continents.Slug(Continent.NorthAmerica));
        Assert.Null(Continents.FromSlug("atlantis"));
    }
}
