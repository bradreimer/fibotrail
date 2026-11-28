using Fibotrail.Detection;

namespace Fibotrail.Trails;

/// <summary>
/// The rules that turn a logger's points into what the trip page shows: segments split at gaps,
/// the summary numbers, a simplified line, and the date check. Pure functions. See
/// docs/specs/gps-trails.md.
/// </summary>
public static class TrailMath
{
    /// <summary>Two points further apart in time than this start a new segment.</summary>
    public static readonly TimeSpan GapTime = TimeSpan.FromMinutes(5);

    /// <summary>Two points further apart than this start a new segment.</summary>
    public const double GapKm = 1;

    /// <summary>A step slower than this counts as standing still, so it adds no moving time.</summary>
    public const double MovingKmPerHour = 1;

    /// <summary>The Douglas–Peucker tolerance for the display line, in meters.</summary>
    public const double DisplayToleranceMeters = 5;

    /// <summary>The most points of one trail that are sent to the browser.</summary>
    public const int MaxDisplayPoints = 5000;

    /// <summary>
    /// How far a trail may start before the trip's first day, or end after its last, and still
    /// overlap. The trip's days are local, and its time zone isn't stored, so the check allows for
    /// the widest offsets from UTC.
    /// </summary>
    public static readonly TimeSpan ZoneAllowance = TimeSpan.FromHours(14);

    /// <summary>
    /// Splits each segment where two consecutive points are more than 5 minutes or 1 km apart. The
    /// map draws each segment as its own line, so no line crosses a gap.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<TrailPoint>> SplitAtGaps(IEnumerable<IReadOnlyList<TrailPoint>> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var result = new List<IReadOnlyList<TrailPoint>>();
        foreach (var segment in segments)
        {
            var current = new List<TrailPoint>();
            foreach (var point in segment)
            {
                if (current.Count > 0 && IsGap(current[^1], point))
                {
                    result.Add(current);
                    current = [];
                }

                current.Add(point);
            }

            if (current.Count > 0)
            {
                result.Add(current);
            }
        }

