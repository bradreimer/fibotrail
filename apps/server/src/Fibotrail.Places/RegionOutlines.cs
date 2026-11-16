using System.IO.Compression;
using System.Text.Json;

namespace Fibotrail.Places;

/// <summary>A country that also tracks its regions: Canada and the USA to start. See ADR-012.</summary>
/// <param name="Id">The ISO 3166-1 numeric code, the same <see cref="Country.Id"/> that the country outlines carry.</param>
public sealed record DetailCountry(string Id, string Name);

/// <summary>A region outline, tied to the detail country that holds it.</summary>
/// <param name="CountryId">The ISO 3166-1 numeric code of the detail country.</param>
public sealed record RegionOutline(string CountryId, CountryOutline Outline);

/// <summary>
/// The Natural Earth states and provinces that ship with Fibotrail, read from the embedded resource
/// that "npm run outlines" writes. See ADR-012.
/// </summary>
public static class RegionOutlines
{
    private const string ResourceName = "Fibotrail.Places.regions.json.gz";

    /// <summary>Reads the embedded detail countries and their region outlines.</summary>
    public static (IReadOnlyList<DetailCountry> Countries, IReadOnlyList<RegionOutline> Regions) Read()
    {
        using var resource = typeof(RegionOutlines).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The region outlines ({ResourceName}) are missing from the build.");
        using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        using var document = JsonDocument.Parse(gzip);
        var root = document.RootElement;
        var countries = root.GetProperty("countries").EnumerateArray()
            .Select(country => new DetailCountry(country.GetProperty("id").GetString()!, country.GetProperty("name").GetString()!))
            .ToList();
        var regions = root.GetProperty("regions").EnumerateArray().Select(region => new RegionOutline(
            region.GetProperty("country").GetString()!,
            new CountryOutline(
                region.GetProperty("id").GetString(),
                region.GetProperty("name").GetString()!,
                [.. region.GetProperty("polygons").EnumerateArray().Select(polygon =>
                    (IReadOnlyList<IReadOnlyList<(double, double)>>)[.. polygon.EnumerateArray().Select(ring =>
                        (IReadOnlyList<(double, double)>)[.. ring.EnumerateArray().Select(position => (position[0].GetDouble(), position[1].GetDouble()))])])])))
            .ToList();
        return (countries, regions);
    }

    /// <summary>A finder over the embedded region outlines.</summary>
    public static RegionFinder Load()
    {
        var (countries, regions) = Read();
        return new RegionFinder(countries, regions);
    }
}
