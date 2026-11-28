using System.Globalization;
using System.Text;

namespace Fibotrail.Trails.Tests;

/// <summary>GPX files built in code: a short walk, a multi-day trip, a file with gaps, and broken ones.</summary>
public static class SampleGpx
{
    public const double KmPerDegreeOfLatitude = 111.195;

    /// <summary>A walk north from a start point: one point every <paramref name="seconds"/> seconds, <paramref name="metersPerStep"/> apart.</summary>
    public static List<TrailPoint> Walk(DateTime start, int points, double latitude = 28.4, double longitude = -81.55, int seconds = 5, double metersPerStep = 7)
    {
        var step = metersPerStep / 1000 / KmPerDegreeOfLatitude;
        return [.. Enumerable.Range(0, points).Select(index => new TrailPoint(start.AddSeconds(index * seconds), latitude + (index * step), longitude, 30))];
    }

    /// <summary>A GPX 1.1 file with one track and one segment for each list.</summary>
    public static string Track(params IEnumerable<TrailPoint>[] segments) => Document("1.1", "http://www.topografix.com/GPX/1/1", Trk(segments));

    /// <summary>A GPX 1.0 file, as older GPSBabel versions write it.</summary>
    public static string Track10(params IEnumerable<TrailPoint>[] segments) => Document("1.0", "http://www.topografix.com/GPX/1/0", Trk(segments));

    /// <summary>A file with only a route, and no track.</summary>
    public static string RouteOnly(IEnumerable<TrailPoint> points) =>
        Document("1.1", "http://www.topografix.com/GPX/1/1", $"<rte><name>Planned</name>{string.Concat(points.Select(point => Point("rtept", point)))}</rte>");

    public static Stream AsStream(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    public static string Point(string name, TrailPoint point) =>
        string.Create(CultureInfo.InvariantCulture, $"<{name} lat=\"{point.Latitude:R}\" lon=\"{point.Longitude:R}\"><ele>{point.Elevation ?? 0}</ele><time>{point.Time:yyyy-MM-dd'T'HH:mm:ss'Z'}</time></{name}>");

    private static string Trk(IEnumerable<TrailPoint>[] segments) =>
        $"<trk><name>Logger</name>{string.Concat(segments.Select(segment => $"<trkseg>{string.Concat(segment.Select(point => Point("trkpt", point)))}</trkseg>"))}</trk>";

    private static string Document(string version, string ns, string body) =>
        $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<gpx version=\"{version}\" creator=\"GPSBabel\" xmlns=\"{ns}\"><wpt lat=\"1\" lon=\"1\"><time>2027-03-15T00:00:00Z</time></wpt>{body}</gpx>";
}
