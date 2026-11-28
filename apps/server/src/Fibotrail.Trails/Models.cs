namespace Fibotrail.Trails;

/// <summary>One point a GPS logger recorded.</summary>
/// <param name="Time">When the point was recorded, in UTC.</param>
/// <param name="Elevation">Meters above sea level, when the file has it.</param>
public sealed record TrailPoint(DateTime Time, double Latitude, double Longitude, double? Elevation = null);

/// <summary>A position on the map, with no time.</summary>
public sealed record TrailPosition(double Latitude, double Longitude);

/// <summary>What a GPX file holds: its points, in the segments the file gives them.</summary>
public sealed record GpxTrail(IReadOnlyList<IReadOnlyList<TrailPoint>> Segments)
{
    public int PointCount => Segments.Sum(segment => segment.Count);
}

/// <summary>A parsed file, or the plain reason it can't be used.</summary>
public sealed record GpxParseResult(GpxTrail? Trail, string? Problem)
{
    public static GpxParseResult Refused(string problem) => new(null, problem);
}

/// <summary>The numbers of one trail.</summary>
/// <param name="Start">The first point's time, in UTC.</param>
/// <param name="End">The last point's time, in UTC.</param>
/// <param name="DistanceKm">The sum of the great-circle steps inside each segment. The step across a gap doesn't count.</param>
/// <param name="MovingTime">The time between consecutive points where the speed is above 1 km/h.</param>
/// <param name="PointCount">The number of usable points.</param>
public sealed record TrailSummary(DateTime Start, DateTime End, double DistanceKm, TimeSpan MovingTime, int PointCount);
