using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Operation.Distance;

namespace Fibotrail.Places;

/// <summary>One country's outline: polygons, each a list of rings with the outer ring first.</summary>
/// <param name="Id">The ISO 3166-1 numeric code, as the web app's map features carry it. Null for a few territories.</param>
/// <param name="Polygons">Rings of (longitude, latitude) positions.</param>
public sealed record CountryOutline(string? Id, string Name, IReadOnlyList<IReadOnlyList<IReadOnlyList<(double Longitude, double Latitude)>>> Polygons);

/// <summary>A country, as the finder returns it.</summary>
public sealed record Country(string? Id, string Name)
{
    /// <summary>The key that tells two countries apart: the code, or the name when there's no code.</summary>
    public string Key => Id ?? Name;
}

/// <summary>A position's country, and how far the position is from its outline.</summary>
/// <param name="DistanceKm">0 when the outline contains the position, otherwise the distance to its edge.</param>
public sealed record CountryMatch(Country Country, double DistanceKm);

/// <summary>
/// Finds the country of a position. The outlines contain it, or, for a position just off a coast,
/// the nearest country within a distance counts. A pure function of the outlines it's given, so it
/// is tested with made-up squares and no data file. See docs/specs/life-map.md and ADR-011.
/// </summary>
public sealed class CountryFinder
{
    /// <summary>How far outside every outline a position may be and still count for the nearest country.</summary>
    public const double DefaultNearestKm = 25;

    private const double KmPerDegree = 111.195;
    private static readonly GeometryFactory Factory = new();

    private readonly List<Entry> _entries;

    public CountryFinder(IEnumerable<CountryOutline> outlines)
    {
        ArgumentNullException.ThrowIfNull(outlines);
        _entries = [.. outlines.Select(outline =>
        {
            var polygons = outline.Polygons.Select(rings =>
            {
                var shell = Factory.CreateLinearRing(Coordinates(rings[0]));
                var holes = rings.Skip(1).Select(ring => Factory.CreateLinearRing(Coordinates(ring))).ToArray();
                return Factory.CreatePolygon(shell, holes);
            }).ToArray();
            var geometry = Factory.CreateMultiPolygon(polygons);
            return new Entry(new Country(outline.Id, outline.Name), geometry, PreparedGeometryFactory.Prepare(geometry));
        })];
    }

    /// <summary>The number of countries, for a check against the web app's copy of the outlines.</summary>
    public int Count => _entries.Count;

    /// <summary>The countries, by name.</summary>
    public IEnumerable<Country> Countries => _entries.Select(entry => entry.Country);

    /// <summary>
    /// The country that contains the position. A position that no outline contains joins the nearest
    /// country within <paramref name="nearestKm"/>. Returns null when there is none.
    /// </summary>
    public CountryMatch? Find(double latitude, double longitude, double nearestKm = DefaultNearestKm)
    {
        var point = Factory.CreatePoint(new Coordinate(longitude, latitude));

        foreach (var entry in _entries)
        {
            if (entry.Geometry.EnvelopeInternal.Contains(point.Coordinate) && entry.Prepared.Intersects(point))
            {
                return new CountryMatch(entry.Country, 0);
            }
        }

        // Outside every outline, such as on a coast at 1:50 million detail. Look for the nearest edge.
        var latitudeMargin = nearestKm / KmPerDegree;
        var longitudeMargin = latitudeMargin / Math.Max(0.05, Math.Cos(latitude * Math.PI / 180));
        CountryMatch? best = null;
        foreach (var entry in _entries)
        {
            var envelope = entry.Geometry.EnvelopeInternal;
            if (longitude < envelope.MinX - longitudeMargin || longitude > envelope.MaxX + longitudeMargin
                || latitude < envelope.MinY - latitudeMargin || latitude > envelope.MaxY + latitudeMargin)
            {
                continue;
            }

            var nearest = DistanceOp.NearestPoints(entry.Geometry, point)[0];
            var km = DistanceKm(latitude, longitude, nearest.Y, nearest.X);
            if (km <= nearestKm && (best is null || km < best.DistanceKm))
            {
                best = new CountryMatch(entry.Country, km);
            }
        }

        return best;
    }

    private static Coordinate[] Coordinates(IReadOnlyList<(double Longitude, double Latitude)> ring) =>
        [.. ring.Select(position => new Coordinate(position.Longitude, position.Latitude))];

    /// <summary>The great-circle distance between two positions, in kilometers.</summary>
    public static double DistanceKm(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
    {
        const double EarthRadiusKm = 6371.0088;
        var phiA = latitudeA * Math.PI / 180;
        var phiB = latitudeB * Math.PI / 180;
        var deltaPhi = phiB - phiA;
        var deltaLambda = (longitudeB - longitudeA) * Math.PI / 180;
        var a = (Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2))
            + (Math.Cos(phiA) * Math.Cos(phiB) * Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2));
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private sealed record Entry(Country Country, MultiPolygon Geometry, IPreparedGeometry Prepared);
}
