using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Fibotrail.Trails;

/// <summary>
/// Reads a GPX 1.0 or 1.1 file. It reads tracks and their segments, ignores waypoints, and reads
/// routes only when the file has no track. A point with no time, or at 0, 0, is skipped. See
/// docs/specs/gps-trails.md.
/// </summary>
public static class GpxParser
{
    public const string NotGpx = "This file isn't a GPX file. Convert the logger's files to GPX with GPSBabel, then try again.";
    public const string NoPoints = "This GPX file has no track points with a time. Fibotrail needs a recorded track, not only waypoints.";

    /// <summary>Parses a GPX file. A file that isn't GPX, or has no usable point, is refused with a plain reason.</summary>
    public static GpxParseResult Parse(Stream gpx)
    {
        ArgumentNullException.ThrowIfNull(gpx);

        // No DTD and no outside entities, so a file can't make the parser read anything else.
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true,
        };

        var tracks = new List<IReadOnlyList<TrailPoint>>();
        var routes = new List<IReadOnlyList<TrailPoint>>();
        try
        {
            using var reader = XmlReader.Create(gpx, settings);
            if (reader.MoveToContent() != XmlNodeType.Element || reader.LocalName != "gpx")
            {
                return GpxParseResult.Refused(NotGpx);
            }

            List<TrailPoint>? segment = null;
            var inRoute = false;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.LocalName)
                    {
                        case "trkseg":
                            segment = [];
                            inRoute = false;
                            if (reader.IsEmptyElement)
                            {
                                segment = null;
                            }

                            break;
                        case "rte":
                            segment = [];
                            inRoute = true;
                            if (reader.IsEmptyElement)
                            {
                                segment = null;
                            }

                            break;
                        case "trkpt" or "rtept" when segment is not null:
                            if (ReadPoint(reader) is { } point)
                            {
                                segment.Add(point);
                            }

                            break;
                        default:
                            break;
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName is "trkseg" or "rte" && segment is not null)
                {
                    if (segment.Count > 0)
                    {
                        (inRoute ? routes : tracks).Add(segment);
                    }

                    segment = null;
                }
            }
        }
        catch (XmlException)
        {
            return GpxParseResult.Refused(NotGpx);
        }

        var segments = tracks.Count > 0 ? tracks : routes;
        return segments.Count == 0
            ? GpxParseResult.Refused(NoPoints)
            : new GpxParseResult(new GpxTrail(segments), null);
    }

    /// <summary>
    /// Reads one point and its children. The reader is left on the point's end, so the caller's next
    /// read moves on. Returns null for a point that can't be placed in time and space.
    /// </summary>
    private static TrailPoint? ReadPoint(XmlReader reader)
    {
        using var subtree = reader.ReadSubtree();
        var point = XElement.Load(subtree);
        var latitude = ReadNumber((string?)point.Attribute("lat"));
        var longitude = ReadNumber((string?)point.Attribute("lon"));
        var time = point.Elements().FirstOrDefault(child => child.Name.LocalName == "time") is { } timeElement ? ReadTime(timeElement.Value) : null;
        var elevation = point.Elements().FirstOrDefault(child => child.Name.LocalName == "ele") is { } eleElement ? ReadNumber(eleElement.Value) : null;
        if (latitude is not { } lat || longitude is not { } lon || time is not { } at
            || lat is < -90 or > 90 || lon is < -180 or > 180 || (lat == 0 && lon == 0))
        {
            return null;
        }

        return new TrailPoint(at, lat, lon, elevation);
    }

    private static double? ReadNumber(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;

    /// <summary>GPX times are ISO 8601 in UTC. A time with an offset is moved to UTC, and one with no zone is read as UTC.</summary>
    private static DateTime? ReadTime(string text) =>
        DateTimeOffset.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value)
            ? value.UtcDateTime
            : null;
}
