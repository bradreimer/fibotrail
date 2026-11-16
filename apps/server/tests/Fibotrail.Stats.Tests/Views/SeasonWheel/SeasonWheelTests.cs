using Fibotrail.Places;
using Fibotrail.Stats.Views.SeasonWheel;

namespace Fibotrail.Stats.Tests.Views.SeasonWheel;

public class SeasonWheelTests
{
    private static readonly CountryOutline[] Outlines =
    [
        new("752", "Sweden", [[[(14, 58), (16, 58), (16, 60), (14, 60), (14, 58)]]]),
        new("036", "Australia", [[[(150, -35), (152, -35), (152, -33), (150, -33), (150, -35)]]]),
        new("076", "Brazil", [[[(-50, -10), (-40, -10), (-40, 10), (-50, 10), (-50, -10)]]]),
    ];

    private static StatsLibrary Library(double latitude, double longitude, params DateOnly[] days)
    {
        var trips = days.GroupBy(day => day.Year).Select((group, index) =>
            new StatsTrip(index + 1, $"Trip {index + 1}", group.Min(), group.Max(), group.Count(), 0, 0,
                [(latitude, longitude)], [.. group.Select(day => new PhotoPosition(day, latitude, longitude))]));
        return StatsLibrary.Build(trips, new CountryFinder(Outlines), Outlines);
    }

    private static DateOnly[] Run(int year, int month, int day, int count) =>
        [.. Enumerable.Range(0, count).Select(offset => new DateOnly(year, month, day).AddDays(offset))];

    [Fact]
    public void Sweden_with_days_in_July_August_December_and_January_has_two_of_four_seasons()
    {
        var days = new[] { Run(2019, 7, 1, 5), Run(2022, 12, 27, 5), Run(2023, 1, 1, 2), Run(2024, 8, 1, 7) }.SelectMany(run => run).ToArray();

        var wheel = SeasonWheelView.Compute(Library(59, 15, days), "752")!;

        Assert.Equal(2, wheel.SeasonsSeen);
        Assert.Equal(5, wheel.Months[6].Days);
        Assert.Equal(7, wheel.Months[7].Days);
        Assert.Equal(5, wheel.Months[11].Days);
        Assert.Equal(2, wheel.Months[0].Days);
        Assert.Equal(7, wheel.Seasons[0].Days);
        Assert.Equal(12, wheel.Seasons[2].Days);
        Assert.False(wheel.Seasons[1].Seen);
        Assert.Equal("north", wheel.Hemisphere);
    }

    [Fact]
    public void A_day_in_Sydney_in_January_is_summer_and_a_day_in_Stockholm_in_January_is_winter()
    {
        var sydney = SeasonWheelView.Compute(Library(-34, 151, new DateOnly(2023, 1, 5)), "036")!;
        var stockholm = SeasonWheelView.Compute(Library(59, 15, new DateOnly(2023, 1, 5)), "752")!;

        Assert.Equal(1, sydney.Seasons.Single(season => season.Name == "Summer").Days);
        Assert.Equal(1, stockholm.Seasons.Single(season => season.Name == "Winter").Days);
        Assert.Equal("south", sydney.Hemisphere);
    }

    [Fact]
    public void The_arcs_sit_on_the_months_of_the_hemisphere_of_the_country()
    {
        var sydney = SeasonWheelView.Compute(Library(-34, 151, new DateOnly(2023, 1, 5)), "036")!;
        var stockholm = SeasonWheelView.Compute(Library(59, 15, new DateOnly(2023, 1, 5)), "752")!;

        Assert.Equal([12, 1, 2], stockholm.Seasons[0].Months);
        Assert.Equal([6, 7, 8], sydney.Seasons[0].Months);
        Assert.Equal([12, 1, 2], sydney.Seasons[2].Months);
    }

    [Fact]
    public void A_country_with_days_in_all_four_seasons_has_every_season()
    {
        var days = new[] { new DateOnly(2023, 1, 5), new DateOnly(2023, 4, 5), new DateOnly(2023, 7, 5), new DateOnly(2023, 10, 5) };

        var wheel = SeasonWheelView.Compute(Library(59, 15, days), "752")!;

        Assert.Equal(4, wheel.SeasonsSeen);
        Assert.All(wheel.Seasons, season => Assert.True(season.Seen));
    }

    [Fact]
    public void An_unvisited_country_has_no_wheel() =>
        Assert.Null(SeasonWheelView.Compute(Library(59, 15, new DateOnly(2023, 1, 5)), "250"));
}