        return result;
    }

    /// <summary>The numbers of a trail. The segments should already be split at gaps.</summary>
    public static TrailSummary Summarize(IReadOnlyList<IReadOnlyList<TrailPoint>> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var points = segments.SelectMany(segment => segment).ToList();
        if (points.Count == 0)
        {
            throw new ArgumentException("A trail needs at least one point.", nameof(segments));
        }

        var distance = 0.0;
        var moving = TimeSpan.Zero;
        foreach (var segment in segments)
        {
            for (var index = 1; index < segment.Count; index++)
            {
                var step = Geo.DistanceKm(Point(segment[index - 1]), Point(segment[index]));
                distance += step;
                var elapsed = segment[index].Time - segment[index - 1].Time;
                if (elapsed > TimeSpan.Zero && step / elapsed.TotalHours > MovingKmPerHour)
                {
                    moving += elapsed;
                }
            }
        }

        return new TrailSummary(points.Min(point => point.Time), points.Max(point => point.Time), distance, moving, points.Count);
    }

    /// <summary>
    /// The line the map draws: a trail of more than 20,000 points thinned to evenly spaced points
    /// first, then each segment simplified with Douglas–Peucker to about 5 m, and the tolerance doubled until the whole trail has no more than <paramref name="maxPoints"/> points.
    /// A segment always keeps its first and last point.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<TrailPosition>> Simplify(
        IReadOnlyList<IReadOnlyList<TrailPoint>> segments,
        double toleranceMeters = DisplayToleranceMeters,
        int maxPoints = MaxDisplayPoints)
    {
        ArgumentNullException.ThrowIfNull(segments);

        // Douglas–Peucker is slow on a long, jagged track, so a very long trail is first thinned to
        // evenly spaced points. Four times the display limit keeps the shape.
        var total = segments.Sum(segment => segment.Count);
        var stride = Math.Max(1, (int)Math.Ceiling(total / (4.0 * maxPoints)));
        var thinned = stride == 1 ? segments : [.. segments.Select(segment => Thin(segment, stride))];
        var tolerance = toleranceMeters;
        while (true)
        {
            var simplified = thinned.Select(segment => DouglasPeucker(segment, tolerance)).ToList();
            var count = simplified.Sum(segment => segment.Count);

            // Every segment keeps its two ends, so a trail of many short segments can't go below that.
            var floor = segments.Sum(segment => Math.Min(segment.Count, 2));
            if (count <= maxPoints || count <= floor)
            {
                return simplified;
            }

            tolerance *= 2;
        }
    }

    /// <summary>
    /// True when the trail's time span shares a moment with the trip's days, allowing 14 hours
    /// either side for the trip's unknown time zone.
    /// </summary>
    public static bool Overlaps(TrailSummary trail, DateOnly tripStart, DateOnly tripEnd)
    {
        ArgumentNullException.ThrowIfNull(trail);
        var from = tripStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - ZoneAllowance;
        var to = tripEnd.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) + ZoneAllowance;
        return trail.Start < to && trail.End > from;
    }

    /// <summary>Every <paramref name="stride"/>th point, and always the last one.</summary>
    private static List<TrailPoint> Thin(IReadOnlyList<TrailPoint> segment, int stride)
    {
        var kept = segment.Where((_, index) => index % stride == 0).ToList();
        if (segment.Count > 1 && (segment.Count - 1) % stride != 0)
        {
            kept.Add(segment[^1]);
        }

        return kept;
    }

    private static bool IsGap(TrailPoint previous, TrailPoint next) =>
        (next.Time - previous.Time).Duration() > GapTime || Geo.DistanceKm(Point(previous), Point(next)) > GapKm;

    private static GeoPoint Point(TrailPoint point) => new(point.Latitude, point.Longitude);

    /// <summary>Douglas–Peucker on a local flat projection, which is close enough over the length of one segment.</summary>
    private static List<TrailPosition> DouglasPeucker(IReadOnlyList<TrailPoint> segment, double toleranceMeters)
    {
        if (segment.Count <= 2)
        {
            return [.. segment.Select(point => new TrailPosition(point.Latitude, point.Longitude))];
        }

        // Meters east and north of the first point.
        var origin = segment[0];
        var metersPerDegreeLatitude = 111_195.0;
        var metersPerDegreeLongitude = metersPerDegreeLatitude * Math.Cos(double.DegreesToRadians(origin.Latitude));
        var x = segment.Select(point => (point.Longitude - origin.Longitude) * metersPerDegreeLongitude).ToArray();
        var y = segment.Select(point => (point.Latitude - origin.Latitude) * metersPerDegreeLatitude).ToArray();

        var keep = new bool[segment.Count];
        keep[0] = true;
        keep[^1] = true;

        // A stack instead of recursion, so a trail of 100,000 points can't overflow it.
        var stack = new Stack<(int First, int Last)>();
        stack.Push((0, segment.Count - 1));
        while (stack.Count > 0)
        {
            var (first, last) = stack.Pop();
            var farthest = -1;
            var farthestDistance = 0.0;
            for (var index = first + 1; index < last; index++)
            {
                var distance = DistanceToSegment(x[index], y[index], x[first], y[first], x[last], y[last]);
                if (distance > farthestDistance)
                {
                    farthestDistance = distance;
                    farthest = index;
                }
            }

            if (farthest >= 0 && farthestDistance > toleranceMeters)
            {
                keep[farthest] = true;
                stack.Push((first, farthest));
                stack.Push((farthest, last));
            }
        }

        return [.. segment.Where((_, index) => keep[index]).Select(point => new TrailPosition(point.Latitude, point.Longitude))];
    }

    private static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = (dx * dx) + (dy * dy);
        var t = lengthSquared == 0 ? 0 : Math.Clamp((((px - ax) * dx) + ((py - ay) * dy)) / lengthSquared, 0, 1);
        var cx = ax + (t * dx) - px;
        var cy = ay + (t * dy) - py;
        return Math.Sqrt((cx * cx) + (cy * cy));
    }
}
