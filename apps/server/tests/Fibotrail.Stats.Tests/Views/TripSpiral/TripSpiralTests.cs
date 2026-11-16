using Fibotrail.Places;
using Fibotrail.Stats.Views.TripSpiral;

namespace Fibotrail.Stats.Tests.Views.TripSpiral;

public class TripSpiralTests
{
    private static readonly CountryOutline[] Outlines =
    [
        new("752", "Sweden", [[[(14, 58), (16, 58), (16, 60), (14, 60), (14, 58)]]]),
        new("208", "Denmark", [[[(9, 55), (11, 55), (11, 57), (9, 57), (9, 55)]]]),
        new("840", "United States of America", [[[(-100, 30), (-90, 30), (-90, 40), (-100, 40), (-100, 30)]]]),
    ];

    private static StatsTrip Trip(int id, DateOnly start, int days, params (double Lat, double Lon)[] stops) =>
        new(id, $"Trip {id}", start, start.AddDays(days - 1), days, 0, 0, stops, [.. stops.Select(stop => new PhotoPosition(start, stop.Lat, stop.Lon))]);

    private static StatsLibrary Library(params StatsTrip[] trips) => StatsLibrary.Build(trips, new CountryFinder(Outlines), Outlines);

    [Fact]
    public void Trips_come_oldest_first_each_with_its_continent()
    {
        var spiral = TripSpiralView.Compute(Library(
            Trip(2, new DateOnly(2024, 5, 1), 4, (35, -95)),
            Trip(1, new DateOnly(2020, 1, 1), 3, (59, 15))));

        Assert.Equal([1, 2], spiral.Select(item => item.Trip.Id));
        Assert.Equal(["europe", "north-america"], spiral.Select(item => item.ContinentId));
        Assert.Equal("North America", spiral[1].ContinentName);
    }

    [Fact]
    public void A_trip_takes_the_continent_with_most_of_its_countries_and_none_when_no_stop_is_in_a_country()
    {
        var spiral = TripSpiralView.Compute(Library(
            Trip(1, new DateOnly(2020, 1, 1), 3, (59, 15), (56, 10), (35, -95)),
            Trip(2, new DateOnly(2021, 1, 1), 3, (0, -30))));

        Assert.Equal("europe", spiral[0].ContinentId);
        Assert.Null(spiral[1].ContinentId);
    }

    [Fact]
    public void With_no_trips_the_spiral_is_empty() => Assert.Empty(TripSpiralView.Compute(Library()));
}
