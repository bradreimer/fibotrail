using Fibotrail.Places;
using Fibotrail.Stats.Views;

namespace Fibotrail.Stats.Tests;

public class ViewsTests
{
    private static readonly CountryOutline[] Outlines =
    [
        new("752", "Sweden", [[[(14, 58), (16, 58), (16, 60), (14, 60), (14, 58)]]]),
        new("208", "Denmark", [[[(9, 55), (11, 55), (11, 57), (9, 57), (9, 55)]]]),
    ];

    private static StatsTrip Trip(int id, DateOnly start, int days, double farthest = 100, bool sweden = true, bool denmark = false)
    {
        var photos = new List<PhotoPosition>();
        if (sweden) photos.Add(new PhotoPosition(start, 59, 15));
        if (denmark) photos.Add(new PhotoPosition(start, 56, 10));
        return new StatsTrip(id, $"Trip {id}", start, start.AddDays(days - 1), days, 10, farthest,
            [.. photos.Select(photo => (photo.Latitude, photo.Longitude))], photos);
    }

    private static StatsLibrary Library(params StatsTrip[] trips) => StatsLibrary.Build(trips, new CountryFinder(Outlines), Outlines);

    [Fact]
    public void Trips_per_year_fills_the_years_between_with_zero()
    {
        var library = Library(Trip(1, new DateOnly(2019, 7, 1), 5), Trip(2, new DateOnly(2021, 1, 1), 3), Trip(3, new DateOnly(2021, 6, 1), 3));

        var years = TripsPerYear.Compute(library);

        Assert.Equal([2019, 2020, 2021], years.Select(year => year.Year));
        Assert.Equal([1, 0, 2], years.Select(year => year.Count));
    }

    [Fact]
    public void Trips_per_year_is_empty_with_no_trips() => Assert.Empty(TripsPerYear.Compute(Library()));

    [Fact]
    public void Trip_length_uses_five_bins_and_counts_every_trip_once()
    {
        var library = Library(
            Trip(1, new DateOnly(2020, 1, 1), 2),
            Trip(2, new DateOnly(2020, 2, 1), 3),
            Trip(3, new DateOnly(2020, 3, 1), 4),
            Trip(4, new DateOnly(2020, 4, 1), 5),
            Trip(5, new DateOnly(2020, 5, 1), 8),
            Trip(6, new DateOnly(2020, 6, 1), 9),
            Trip(7, new DateOnly(2020, 7, 1), 40));

        var bins = TripLength.Compute(library);

        Assert.Equal([1, 1, 2, 1, 2], bins.Select(bin => bin.Count));
        Assert.Equal(library.Trips.Count, bins.Sum(bin => bin.Count));
    }

    [Fact]
    public void Records_finds_the_longest_farthest_most_countries_most_visited_and_the_gap()
    {
        var library = Library(
            Trip(1, new DateOnly(2020, 1, 1), 3, farthest: 500),
            Trip(2, new DateOnly(2020, 3, 1), 9, farthest: 4000, denmark: true),
            Trip(3, new DateOnly(2020, 3, 20), 2, farthest: 10));

        var records = Records.Compute(library);

        Assert.Equal(2, records.LongestTrip!.Trip.Id);
        Assert.Equal(2, records.FarthestTrip!.Trip.Id);
        Assert.Equal(2, records.MostCountriesTrip!.Trip.Id);
        Assert.Equal(2d, records.MostCountriesTrip.Value);
        Assert.Equal("Sweden", records.MostVisitedCountry!.Name);
        Assert.Equal(3, records.MostVisitedCountry.Trips);
        // Jan 4 to Feb 29 is 57 days (2020 is a leap year).
        Assert.Equal(57, records.LongestGap!.Days);
        Assert.Equal((1, 2), (records.LongestGap.After.Id, records.LongestGap.Before.Id));
    }

    [Fact]
    public void Records_are_null_with_no_trips()
    {
        var records = Records.Compute(Library());

        Assert.Null(records.LongestTrip);
        Assert.Null(records.MostVisitedCountry);
        Assert.Null(records.LongestGap);
    }
}
