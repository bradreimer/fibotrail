using Fibotrail.Detection.Parks;

namespace Fibotrail.Detection.Tests;

/// <summary>Distinct trips across parks: a trip to two parks counts once. Parks are made-up squares.</summary>
public class ParkTripsTests
{
    private static ParkPlace Square(string id, string group, double south, double west) =>
        new(id, id, group, 3, 50, [[[new(south, west), new(south, west + 0.01), new(south + 0.01, west + 0.01), new(south + 0.01, west), new(south, west)]]]);

    // A and B are in one resort. C is in another.
    private static readonly ParkPlace A = Square("A", "East", 0, 0);
    private static readonly ParkPlace B = Square("B", "East", 0, 0.02);
    private static readonly ParkPlace C = Square("C", "West", 10, 10);

    private static readonly DateOnly Day = new(2026, 3, 10);

    private static IEnumerable<ParkPhoto> At(ParkPlace place, int count) =>
        Enumerable.Range(0, count).Select(i => new ParkPhoto(Day.AddDays(i), place.Polygons[0][0][0].Latitude + 0.005, place.Polygons[0][0][0].Longitude + 0.005));

    private static ParkTrip Trip(int id, params IEnumerable<ParkPhoto>[] photos) => new(id, $"Trip {id}", [.. photos.SelectMany(p => p)]);

    private static IReadOnlyList<ParkStats> Count(params ParkTrip[] trips) => ParkDays.Count(trips, new ParkLookup([A, B, C]));

    private static IReadOnlyList<ParkStats> In(IReadOnlyList<ParkStats> stats, params string[] ids) => [.. stats.Where(item => ids.Contains(item.Place.Id))];

    [Fact]
    public void One_trip_to_one_park_is_one_trip() =>
        Assert.Equal(1, ParkTrips.Distinct(Count(Trip(1, At(A, 3)))));

    [Fact]
    public void One_trip_to_two_parks_of_one_resort_counts_once()
    {
        var stats = Count(Trip(1, At(A, 3), At(B, 3)));

        Assert.Equal(1, ParkTrips.Distinct(In(stats, "A", "B")));
        Assert.All(stats, park => Assert.Single(park.Trips));
    }

    [Fact]
    public void One_trip_to_two_resorts_counts_once_for_each_and_once_overall()
    {
        var stats = Count(Trip(1, At(A, 3), At(C, 3)));

        Assert.Equal(1, ParkTrips.Distinct(stats));
        Assert.Equal(1, ParkTrips.Distinct(In(stats, "A", "B")));
        Assert.Equal(1, ParkTrips.Distinct(In(stats, "C")));
    }

    [Fact]
    public void A_trip_with_too_few_photos_adds_nothing() =>
        Assert.Equal(0, ParkTrips.Distinct(Count(Trip(1, At(A, 2)))));

    [Fact]
    public void Two_trips_to_one_park_are_two_trips() =>
        Assert.Equal(2, ParkTrips.Distinct(Count(Trip(1, At(A, 3)), Trip(2, At(A, 3)))));

    [Fact]
    public void No_parks_means_no_trips() => Assert.Equal(0, ParkTrips.Distinct([]));
}
