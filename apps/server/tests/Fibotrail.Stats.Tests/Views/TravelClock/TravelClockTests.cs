using Fibotrail.Places;
using Fibotrail.Stats.Views.TravelClock;

namespace Fibotrail.Stats.Tests.Views.TravelClock;

public class TravelClockTests
{
    private static StatsLibrary Library(params (int Id, DateOnly Start, DateOnly End)[] trips)
    {
        CountryOutline[] outlines = [];
        return StatsLibrary.Build(
            trips.Select(trip => new StatsTrip(trip.Id, $"Trip {trip.Id}", trip.Start, trip.End, trip.End.DayNumber - trip.Start.DayNumber + 1, 0, 0, [], [])),
            new CountryFinder(outlines),
            outlines);
    }

    [Fact]
    public void A_trip_from_July_12_to_16_adds_5_days_and_1_trip_to_July()
    {
        var months = TravelClockView.Compute(Library((1, new DateOnly(2024, 7, 12), new DateOnly(2024, 7, 16))));

        var july = months[6];
        Assert.Equal((7, 5, 1), (july.Month, july.Days, july.Trips.Count));
        Assert.Equal(5, months.Sum(month => month.Days));
    }

    [Fact]
    public void A_trip_from_December_27_to_January_2_counts_for_both_months()
    {
        var months = TravelClockView.Compute(Library((1, new DateOnly(2022, 12, 27), new DateOnly(2023, 1, 2))));

        Assert.Equal((2, 1), (months[0].Days, months[0].Trips.Count));
        Assert.Equal((5, 1), (months[11].Days, months[11].Trips.Count));
    }

    [Fact]
    public void Trips_of_a_month_come_from_every_year_newest_first_and_a_trip_counts_once_per_month()
    {
        var months = TravelClockView.Compute(Library(
            (1, new DateOnly(2020, 7, 1), new DateOnly(2020, 7, 31)),
            (2, new DateOnly(2024, 7, 5), new DateOnly(2024, 7, 6))));

        Assert.Equal([2, 1], months[6].Trips.Select(trip => trip.Id));
        Assert.Equal(33, months[6].Days);
    }

    [Fact]
    public void There_are_always_twelve_months_even_with_no_trips()
    {
        var months = TravelClockView.Compute(Library());

        Assert.Equal(Enumerable.Range(1, 12), months.Select(month => month.Month));
        Assert.All(months, month => Assert.Equal(0, month.Days));
    }
}
