using static Fibotrail.Detection.Tests.Synthetic;

namespace Fibotrail.Detection.Tests;

/// <summary>A trip you enter by hand, for photos that carry no position.</summary>
public class ManualTripTests
{
    private static readonly GeoPoint SanFrancisco = new(37.7749, -122.4194);

    private static readonly ManualTripSpec Spec = new(new DateOnly(2005, 7, 31), new DateOnly(2005, 8, 6), "Bay Area", "San Francisco", "United States", SanFrancisco);

    /// <summary>A week of photos with no position, as a 2005 camera took them.</summary>
    private static List<PhotoPoint> Camera() => At(null, 40, Time(2005, 7, 31, 19), Time(2005, 8, 6, 0));

    [Fact]
    public void Every_photo_in_the_days_belongs_to_the_trip_whether_or_not_it_has_a_position()
    {
        var photos = Camera();
        photos.AddRange(At(null, 5, Time(2005, 7, 20), Time(2005, 7, 21)));
        photos.AddRange(At(null, 5, Time(2005, 8, 7), Time(2005, 8, 8)));

        var trip = ManualTrips.Build(photos, Spec, Home);

        Assert.NotNull(trip);
        Assert.Equal(40, trip.PhotoIds.Count);
        Assert.Equal(7, trip.Days);
        Assert.Equal(new DateOnly(2005, 7, 31), trip.StartDate);
        Assert.Equal(new DateOnly(2005, 8, 6), trip.EndDate);
    }

    [Fact]
    public void Photos_with_no_position_make_one_stop_at_the_position_you_gave()
    {
        var trip = ManualTrips.Build(Camera(), Spec, Home)!;

        var stop = Assert.Single(trip.Stops);
        Assert.Equal(SanFrancisco, stop.Center);
        Assert.Equal("San Francisco", stop.Place);
        Assert.Equal(40, stop.PhotoIds.Count);
        Assert.Equal(0, trip.DistanceKm);
        Assert.Equal(Geo.DistanceKm(Home, SanFrancisco), trip.FarthestFromHomeKm, 3);
        Assert.Equal(trip.PhotoIds[0], trip.CoverPhotoId);
    }

    [Fact]
    public void The_trip_has_your_name_place_and_country_and_is_High_confidence()
    {
        var trip = ManualTrips.Build(Camera(), Spec, Home)!;

        Assert.Equal("Bay Area", trip.Name);
        Assert.Equal("San Francisco", trip.MainPlace);
        Assert.Equal("United States", trip.Country);
        Assert.Equal(Confidence.High, trip.Confidence);
        Assert.Equal(TripDetector.Fingerprint(new DateOnly(2005, 7, 31), "San Francisco"), trip.Fingerprint);
    }

    [Fact]
    public void Without_a_name_the_trip_is_named_like_a_suggestion()
    {
        var trip = ManualTrips.Build(Camera(), Spec with { Name = null }, Home)!;

        Assert.Equal("San Francisco, July 2005", trip.Name);
    }

    [Fact]
    public void Without_a_position_the_trip_has_no_stop()
    {
        var trip = ManualTrips.Build(Camera(), Spec with { Position = null }, Home)!;

        Assert.Empty(trip.Stops);
        Assert.Equal(0, trip.FarthestFromHomeKm);
        Assert.Equal(40, trip.PhotoIds.Count);
    }

    [Fact]
    public void When_photos_have_a_position_the_stops_come_from_them()
    {
        var photos = At(Park, 30, Time(2005, 8, 1, 9), Time(2005, 8, 3, 18), "Orlando", "Florida", "United States");

        var trip = ManualTrips.Build(photos, Spec, Home)!;

        var stop = Assert.Single(trip.Stops);
        Assert.Equal(Park.Latitude, stop.Center.Latitude, 3);
        Assert.Equal(30, trip.AwayPhotoCount);
        Assert.Equal("San Francisco", trip.MainPlace);
    }

    [Fact]
    public void With_no_country_given_the_trip_takes_the_one_the_photos_name()
    {
        var photos = At(Park, 30, Time(2005, 8, 1, 9), Time(2005, 8, 3, 18), "Orlando", "Florida", "United States");

        Assert.Equal("United States", ManualTrips.Build(photos, Spec with { Country = null }, Home)!.Country);
    }

    [Fact]
    public void No_photo_in_the_days_gives_no_trip()
    {
        var photos = At(null, 10, Time(2005, 9, 1), Time(2005, 9, 2));

        Assert.Null(ManualTrips.Build(photos, Spec, Home));
    }

    [Fact]
    public void The_same_photos_always_give_the_same_trip()
    {
        var photos = Camera();

        Assert.Equal(ManualTrips.Build(photos, Spec, Home)!.Fingerprint, ManualTrips.Build([.. photos.AsEnumerable().Reverse()], Spec, Home)!.Fingerprint);
    }
}
