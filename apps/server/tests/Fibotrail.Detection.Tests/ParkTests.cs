using Fibotrail.Detection.Parks;

namespace Fibotrail.Detection.Tests;

/// <summary>The park lookup and the park-day count. The parks are made-up squares near the equator.</summary>
public class ParkTests
{
    // Each square is about 1.1 km on a side. A is at 0..0.01 degrees, B sits right next to it.
    private static ParkPlace Square(string id, double south, double west, double buffer = 50, int minPhotos = 3) =>
        new(id, id, "Resort", minPhotos, buffer, [[[new(south, west), new(south, west + 0.01), new(south + 0.01, west + 0.01), new(south + 0.01, west), new(south, west)]]]);

    private static readonly ParkPlace A = Square("A", 0, 0);
    private static readonly ParkPlace B = Square("B", 0, 0.01005);

    private static ParkLookup Lookup(params ParkPlace[] places) => new(places);

    private static readonly DateOnly Day1 = new(2026, 3, 10);

    private static ParkPhoto Photo(int dayOffset, double latitude, double longitude) => new(Day1.AddDays(dayOffset), latitude, longitude);

    [Fact]
    public void A_position_inside_the_boundary_is_in_the_park()
    {
        Assert.Equal("A", Lookup(A).Find(0.005, 0.005)?.Id);
    }

    [Fact]
    public void A_position_just_outside_is_in_the_park_only_within_the_buffer()
    {
        // 0.0002 degrees is about 22 m, inside the 50 m buffer. 0.001 degrees is about 110 m.
        Assert.Equal("A", Lookup(A).Find(-0.0002, 0.005)?.Id);
        Assert.Null(Lookup(A).Find(-0.001, 0.005));
    }

    [Fact]
    public void A_position_far_away_is_in_no_park() => Assert.Null(Lookup(A, B).Find(40, 40));

    [Fact]
    public void A_photo_in_two_buffers_belongs_to_the_nearer_boundary()
    {
        var lookup = Lookup(A, B);

        // Just inside A and just inside A's buffer of B, and the other way around.
        Assert.Equal("A", lookup.Find(0.005, 0.00998)?.Id);
        Assert.Equal("B", lookup.Find(0.005, 0.01010)?.Id);
    }

    [Fact]
    public void A_hole_in_a_polygon_is_outside_the_park()
    {
        var park = new ParkPlace("H", "H", "Resort", 3, 0,
        [[
            [new(0, 0), new(0, 0.01), new(0.01, 0.01), new(0.01, 0), new(0, 0)],
            [new(0.004, 0.004), new(0.004, 0.006), new(0.006, 0.006), new(0.006, 0.004), new(0.004, 0.004)],
        ]]);

        Assert.Null(Lookup(park).Find(0.005, 0.005));
        Assert.Equal("H", Lookup(park).Find(0.001, 0.001)?.Id);
    }

    [Fact]
    public void Three_days_with_at_least_three_photos_in_all_give_3_days_and_1_trip()
    {
        var trip = new ParkTrip(1, "Orlando", [Photo(0, 0.005, 0.005), Photo(1, 0.005, 0.005), Photo(2, 0.005, 0.005)]);

        var stats = Assert.Single(ParkDays.Count([trip], Lookup(A)));

        Assert.Equal((3, 1, Day1), (stats.Days, stats.Trips.Count, stats.FirstVisit));
    }

    [Fact]
    public void Two_trips_add_their_days_and_both_are_listed()
    {
        var first = new ParkTrip(1, "March", [Photo(0, 0.005, 0.005), Photo(1, 0.005, 0.005), Photo(2, 0.005, 0.005)]);
        var second = new ParkTrip(2, "June", [Photo(90, 0.005, 0.005), Photo(90, 0.005, 0.005), Photo(91, 0.005, 0.005)]);

        var stats = Assert.Single(ParkDays.Count([second, first], Lookup(A)));

        Assert.Equal(5, stats.Days);
        Assert.Equal([1, 2], stats.Trips.Select(visit => visit.TripId));
        Assert.Equal([3, 2], stats.Trips.Select(visit => visit.Days));
    }

    [Fact]
    public void A_day_with_photos_in_two_parks_counts_for_both()
    {
        var trip = new ParkTrip(1, "Orlando",
        [
            Photo(0, 0.005, 0.005), Photo(0, 0.005, 0.005), Photo(0, 0.005, 0.005),
            Photo(0, 0.005, 0.0151), Photo(0, 0.005, 0.0151), Photo(0, 0.005, 0.0151),
        ]);

        var stats = ParkDays.Count([trip], Lookup(A, B));

        Assert.Equal(2, stats.Count);
        Assert.All(stats, park => Assert.Equal(1, park.Days));
    }

    [Fact]
    public void A_trip_with_only_two_photos_in_a_park_doesnt_visit_it()
    {
        var trip = new ParkTrip(1, "Orlando", [Photo(0, 0.005, 0.005), Photo(1, 0.005, 0.005)]);

        Assert.Empty(ParkDays.Count([trip], Lookup(A)));
    }

    [Fact]
    public void A_stray_photo_on_a_quiet_day_still_counts_when_the_trip_visits_the_park()
    {
        var trip = new ParkTrip(1, "Orlando", [Photo(0, 0.005, 0.005), Photo(0, 0.005, 0.005), Photo(0, 0.005, 0.005), Photo(1, 0.005, 0.005)]);

        Assert.Equal(2, Assert.Single(ParkDays.Count([trip], Lookup(A))).Days);
    }

    [Fact]
    public void Photos_outside_every_park_add_no_day()
    {
        var trip = new ParkTrip(1, "Orlando", [Photo(0, 5, 5), Photo(1, 5, 5), Photo(2, 5, 5)]);

        Assert.Empty(ParkDays.Count([trip], Lookup(A)));
    }

    [Fact]
    public void A_trip_that_crosses_midnight_counts_each_local_day_it_has_photos_on()
    {
        var trip = new ParkTrip(1, "Late", [Photo(0, 0.005, 0.005), Photo(0, 0.005, 0.005), Photo(1, 0.005, 0.005)]);

        Assert.Equal(2, Assert.Single(ParkDays.Count([trip], Lookup(A))).Days);
    }

    [Fact]
    public void Parks_come_most_days_first()
    {
        var trip = new ParkTrip(1, "Orlando",
        [
            Photo(0, 0.005, 0.0151), Photo(1, 0.005, 0.0151), Photo(2, 0.005, 0.0151),
            Photo(0, 0.005, 0.005), Photo(0, 0.005, 0.005), Photo(0, 0.005, 0.005),
        ]);

        Assert.Equal(["B", "A"], ParkDays.Count([trip], Lookup(A, B)).Select(stats => stats.Place.Id));
    }
}
