using Fibotrail.Detection;
using Fibotrail.Places;
using Fibotrail.Stats.Views.DistanceRings;

namespace Fibotrail.Stats.Tests.Views.DistanceRings;

public class DistanceRingsTests
{
    private static StatsLibrary Library(GeoPoint? home, params (int Id, (double Lat, double Lon)[] Stops)[] trips)
    {
        CountryOutline[] outlines = [];
        var day = new DateOnly(2024, 1, 1);
        return StatsLibrary.Build(
            trips.Select(trip => new StatsTrip(trip.Id, $"Trip {trip.Id}", day, day.AddDays(2), 3, 0, 0, trip.Stops, [])),
            new CountryFinder(outlines),
            outlines,
            home);
    }

    [Fact]
    public void A_trip_due_north_of_home_has_bearing_0_and_the_distance_of_its_farthest_stop()
    {
        var rings = DistanceRingsView.Compute(Library(new GeoPoint(0, 0), (1, [(1, 0), (9.0, 0)])));

        var ring = Assert.Single(rings);
        Assert.Equal(0, ring.BearingDegrees);
        Assert.InRange(ring.DistanceKm, 998, 1002);
    }

    [Fact]
    public void Trips_come_nearest_first_and_a_trip_with_no_stop_is_left_out()
    {
        var rings = DistanceRingsView.Compute(Library(
            new GeoPoint(0, 0),
            (1, [(0, 20)]),
            (2, [(0, 2)]),
            (3, [])));

        Assert.Equal([2, 1], rings.Select(ring => ring.Trip.Id));
        Assert.Equal(90, rings[0].BearingDegrees);
    }

    [Fact]
    public void A_trip_across_the_antimeridian_takes_the_shorter_way()
    {
        var rings = DistanceRingsView.Compute(Library(new GeoPoint(21.3, -157.9), (1, [(35.7, 139.7)])));

        Assert.InRange(Assert.Single(rings).BearingDegrees, 298, 301);
    }

    [Fact]
    public void With_no_home_location_no_trip_is_placed() =>
        Assert.Empty(DistanceRingsView.Compute(Library(null, (1, [(1, 1)]))));
}
