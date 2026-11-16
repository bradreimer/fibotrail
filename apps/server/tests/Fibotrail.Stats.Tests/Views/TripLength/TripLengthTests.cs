using Fibotrail.Places;
using Fibotrail.Stats.Views.TripLength;

namespace Fibotrail.Stats.Tests.Views.TripLength;

public class TripLengthTests
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

        var bins = TripLengthView.Compute(library);

        Assert.Equal([1, 1, 2, 1, 2], bins.Select(bin => bin.Count));
        Assert.Equal(library.Trips.Count, bins.Sum(bin => bin.Count));
    }
}
