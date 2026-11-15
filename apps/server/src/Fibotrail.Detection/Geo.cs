using NetTopologySuite.Geometries;

namespace Fibotrail.Detection;

/// <summary>Distance on the globe, and grouping of nearby points.</summary>
public static class Geo
{
    private const double EarthRadiusKm = 6371.0088;
    private const double KmPerDegreeOfLatitude = 111.195;

    /// <summary>
    /// The great-circle distance between two points, in kilometers, by the haversine formula.
    /// NetTopologySuite measures on a flat plane, which is wrong for degrees, so the distance is
    /// computed here.
    /// </summary>
    public static double DistanceKm(GeoPoint from, GeoPoint to)
    {
        var latitude1 = double.DegreesToRadians(from.Latitude);
        var latitude2 = double.DegreesToRadians(to.Latitude);
        var deltaLatitude = latitude2 - latitude1;
        var deltaLongitude = double.DegreesToRadians(to.Longitude - from.Longitude);
        var a = (Math.Sin(deltaLatitude / 2) * Math.Sin(deltaLatitude / 2))
            + (Math.Cos(latitude1) * Math.Cos(latitude2) * Math.Sin(deltaLongitude / 2) * Math.Sin(deltaLongitude / 2));
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>
    /// Groups points that are within a radius of each other. Points are taken in the order given.
    /// Each joins the first group whose first point is within the radius, or starts a group. The
    /// same input always gives the same groups, in the order their first points appeared.
    /// </summary>
    /// <remarks>
    /// A NetTopologySuite spatial index finds the nearby groups, so 70,000 points don't need
    /// 70,000 times 70,000 comparisons.
    /// </remarks>
    public static IReadOnlyList<IReadOnlyList<T>> Group<T>(IEnumerable<T> items, Func<T, GeoPoint> position, double radiusKm)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(position);

        var groups = new List<(GeoPoint Anchor, List<T> Members)>();
        var index = new Quadtree();
        foreach (var item in items)
        {
            var point = position(item);
            var nearest = -1;
            foreach (var candidate in index.Query(SearchBox(point, radiusKm)).Order())
            {
                if (DistanceKm(groups[candidate].Anchor, point) <= radiusKm)
                {
                    nearest = candidate;
                    break;
                }
            }

            if (nearest < 0)
            {
                groups.Add((point, [item]));
                index.Insert(new Envelope(point.Longitude, point.Longitude, point.Latitude, point.Latitude), groups.Count - 1);
            }
            else
            {
                groups[nearest].Members.Add(item);
            }
        }

        return [.. groups.Select(group => (IReadOnlyList<T>)group.Members)];
    }

    /// <summary>The average of some positions. Good enough for points a few kilometers apart.</summary>
    public static GeoPoint Center(IEnumerable<GeoPoint> points)
    {
        var all = points.ToList();
        return new GeoPoint(all.Average(point => point.Latitude), all.Average(point => point.Longitude));
    }

    /// <summary>A box in degrees that holds every point within the radius.</summary>
    private static Envelope SearchBox(GeoPoint center, double radiusKm)
    {
        var latitudeSpan = radiusKm / KmPerDegreeOfLatitude;
        // A degree of longitude shrinks toward the poles. The floor keeps the box finite there.
        var longitudeSpan = latitudeSpan / Math.Max(0.01, Math.Cos(double.DegreesToRadians(center.Latitude)));
        return new Envelope(center.Longitude - longitudeSpan, center.Longitude + longitudeSpan, center.Latitude - latitudeSpan, center.Latitude + latitudeSpan);
    }

    /// <summary>A small wrapper, so the index stores group numbers.</summary>
    private sealed class Quadtree
    {
        private readonly NetTopologySuite.Index.Quadtree.Quadtree<int> _tree = new();

        public void Insert(Envelope envelope, int group) => _tree.Insert(envelope, group);

        public IList<int> Query(Envelope box) => _tree.Query(box);
    }
}
