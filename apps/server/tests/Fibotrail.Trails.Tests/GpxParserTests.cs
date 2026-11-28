namespace Fibotrail.Trails.Tests;

public class GpxParserTests
{
    private static readonly DateTime March15 = new(2027, 3, 15, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_short_walk_gives_its_points_in_order_with_their_UTC_times()
    {
        var walk = SampleGpx.Walk(March15, 20);

        var result = GpxParser.Parse(SampleGpx.AsStream(SampleGpx.Track(walk)));

        Assert.Null(result.Problem);
        var segment = Assert.Single(result.Trail!.Segments);
        Assert.Equal(20, segment.Count);
        Assert.Equal(March15, segment[0].Time);
        Assert.Equal(DateTimeKind.Utc, segment[0].Time.Kind);
        Assert.Equal(walk[^1].Latitude, segment[^1].Latitude, 9);
        Assert.Equal(30, segment[0].Elevation);
    }

    [Fact]
    public void Each_track_segment_stays_its_own_segment()
    {
        var gpx = SampleGpx.Track(SampleGpx.Walk(March15, 5), SampleGpx.Walk(March15.AddDays(1), 7));

        var trail = GpxParser.Parse(SampleGpx.AsStream(gpx)).Trail!;

        Assert.Equal([5, 7], trail.Segments.Select(segment => segment.Count));
        Assert.Equal(12, trail.PointCount);
    }

    [Fact]
    public void A_GPX_1_0_file_is_read_the_same_way()
    {
        var trail = GpxParser.Parse(SampleGpx.AsStream(SampleGpx.Track10(SampleGpx.Walk(March15, 4)))).Trail;

        Assert.Equal(4, trail!.PointCount);
    }

    [Fact]
    public void Waypoints_are_ignored()
    {
        // Every sample file has one waypoint. It adds no point.
        var trail = GpxParser.Parse(SampleGpx.AsStream(SampleGpx.Track(SampleGpx.Walk(March15, 3)))).Trail!;

        Assert.Equal(3, trail.PointCount);
    }

    [Fact]
    public void A_route_is_read_only_when_the_file_has_no_track()
    {
        var routeOnly = GpxParser.Parse(SampleGpx.AsStream(SampleGpx.RouteOnly(SampleGpx.Walk(March15, 6)))).Trail!;
        Assert.Equal(6, routeOnly.PointCount);

        var both = SampleGpx.Track(SampleGpx.Walk(March15, 3)).Replace("</gpx>", $"<rte>{SampleGpx.Point("rtept", SampleGpx.Walk(March15, 1)[0])}</rte></gpx>", StringComparison.Ordinal);
        Assert.Equal(3, GpxParser.Parse(SampleGpx.AsStream(both)).Trail!.PointCount);
    }

    [Fact]
    public void A_point_with_no_time_or_at_0_0_is_skipped()
    {
        var gpx = SampleGpx.Track(SampleGpx.Walk(March15, 3))
            .Replace("<trkseg>", "<trkseg><trkpt lat=\"28.1\" lon=\"-81.1\"><ele>3</ele></trkpt><trkpt lat=\"0\" lon=\"0\"><time>2027-03-15T13:00:00Z</time></trkpt>", StringComparison.Ordinal);

        var trail = GpxParser.Parse(SampleGpx.AsStream(gpx)).Trail!;

        Assert.Equal(3, trail.PointCount);
    }

    [Fact]
    public void A_time_with_an_offset_is_moved_to_UTC()
    {
        var gpx = SampleGpx.Track(SampleGpx.Walk(March15, 1)).Replace("2027-03-15T14:00:00Z", "2027-03-15T10:00:00-04:00", StringComparison.Ordinal);

        var point = GpxParser.Parse(SampleGpx.AsStream(gpx)).Trail!.Segments[0][0];

        Assert.Equal(March15, point.Time);
    }

    [Theory]
    [InlineData("<gpx><trk><trkseg><trkpt lat=\"1\" lon=\"1\"><time>2027-03-15T13:00:00Z</time></trkpt>")]
    [InlineData("{\"type\":\"FeatureCollection\"}")]
    [InlineData("<kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document/></kml>")]
    [InlineData("")]
    public void A_file_that_isnt_well_formed_GPX_is_refused(string text)
    {
        var result = GpxParser.Parse(SampleGpx.AsStream(text));

        Assert.Null(result.Trail);
        Assert.Equal(GpxParser.NotGpx, result.Problem);
    }

    [Fact]
    public void A_file_with_only_waypoints_is_refused()
    {
        var result = GpxParser.Parse(SampleGpx.AsStream("<gpx version=\"1.1\"><wpt lat=\"1\" lon=\"1\"><time>2027-03-15T00:00:00Z</time></wpt><trk><trkseg/></trk></gpx>"));

        Assert.Equal(GpxParser.NoPoints, result.Problem);
    }

    [Fact]
    public void A_file_with_a_DTD_is_refused_so_it_cant_read_other_files()
    {
        var gpx = "<?xml version=\"1.0\"?><!DOCTYPE gpx [<!ENTITY secret SYSTEM \"file:///etc/passwd\">]><gpx><trk><trkseg><trkpt lat=\"1\" lon=\"1\"><name>&secret;</name><time>2027-03-15T13:00:00Z</time></trkpt></trkseg></trk></gpx>";

        Assert.Equal(GpxParser.NotGpx, GpxParser.Parse(SampleGpx.AsStream(gpx)).Problem);
    }
}
