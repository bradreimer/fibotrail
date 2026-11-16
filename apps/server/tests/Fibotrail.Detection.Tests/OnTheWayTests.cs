using static Fibotrail.Detection.Tests.Synthetic;

namespace Fibotrail.Detection.Tests;

/// <summary>Photos on the way belong to the trip but add no stop, line, or distance. See docs/specs/on-the-way.md.</summary>
public class OnTheWayTests
{
    /// <summary>A layover farther from home than the destination (London, about 7,600 km; Orlando is about 4,200 km).</summary>
    private static readonly GeoPoint Layover = new(51.47, -0.45);

    private static (List<PhotoPoint> Photos, IReadOnlySet<string> LayoverIds) LayoverThenOrlando()
    {
        var layover = At(Layover, 6, Time(2027, 3, 14, 20), Time(2027, 3, 14, 22), "Hounslow", "England", "United Kingdom");
        var orlando = At(Park, 30, Time(2027, 3, 15, 9), Time(2027, 3, 17, 18), "Orlando", "Florida", "United States of America");
        return ([.. layover, .. orlando], layover.Select(photo => photo.Id).ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void Without_a_photo_on_the_way_the_layover_is_a_stop()
    {
        var (photos, _) = LayoverThenOrlando();

        var trip = Assert.Single(TripDetector.Detect(photos, Home));

        Assert.Equal(["Hounslow", "Orlando"], trip.Stops.Select(stop => stop.Place));
        Assert.True(trip.DistanceKm > 1000);
    }

    [Fact]
    public void A_stop_made_only_of_photos_on_the_way_disappears_with_its_line_and_distance()
    {
        var (photos, layoverIds) = LayoverThenOrlando();
        var all = Assert.Single(TripDetector.Detect(photos, Home));

        var trip = Assert.Single(TripDetector.Detect(photos, Home, onTheWay: layoverIds));

        Assert.Equal(["Orlando"], trip.Stops.Select(stop => stop.Place));
        Assert.Equal(0, trip.DistanceKm);
        Assert.True(trip.FarthestFromHomeKm < all.FarthestFromHomeKm);
        Assert.InRange(trip.FarthestFromHomeKm, Geo.DistanceKm(Home, Park) - 1, Geo.DistanceKm(Home, Park) + 1);
    }

    [Fact]
    public void Photos_on_the_way_still_count_toward_the_trip()
    {
        var (photos, layoverIds) = LayoverThenOrlando();
        var all = Assert.Single(TripDetector.Detect(photos, Home));

        var trip = Assert.Single(TripDetector.Detect(photos, Home, onTheWay: layoverIds));

        Assert.Equal(all.PhotoIds, trip.PhotoIds);
        Assert.Equal(all.AwayPhotoCount, trip.AwayPhotoCount);
        Assert.Equal((all.StartDate, all.EndDate, all.Days), (trip.StartDate, trip.EndDate, trip.Days));
        Assert.Equal(all.Fingerprint, trip.Fingerprint);
        Assert.Equal(all.Name, trip.Name);
    }

    [Fact]
    public void A_stop_with_some_photos_on_the_way_keeps_its_pin_and_counts_the_rest()
    {
        var orlando = At(Park, 30, Time(2027, 3, 15, 9), Time(2027, 3, 17, 18), "Orlando", "Florida", "United States of America");
        var hidden = orlando.Take(10).Select(photo => photo.Id).ToHashSet(StringComparer.Ordinal);

        var stop = Assert.Single(Assert.Single(TripDetector.Detect(orlando, Home, onTheWay: hidden)).Stops);

        Assert.Equal(20, stop.PhotoIds.Count);
        Assert.DoesNotContain(stop.PhotoIds, hidden.Contains);
    }

    [Fact]
    public void A_trip_whose_away_photos_are_all_on_the_way_has_no_stop_and_keeps_a_cover()
    {
        var orlando = At(Park, 30, Time(2027, 3, 15, 9), Time(2027, 3, 17, 18), "Orlando", "Florida", "United States of America");
        var hidden = orlando.Select(photo => photo.Id).ToHashSet(StringComparer.Ordinal);

        var trip = Assert.Single(TripDetector.Detect(orlando, Home, onTheWay: hidden));

        Assert.Empty(trip.Stops);
        Assert.Equal((0, 0), (trip.DistanceKm, trip.FarthestFromHomeKm));
        Assert.Contains(trip.CoverPhotoId, trip.PhotoIds);
    }

    [Fact]
    public void An_empty_set_changes_nothing()
    {
        var (photos, _) = LayoverThenOrlando();

        var without = Assert.Single(TripDetector.Detect(photos, Home));
        var empty = Assert.Single(TripDetector.Detect(photos, Home, onTheWay: new HashSet<string>()));

        Assert.Equal(without.Stops.Select(stop => (stop.Place, stop.PhotoIds.Count)), empty.Stops.Select(stop => (stop.Place, stop.PhotoIds.Count)));
        Assert.Equal((without.DistanceKm, without.FarthestFromHomeKm, without.CoverPhotoId), (empty.DistanceKm, empty.FarthestFromHomeKm, empty.CoverPhotoId));
    }

    [Fact]
    public void Measure_gives_the_geometry_that_detection_stores()
    {
        var (photos, layoverIds) = LayoverThenOrlando();
        var trip = Assert.Single(TripDetector.Detect(photos, Home, onTheWay: layoverIds));
        var away = photos.OrderBy(photo => photo.LocalTime).ThenBy(photo => photo.Id, StringComparer.Ordinal).ToList();

        var geometry = TripDetector.Measure(away, trip.PhotoIds, Home, DetectionSettings.Default, layoverIds);

        Assert.Equal(trip.Stops.Select(stop => stop.PhotoIds), geometry.Stops.Select(stop => stop.PhotoIds));
        Assert.Equal((trip.DistanceKm, trip.FarthestFromHomeKm, trip.CoverPhotoId), (geometry.DistanceKm, geometry.FarthestFromHomeKm, geometry.CoverPhotoId));
    }
}
