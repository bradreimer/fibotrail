using Fibotrail.Detection;

namespace Fibotrail.Trails.Tests;

public class PhotoAlignmentTests
{
    private static readonly DateTime Start = new(2027, 3, 15, 14, 0, 0, DateTimeKind.Utc);

    /// <summary>A walk north, 1 km an hour slower than a car: one point a minute, 80 m apart, for 3 hours.</summary>
    private static List<TrailPoint> Walk(DateTime start, int minutes = 180, double latitude = 28.4) =>
        SampleGpx.Walk(start, minutes + 1, latitude, seconds: 60, metersPerStep: 80);

    /// <summary>A photo taken on the trail at a UTC moment, shown in local time with the offset, at its true position.</summary>
    private static AlignPhoto PlacedAt(List<TrailPoint> walk, int minute, int offsetMinutes, string? id = null) =>
        new(id ?? $"p{minute}", DateTime.SpecifyKind(walk[minute].Time.AddMinutes(offsetMinutes), DateTimeKind.Unspecified), walk[minute].Latitude, walk[minute].Longitude);

    private static int[] Minutes(params int[] minutes) => minutes;

    private static double MetersBetween(TrailPosition position, double latitude, double longitude) =>
        Geo.DistanceKm(new GeoPoint(position.Latitude, position.Longitude), new GeoPoint(latitude, longitude)) * 1000;

    [Fact]
    public void A_photo_between_two_points_is_placed_in_proportion_to_its_time()
    {
        var walk = Walk(Start);
        // 30 seconds after minute 10, local time 5 hours ahead.
        var local = DateTime.SpecifyKind(walk[10].Time.AddSeconds(30).AddHours(5), DateTimeKind.Unspecified);

        var position = PhotoAlignment.PositionAt([walk], local, 300)!;

        Assert.Equal((walk[10].Latitude + walk[11].Latitude) / 2, position.Latitude, 9);
        Assert.Equal(walk[10].Longitude, position.Longitude, 9);
    }

    [Fact]
    public void A_photo_at_a_points_exact_time_is_placed_on_that_point()
    {
        var walk = Walk(Start);

        var position = PhotoAlignment.PositionAt([walk], walk[42].Time, 0)!;

        Assert.Equal(walk[42].Latitude, position.Latitude, 12);
    }

    [Fact]
    public void A_photo_before_or_after_the_trail_or_in_a_gap_is_outside()
    {
        var morning = Walk(Start, 60);
        var afternoon = Walk(Start.AddHours(3), 60, morning[^1].Latitude);
        var segments = new List<IReadOnlyList<TrailPoint>> { morning, afternoon };

        Assert.Null(PhotoAlignment.PositionAt(segments, Start.AddMinutes(-1), 0));
        Assert.Null(PhotoAlignment.PositionAt(segments, Start.AddHours(2), 0));
        Assert.Null(PhotoAlignment.PositionAt(segments, Start.AddHours(5), 0));
        Assert.NotNull(PhotoAlignment.PositionAt(segments, Start.AddHours(3).AddMinutes(30), 0));
    }

    [Fact]
    public void Align_keeps_the_order_and_marks_the_photos_outside()
    {
        var walk = Walk(Start);
        var photos = new[] { new AlignPhoto("before", Start.AddHours(-1)), new AlignPhoto("on", Start.AddHours(1)), new AlignPhoto("after", Start.AddHours(9)) };

        var aligned = PhotoAlignment.Align(photos, [walk], 0);

        Assert.Equal(["before", "on", "after"], aligned.Select(photo => photo.Photo.Id));
        Assert.Equal([true, false, true], aligned.Select(photo => photo.Outside));
    }

    [Fact]
    public void The_estimate_finds_a_5_hour_zone_and_a_7_minute_camera_drift()
    {
        var walk = Walk(Start);
        const int offset = (5 * 60) + 7;
        var photos = Minutes(5, 20, 40, 75, 110, 150, 170).Select(minute => PlacedAt(walk, minute, offset)).ToList();

        var estimate = PhotoAlignment.EstimateOffset(photos, [walk]);

        Assert.Equal(offset, estimate);
        var distances = PhotoAlignment.Align(photos, [walk], estimate!.Value)
            .Select(photo => MetersBetween(photo.Position!, photo.Photo.Latitude!.Value, photo.Photo.Longitude!.Value))
            .Order()
            .ToList();
        Assert.True(distances[distances.Count / 2] < 100);
    }

    [Fact]
    public void The_estimate_finds_a_zone_behind_UTC()
    {
        var walk = Walk(Start);
        var photos = Minutes(10, 60, 120, 170).Select(minute => PlacedAt(walk, minute, -8 * 60)).ToList();

        Assert.Equal(-8 * 60, PhotoAlignment.EstimateOffset(photos, [walk]));
    }

    [Fact]
    public void Photos_with_no_position_dont_count_toward_the_estimate()
    {
        var walk = Walk(Start);
        var photos = new List<AlignPhoto> { PlacedAt(walk, 10, 60), PlacedAt(walk, 90, 60) };
        photos.AddRange(Enumerable.Range(0, 20).Select(index => new AlignPhoto($"camera{index}", Start.AddMinutes(index * 5))));

        Assert.Null(PhotoAlignment.EstimateOffset(photos, [walk]));
    }

    [Fact]
    public void Photos_far_from_the_trail_give_no_estimate()
    {
        var walk = Walk(Start);
        // Taken during the walk's time, but 50 km away.
        var photos = Minutes(10, 60, 120, 170)
            .Select(minute => PlacedAt(walk, minute, 0) with { Latitude = walk[minute].Latitude + 0.45 })
            .ToList();

        Assert.Null(PhotoAlignment.EstimateOffset(photos, [walk]));
    }

    [Fact]
    public void Across_a_daylight_saving_change_the_estimate_fits_the_most_photos()
    {
        // US clocks moved forward on March 14, 2027. Six photos before at UTC−5, four after at UTC−4.
        var walk = Walk(new DateTime(2027, 3, 14, 4, 0, 0, DateTimeKind.Utc), 6 * 60);
        var before = Minutes(5, 20, 35, 50, 65, 80).Select(minute => PlacedAt(walk, minute, -5 * 60));
        var after = Minutes(250, 280, 310, 340).Select(minute => PlacedAt(walk, minute, -4 * 60));

        Assert.Equal(-5 * 60, PhotoAlignment.EstimateOffset([.. before, .. after], [walk]));
    }

    [Fact]
    public void An_estimate_over_many_photos_and_segments_is_quick()
    {
        var days = Enumerable.Range(0, 7).Select(day => (IReadOnlyList<TrailPoint>)SampleGpx.Walk(Start.AddDays(day), 36_000, seconds: 1, metersPerStep: 1.4)).ToList();
        var photos = Enumerable.Range(0, 2000)
            .Select(index => days[index % 7][(index * 17) % 36_000])
            .Select((point, index) => new AlignPhoto($"p{index}", DateTime.SpecifyKind(point.Time.AddHours(2), DateTimeKind.Unspecified), point.Latitude, point.Longitude))
            .ToList();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var estimate = PhotoAlignment.EstimateOffset(photos, days);
        watch.Stop();

        Assert.Equal(120, estimate);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"The estimate took {watch.Elapsed.TotalSeconds:0.0} seconds.");
    }
}
