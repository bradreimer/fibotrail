using Fibotrail.Places;
using Fibotrail.Stats.Views.TripsPerYear;

namespace Fibotrail.Stats.Tests.Views.TripsPerYear;

public class TripsPerYearTests
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

        var years = TripsPerYearView.Compute(library);

        Assert.Equal([2019, 2020, 2021], years.Select(year => year.Year));
        Assert.Equal([1, 0, 2], years.Select(year => year.Count));
    }

    [Fact]
    public void Trips_per_year_is_empty_with_no_trips() => Assert.Empty(TripsPerYearView.Compute(Library()));
}
