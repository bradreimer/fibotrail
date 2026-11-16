using System.IO.Compression;
using System.Text.Json;

namespace Fibotrail.Places;

/// <summary>
/// The Natural Earth country outlines that ship with Fibotrail, read from the embedded resource that
/// "npm run outlines" writes from the world-atlas package. See ADR-009 and ADR-011.
/// </summary>
public static class CountryOutlines
{
    private const string ResourceName = "Fibotrail.Places.countries.json.gz";

    /// <summary>Reads the embedded outlines.</summary>
    public static IReadOnlyList<CountryOutline> Read()
    {
        using var resource = typeof(CountryOutlines).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The country outlines ({ResourceName}) are missing from the build.");
        using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        using var document = JsonDocument.Parse(gzip);
        return [.. document.RootElement.GetProperty("countries").EnumerateArray().Select(country => new CountryOutline(
            country.GetProperty("id").ValueKind == JsonValueKind.Null ? null : country.GetProperty("id").GetString(),
            country.GetProperty("name").GetString()!,
            [.. country.GetProperty("polygons").EnumerateArray().Select(polygon =>
                (IReadOnlyList<IReadOnlyList<(double, double)>>)[.. polygon.EnumerateArray().Select(ring =>
                    (IReadOnlyList<(double, double)>)[.. ring.EnumerateArray().Select(position => (position[0].GetDouble(), position[1].GetDouble()))])])]))];
    }

    /// <summary>A finder over the embedded outlines.</summary>
    public static CountryFinder Load() => new(Read());
}
