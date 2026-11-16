using NetTopologySuite.Geometries;

namespace Fibotrail.Detection.Parks;

/// <summary>A place in a collection, such as one theme park.</summary>
/// <param name="Group">The resort the place belongs to, such as "Walt Disney World Resort".</param>
/// <param name="MinPhotos">How many photos of one trip must be in the place for the trip to visit it.</param>
/// <param name="BufferMeters">How far outside the boundary a photo may be and still count.</param>
/// <param name="Polygons">Polygons of rings of positions. The first ring of each polygon is its outer edge, the rest are holes.</param>
public sealed record ParkPlace(
    string Id,
    string Name,
    string Group,
    int MinPhotos,
    double BufferMeters,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<GeoPoint>>> Polygons);

/// <summary>
/// Finds the place a position is in: inside its boundary, or within the buffer of it. When two
/// places match, the nearer boundary wins, so two neighboring parks never share a photo. Pure, so
/// it is tested with made-up squares. See docs/specs/collections.md and ADR-013.
/// </summary>
public sealed class ParkLookup
{
    private const double MetersPerDegreeLatitude = 110_574;
    private const double MetersPerDegreeLongitudeAtEquator = 111_320;

    private readonly List<Entry> _entries;

    public ParkLookup(IEnumerable<ParkPlace> places)
    {
        ArgumentNullException.ThrowIfNull(places);
        _entries = [.. places.Select(place =>
        {
            var points = place.Polygons.SelectMany(polygon => polygon.SelectMany(ring => ring)).ToList();
            return new Entry(
                place,
                points.Min(point => point.Latitude),
                points.Max(point => point.Latitude),
                points.Min(point => point.Longitude),
                points.Max(point => point.Longitude));
        })];
    }

    /// <summary>The place a position is in, or null.</summary>
    public ParkPlace? Find(double latitude, double longitude)
    {
        ParkPlace? best = null;
        var bestDistance = double.MaxValue;
        foreach (var entry in _entries)
        {
            // A cheap box test first, widened by the buffer, so most photos cost one comparison.
            var latitudeMargin = entry.Place.BufferMeters / MetersPerDegreeLatitude;
            var longitudeMargin = entry.Place.BufferMeters / (MetersPerDegreeLongitudeAtEquator * Math.Max(Math.Cos(double.DegreesToRadians(latitude)), 0.01));
            if (latitude < entry.MinLatitude - latitudeMargin || latitude > entry.MaxLatitude + latitudeMargin
                || longitude < entry.MinLongitude - longitudeMargin || longitude > entry.MaxLongitude + longitudeMargin)
            {
                continue;
            }

            var distance = DistanceMeters(entry.Place, latitude, longitude);
            if (distance > entry.Place.BufferMeters) continue;
            if (distance < bestDistance || (distance == bestDistance && best is not null && string.CompareOrdinal(entry.Place.Id, best.Id) < 0))
            {
                best = entry.Place;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>0 when the boundary contains the position, otherwise the distance to its edge in meters.</summary>
    private static double DistanceMeters(ParkPlace place, double latitude, double longitude)
    {
        // Measure on a flat map centered on the position: meters east and north of it.
        var east = MetersPerDegreeLongitudeAtEquator * Math.Cos(double.DegreesToRadians(latitude));
        var factory = new GeometryFactory();
        var origin = new Point(0, 0);
        var best = double.MaxValue;
        foreach (var polygon in place.Polygons)
        {
            LinearRing ToRing(IReadOnlyList<GeoPoint> ring) => factory.CreateLinearRing([.. ring.Select(point =>
                new Coordinate((point.Longitude - longitude) * east, (point.Latitude - latitude) * MetersPerDegreeLatitude))]);
            var shape = factory.CreatePolygon(ToRing(polygon[0]), [.. polygon.Skip(1).Select(ToRing)]);
            best = Math.Min(best, shape.Distance(origin));
        }

        return best;
    }

    private sealed record Entry(ParkPlace Place, double MinLatitude, double MaxLatitude, double MinLongitude, double MaxLongitude);
}
