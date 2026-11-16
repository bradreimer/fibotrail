using System.Text.Json;
using Fibotrail.Detection;
using Fibotrail.Detection.Parks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Fibotrail.Server.Collections;

/// <summary>A place in a collection. A planned place shows as "Coming" and matches no photo.</summary>
public sealed record CollectionPlace(ParkPlace Park, bool Planned);

/// <summary>A collection, read from one YAML file in <c>collections/</c>. See ADR-003 and docs/specs/collections.md.</summary>
public sealed record CollectionDefinition(string Id, string Name, string Description, int MinPhotos, double BufferMeters, IReadOnlyList<CollectionPlace> Places);

/// <summary>A collection file is wrong. The message names the file.</summary>
public sealed class CollectionFileException(string message) : Exception(message);

/// <summary>Every collection, and a lookup over the places that are open.</summary>
public sealed class CollectionCatalog(IReadOnlyList<CollectionDefinition> collections)
{
    public IReadOnlyList<CollectionDefinition> Collections { get; } = collections;

    /// <summary>The open places of every collection. Planned places aren't matched.</summary>
    public ParkLookup Lookup { get; } = new(collections.SelectMany(collection => collection.Places).Where(place => !place.Planned).Select(place => place.Park));

    public static CollectionCatalog Empty { get; } = new([]);
}

/// <summary>
/// Reads the collection files. A missing boundary, a polygon that isn't closed, a duplicate id, or
/// a bad value is an error that names the file, and the app refuses to start. A file is never
/// skipped silently.
/// </summary>
public static class CollectionFiles
{
    public const int DefaultMinPhotos = 3;
    public const double DefaultBufferMeters = 50;

    /// <summary>The folder next to the server, where the image keeps the collections.</summary>
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "collections");

    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>Reads every <c>*.yaml</c> file of a folder. A folder that doesn't exist holds no collections.</summary>
    public static CollectionCatalog Load(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (!Directory.Exists(directory)) return CollectionCatalog.Empty;

        var collections = new List<CollectionDefinition>();
        var collectionIds = new HashSet<string>(StringComparer.Ordinal);
        var placeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(directory, "*.yaml").Order(StringComparer.Ordinal))
        {
            var collection = LoadFile(file);
            if (!collectionIds.Add(collection.Id)) throw new CollectionFileException($"{file}: the collection id \"{collection.Id}\" is used by another file.");
            foreach (var place in collection.Places)
            {
                if (!placeIds.Add(place.Park.Id)) throw new CollectionFileException($"{file}: the place id \"{place.Park.Id}\" is used more than once.");
            }

            collections.Add(collection);
        }

        return new CollectionCatalog(collections);
    }

    private static CollectionDefinition LoadFile(string file)
    {
        RawCollection raw;
        try
        {
            raw = Yaml.Deserialize<RawCollection>(File.ReadAllText(file)) ?? throw new CollectionFileException($"{file}: the file is empty.");
        }
        catch (YamlDotNet.Core.YamlException exception)
        {
            throw new CollectionFileException($"{file}: {exception.Message}");
        }

        string Required(string? value, string field) =>
            string.IsNullOrWhiteSpace(value) ? throw new CollectionFileException($"{file}: \"{field}\" is missing.") : value;

        var id = Required(raw.Id, "id");
        var minPhotos = raw.Rule?.MinPhotos ?? DefaultMinPhotos;
        var buffer = raw.Rule?.BufferM ?? DefaultBufferMeters;
        if (minPhotos < 1) throw new CollectionFileException($"{file}: rule.min_photos must be at least 1.");
        if (buffer < 0) throw new CollectionFileException($"{file}: rule.buffer_m can't be negative.");

        var places = new List<CollectionPlace>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rawPlace in raw.Places ?? [])
        {
            var placeId = Required(rawPlace.Id, "places[].id");
            if (!seen.Add(placeId)) throw new CollectionFileException($"{file}: the place id \"{placeId}\" is used more than once.");
            var status = rawPlace.Status ?? "open";
            if (status is not ("open" or "planned")) throw new CollectionFileException($"{file}: place \"{placeId}\" has status \"{status}\". Use open or planned.");

            var boundaryName = Required(rawPlace.Boundary, $"places[{placeId}].boundary");
            var boundaryFile = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, boundaryName));
            if (!File.Exists(boundaryFile)) throw new CollectionFileException($"{file}: the boundary file {boundaryName} for \"{placeId}\" is missing.");

            places.Add(new CollectionPlace(
                new ParkPlace(placeId, Required(rawPlace.Name, $"places[{placeId}].name"), rawPlace.Group ?? string.Empty, minPhotos, rawPlace.BufferM ?? buffer, ReadBoundary(boundaryFile)),
                status == "planned"));
        }

        return new CollectionDefinition(id, Required(raw.Name, "name"), raw.Description ?? string.Empty, minPhotos, buffer, places);
    }

    /// <summary>Reads the polygons of a GeoJSON file: a Polygon, a MultiPolygon, or a Feature or FeatureCollection of them.</summary>
    public static IReadOnlyList<IReadOnlyList<IReadOnlyList<GeoPoint>>> ReadBoundary(string file)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(file));
        }
        catch (JsonException exception)
        {
            throw new CollectionFileException($"{file}: not valid JSON. {exception.Message}");
        }

        using (document)
        {
            var polygons = new List<IReadOnlyList<IReadOnlyList<GeoPoint>>>();
            Collect(document.RootElement, polygons, file);
            if (polygons.Count == 0) throw new CollectionFileException($"{file}: no polygon found.");
            return polygons;
        }
    }

    private static void Collect(JsonElement element, List<IReadOnlyList<IReadOnlyList<GeoPoint>>> polygons, string file)
    {
        var type = element.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
        switch (type)
        {
            case "FeatureCollection":
                foreach (var feature in element.GetProperty("features").EnumerateArray()) Collect(feature, polygons, file);
                break;
            case "Feature":
                Collect(element.GetProperty("geometry"), polygons, file);
                break;
            case "Polygon":
                polygons.Add(ReadPolygon(element.GetProperty("coordinates"), file));
                break;
            case "MultiPolygon":
                foreach (var polygon in element.GetProperty("coordinates").EnumerateArray()) polygons.Add(ReadPolygon(polygon, file));
                break;
            default:
                throw new CollectionFileException($"{file}: \"{type}\" isn't a Polygon or MultiPolygon.");
        }
    }

    private static IReadOnlyList<IReadOnlyList<GeoPoint>> ReadPolygon(JsonElement rings, string file) =>
        [.. rings.EnumerateArray().Select(ring =>
        {
            // GeoJSON positions are longitude first.
            var points = ring.EnumerateArray().Select(position => new GeoPoint(position[1].GetDouble(), position[0].GetDouble())).ToList();
            if (points.Count < 4) throw new CollectionFileException($"{file}: a polygon ring needs at least 4 positions.");
            if (points[0] != points[^1]) throw new CollectionFileException($"{file}: a polygon ring isn't closed: its first and last positions differ.");
            return (IReadOnlyList<GeoPoint>)points;
        })];

    private sealed class RawCollection
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public RawRule? Rule { get; set; }

        public List<RawPlace>? Places { get; set; }
    }

    private sealed class RawRule
    {
        public int? MinPhotos { get; set; }

        public double? BufferM { get; set; }
    }

    private sealed class RawPlace
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Group { get; set; }

        public string? Status { get; set; }

        public string? Boundary { get; set; }

        public double? BufferM { get; set; }
    }
}
