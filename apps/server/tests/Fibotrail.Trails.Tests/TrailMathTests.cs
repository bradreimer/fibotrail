namespace Fibotrail.Trails.Tests;

public class TrailMathTests
{
    private static readonly DateTime March15 = new(2027, 3, 15, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_10_minute_gap_splits_a_segment_in_two()
    {
        var before = SampleGpx.Walk(March15, 10);
        var after = SampleGpx.Walk(before[^1].Time.AddMinutes(10), 10, before[^1].Latitude);

        var split = TrailMath.SplitAtGaps([[.. before, .. after]]);

        Assert.Equal([10, 10], split.Select(segment => segment.Count));
    }

    [Fact]
    public void A_jump_of_more_than_1_km_splits_a_segment_even_when_it_is_quick()
    {
        var before = SampleGpx.Walk(March15, 5);
        var after = SampleGpx.Walk(before[^1].Time.AddSeconds(5), 5, before[^1].Latitude + (2 / SampleGpx.KmPerDegreeOfLatitude));

        Assert.Equal(2, TrailMath.SplitAtGaps([[.. before, .. after]]).Count);
    }

    [Fact]
    public void A_steady_walk_isnt_split()
    {
        Assert.Single(TrailMath.SplitAtGaps([SampleGpx.Walk(March15, 500)]));
    }

    [Fact]
    public void Distance_is_the_sum_of_the_steps_and_leaves_out_the_step_across_a_gap()
    {
        // Two walks of 99 steps of 10 m, 5 km apart.
        var first = SampleGpx.Walk(March15, 100, metersPerStep: 10);
        var second = SampleGpx.Walk(March15.AddHours(2), 100, first[^1].Latitude + (5 / SampleGpx.KmPerDegreeOfLatitude), metersPerStep: 10);

        var summary = TrailMath.Summarize(TrailMath.SplitAtGaps([[.. first, .. second]]));

        Assert.Equal(1.98, summary.DistanceKm, 2);
        Assert.Equal(200, summary.PointCount);
        Assert.Equal(March15, summary.Start);
        Assert.Equal(second[^1].Time, summary.End);
    }

    [Fact]
    public void Moving_time_leaves_out_standing_still()
    {
        // 60 steps of 7 m in 5 s is about 5 km/h. Then 60 steps of 0 m: standing still.
        var walking = SampleGpx.Walk(March15, 61);
        var standing = SampleGpx.Walk(walking[^1].Time.AddSeconds(5), 60, walking[^1].Latitude, metersPerStep: 0);

        var summary = TrailMath.Summarize(TrailMath.SplitAtGaps([[.. walking, .. standing]]));

        Assert.Equal(TimeSpan.FromMinutes(5), summary.MovingTime);
    }

    [Fact]
    public void A_multi_day_trip_spans_its_days()
    {
        var days = Enumerable.Range(0, 4).Select(day => (IReadOnlyList<TrailPoint>)SampleGpx.Walk(March15.AddDays(day), 100)).ToList();

        var segments = TrailMath.SplitAtGaps(days);
        var summary = TrailMath.Summarize(segments);

        Assert.Equal(4, segments.Count);
        Assert.Equal(TimeSpan.FromDays(3) + TimeSpan.FromSeconds(495), summary.End - summary.Start);
    }

    [Fact]
    public void A_straight_line_simplifies_to_its_two_ends()
    {
        var walk = SampleGpx.Walk(March15, 1000);

        var line = Assert.Single(TrailMath.Simplify([walk]));

        Assert.Equal(2, line.Count);
        Assert.Equal(walk[0].Latitude, line[0].Latitude);
        Assert.Equal(walk[^1].Latitude, line[^1].Latitude);
    }

    [Fact]
    public void A_corner_is_kept()
    {
        var north = SampleGpx.Walk(March15, 50);
        var east = Enumerable.Range(1, 50)
            .Select(index => new TrailPoint(north[^1].Time.AddSeconds(index * 5), north[^1].Latitude, north[^1].Longitude + (index * 0.0001)))
            .ToList();

        var line = Assert.Single(TrailMath.Simplify([[.. north, .. east]]));

        Assert.Equal(3, line.Count);
    }

    [Fact]
    public void A_trail_of_100000_points_sends_no_more_than_5000()
    {
        // A winding walk of a day: it bends every few hundred points, so 5 m alone can't thin it to 5,000.
        var points = Enumerable.Range(0, 100_000)
            .Select(index => new TrailPoint(March15.AddSeconds(index), 28.4 + (index * 0.00002), -81.55 + (0.002 * Math.Sin(index / 40.0)) + (0.0004 * Math.Sin(index / 7.0))))
            .ToList();

        var lines = TrailMath.Simplify([points]);

        Assert.InRange(lines.Sum(line => line.Count), 2, TrailMath.MaxDisplayPoints);
    }

    [Theory]
    [InlineData("2027-03-15T14:00:00Z", "2027-03-18T20:00:00Z", true)]
    [InlineData("2027-03-10T14:00:00Z", "2027-03-15T10:00:00Z", true)]
    [InlineData("2027-03-20T00:00:00Z", "2027-03-20T12:00:00Z", false)]
    [InlineData("2027-03-01T00:00:00Z", "2027-03-02T12:00:00Z", false)]
    public void Overlap_compares_the_trails_UTC_span_with_the_trips_local_days(string start, string end, bool overlaps)
    {
        var summary = new TrailSummary(DateTime.Parse(start, System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime(), DateTime.Parse(end, System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime(), 1, TimeSpan.Zero, 2);

        Assert.Equal(overlaps, TrailMath.Overlaps(summary, new DateOnly(2027, 3, 15), new DateOnly(2027, 3, 18)));
    }

    [Fact]
    public void A_trail_in_Tokyo_on_the_trips_last_local_evening_overlaps()
    {
        // 23:00 in Tokyo on March 18 is 14:00 UTC, the same day. 07:00 on March 19 in Tokyo is 22:00 UTC on March 18.
        var summary = new TrailSummary(new DateTime(2027, 3, 18, 14, 0, 0, DateTimeKind.Utc), new DateTime(2027, 3, 18, 22, 0, 0, DateTimeKind.Utc), 1, TimeSpan.Zero, 2);

        Assert.True(TrailMath.Overlaps(summary, new DateOnly(2027, 3, 15), new DateOnly(2027, 3, 18)));
    }

    [Fact]
    public void A_daylight_saving_change_doesnt_break_the_steps()
    {
        // US clocks moved forward at 07:00 UTC on March 14, 2027. GPX times are UTC, so the walk is one even segment.
        var walk = SampleGpx.Walk(new DateTime(2027, 3, 14, 6, 50, 0, DateTimeKind.Utc), 240);

        var segments = TrailMath.SplitAtGaps([walk]);

        Assert.Single(segments);
        Assert.Equal(TimeSpan.FromSeconds(239 * 5), TrailMath.Summarize(segments).MovingTime);
    }
}
