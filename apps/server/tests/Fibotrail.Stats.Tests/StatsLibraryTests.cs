using Fibotrail.Places;

namespace Fibotrail.Stats.Tests;

public class StatsLibraryTests
{
    // Two squares: Sweden around (59, 15) and Denmark around (56, 10).
    private static readonly CountryOutline[] Outlines =
    [
        Square("752", "Sweden", 58, 14, 60, 16),
        Square("208", "Denmark", 55, 9, 57, 11),
        Square(null, "Nowhereland", 0, 0, 1, 1),
    ];

    private static CountryOutline Square(string? id, string name, double south, double west, double north, double east) =>
        new(id, name, [[[(west, south), (east, south), (east, north), (west, north), (west, south)]]]);

    private static StatsLibrary Build(params StatsTrip[] trips) => StatsLibrary.Build(trips, new CountryFinder(Outlines), Outlines);

    private static StatsTrip Trip(int id, DateOnly start, int days, params (double Lat, double Lon, DateOnly Day)[] photos) =>
        new(id, $"Trip {id}", start, start.AddDays(days - 1), days, 100, 50,
            [.. photos.Select(photo => (photo.Lat, photo.Lon)).Distinct()],
            [.. photos.Select(photo => new PhotoPosition(photo.Day, photo.Lat, photo.Lon))]);

    private static (double, double, DateOnly)[] SwedenDays(DateOnly start, int days) =>
        [.. Enumerable.Range(0, days).Select(offset => (59.0, 15.0, start.AddDays(offset)))];

    [Fact]
    public void Three_trips_to_Sweden_read_3_trips_and_19_days()
    {
        var library = Build(
            Trip(1, new DateOnly(2019, 7, 1), 5, SwedenDays(new DateOnly(2019, 7, 1), 5)),
            Trip(2, new DateOnly(2022, 12, 27), 7, SwedenDays(new DateOnly(2022, 12, 27), 7)),
            Trip(3, new DateOnly(2024, 8, 1), 7, SwedenDays(new DateOnly(2024, 8, 1), 7)));

        var sweden = Assert.Single(library.Countries);
        Assert.Equal(3, sweden.Trips.Count);
        Assert.Equal(19, sweden.Days);
        Assert.Equal(new DateOnly(2019, 7, 1), sweden.FirstVisit);
    }

    [Fact]
    public void A_trip_with_days_in_two_countries_counts_once_for_each_and_a_shared_day_counts_for_both()
    {
        var day1 = new DateOnly(2023, 5, 1);
        var day2 = new DateOnly(2023, 5, 2);
        var library = Build(Trip(1, day1, 2, (59, 15, day1), (56, 10, day1), (56, 10, day2)));

        Assert.Equal(2, library.Countries.Count);
        var sweden = library.Country("752")!;
        var denmark = library.Country("208")!;
        Assert.Equal((1, 1), (sweden.Trips.Count, sweden.Days));
        Assert.Equal((1, 2), (denmark.Trips.Count, denmark.Days));
    }

    [Fact]
    public void A_day_with_no_position_adds_to_days_away_and_to_no_country()
    {
        var day1 = new DateOnly(2023, 5, 1);
        var trip = Trip(1, day1, 3, (59, 15, day1));

        var library = Build(trip);

        Assert.Equal(3, library.Totals().DaysAway);
        Assert.Equal(1, library.Country("752")!.Days);
        Assert.True(trip.HasUnpositionedDays);
    }

    [Fact]
    public void Countries_sort_by_days_then_trips_then_name()
    {
        var d = new DateOnly(2023, 5, 1);
        var library = Build(
            Trip(1, d, 1, (59, 15, d)),
            Trip(2, d.AddDays(10), 2, (56, 10, d.AddDays(10)), (56, 10, d.AddDays(11))));

        Assert.Equal(["Denmark", "Sweden"], library.Countries.Select(country => country.Country.Name));
    }

    [Fact]
    public void Totals_count_trips_days_distance_countries_and_continents()
    {
        var d = new DateOnly(2023, 5, 1);
        var library = Build(Trip(1, d, 2, (59, 15, d), (56, 10, d.AddDays(1))));

        var totals = library.Totals();

        Assert.Equal(new StatsTotals(1, 2, 100, 2, 1), totals);
    }

    [Fact]
    public void No_trips_gives_empty_numbers_and_every_continent_with_none_visited()
    {
        var library = Build();

        Assert.Equal(new StatsTotals(0, 0, 0, 0, 0), library.Totals());
        Assert.Equal(7, library.Progress().Count);
        Assert.All(library.Progress(), progress => Assert.Equal(0, progress.Visited));
    }

    [Fact]
    public void The_continent_visited_counts_add_up_to_the_countries_tile()
    {
        var d = new DateOnly(2023, 5, 1);
        var library = Build(Trip(1, d, 2, (59, 15, d), (56, 10, d.AddDays(1))));

        Assert.Equal(library.Totals().Countries, library.Progress().Sum(progress => progress.Visited));
    }
}
