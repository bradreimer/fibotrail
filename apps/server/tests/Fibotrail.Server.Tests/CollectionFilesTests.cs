using Fibotrail.Server.Collections;

namespace Fibotrail.Server.Tests;

/// <summary>Reading collection files, and refusing a bad one by name. See ADR-003 and docs/specs/collections.md.</summary>
public sealed class CollectionFilesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"fibotrail-collections-{Guid.NewGuid():N}");

    public CollectionFilesTests() => Directory.CreateDirectory(Path.Combine(_directory, "boundaries"));

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private const string Square = """{"type":"Polygon","coordinates":[[[0,0],[0.01,0],[0.01,0.01],[0,0.01],[0,0]]]}""";

    private void Write(string name, string content) => File.WriteAllText(Path.Combine(_directory, name), content);

    private static string Yaml(string places, string id = "test-parks") => $"""
        id: {id}
        name: Test parks
        description: Made up
        rule:
          min_photos: 2
          buffer_m: 30
        places:
        {places}
        """;

    private const string OnePlace = """
          - id: alpha
            name: Alpha
            group: Resort
            status: open
            boundary: boundaries/alpha.geojson
        """;

    [Fact]
    public void A_folder_that_doesnt_exist_holds_no_collections() =>
        Assert.Empty(CollectionFiles.Load(Path.Combine(_directory, "nowhere")).Collections);

    [Fact]
    public void A_collection_loads_with_its_rule_and_its_place_boundaries()
    {
        Write("boundaries/alpha.geojson", Square);
        Write("test.yaml", Yaml(OnePlace));

        var catalog = CollectionFiles.Load(_directory);

        var collection = Assert.Single(catalog.Collections);
        Assert.Equal(("test-parks", 2, 30d), (collection.Id, collection.MinPhotos, collection.BufferMeters));
        var place = Assert.Single(collection.Places);
        Assert.Equal(("alpha", "Resort", false), (place.Park.Id, place.Park.Group, place.Planned));
        Assert.Equal("alpha", catalog.Lookup.Find(0.005, 0.005)?.Id);
    }

    [Fact]
    public void A_feature_collection_and_a_multipolygon_are_read()
    {
        const string ring = "[[[0,0],[0.01,0],[0.01,0.01],[0,0.01],[0,0]]]";
        Write("boundaries/alpha.geojson", "{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"geometry\":{\"type\":\"MultiPolygon\",\"coordinates\":[" + ring + "," + ring + "]}}]}");

        var polygons = CollectionFiles.ReadBoundary(Path.Combine(_directory, "boundaries/alpha.geojson"));

        Assert.Equal(2, polygons.Count);
    }

    [Fact]
    public void A_planned_place_is_listed_but_matches_no_photo()
    {
        Write("boundaries/alpha.geojson", Square);
        Write("test.yaml", Yaml(OnePlace.Replace("status: open", "status: planned", StringComparison.Ordinal)));

        var catalog = CollectionFiles.Load(_directory);

        Assert.True(Assert.Single(Assert.Single(catalog.Collections).Places).Planned);
        Assert.Null(catalog.Lookup.Find(0.005, 0.005));
    }

    [Fact]
    public void A_missing_boundary_file_is_an_error_that_names_the_file()
    {
        Write("test.yaml", Yaml(OnePlace));

        var error = Assert.Throws<CollectionFileException>(() => CollectionFiles.Load(_directory));

        Assert.Contains("test.yaml", error.Message, StringComparison.Ordinal);
        Assert.Contains("boundaries/alpha.geojson", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_polygon_that_isnt_closed_is_an_error_that_names_the_file()
    {
        Write("boundaries/alpha.geojson", """{"type":"Polygon","coordinates":[[[0,0],[0.01,0],[0.01,0.01],[0,0.01]]]}""");
        Write("test.yaml", Yaml(OnePlace));

        var error = Assert.Throws<CollectionFileException>(() => CollectionFiles.Load(_directory));

        Assert.Contains("alpha.geojson", error.Message, StringComparison.Ordinal);
        Assert.Contains("closed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_duplicate_place_id_is_an_error()
    {
        Write("boundaries/alpha.geojson", Square);
        Write("test.yaml", Yaml(OnePlace + "\n" + OnePlace));

        var error = Assert.Throws<CollectionFileException>(() => CollectionFiles.Load(_directory));

        Assert.Contains("\"alpha\"", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_place_id_used_in_two_files_is_an_error()
    {
        Write("boundaries/alpha.geojson", Square);
        Write("one.yaml", Yaml(OnePlace, "one"));
        Write("two.yaml", Yaml(OnePlace, "two"));

        Assert.Throws<CollectionFileException>(() => CollectionFiles.Load(_directory));
    }

    [Fact]
    public void An_unknown_status_is_an_error()
    {
        Write("boundaries/alpha.geojson", Square);
        Write("test.yaml", Yaml(OnePlace.Replace("status: open", "status: maybe", StringComparison.Ordinal)));

        Assert.Throws<CollectionFileException>(() => CollectionFiles.Load(_directory));
    }

    [Fact]
    public void A_file_that_is_not_yaml_is_an_error_that_names_the_file()
    {
        Write("test.yaml", "id: [unclosed");

        var error = Assert.Throws<CollectionFileException>(() => CollectionFiles.Load(_directory));

        Assert.Contains("test.yaml", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_collections_shipped_with_the_app_load_and_find_the_parks_they_list()
    {
        var shipped = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "collections"));
        Assert.True(Directory.Exists(shipped), $"The shipped collections folder wasn't found at {shipped}.");

        var catalog = CollectionFiles.Load(shipped);

        var disney = Assert.Single(catalog.Collections, collection => collection.Id == "disney-parks");
        Assert.Contains(disney.Places, place => place.Park.Id == "magic-kingdom");
        // The middle of Magic Kingdom is in the park, and Orlando's downtown is not.
        Assert.Equal("magic-kingdom", catalog.Lookup.Find(28.4195, -81.5810)?.Id);
        Assert.Null(catalog.Lookup.Find(28.5383, -81.3792));
        // EPCOT is next to Magic Kingdom on the map, and a photo in one is never in the other.
        Assert.Contains(disney.Places, place => place.Park.Id == "epcot");
        Assert.Equal("epcot", catalog.Lookup.Find(28.3700, -81.5490)?.Id);
        Assert.Equal("magic-kingdom", catalog.Lookup.Find(28.4195, -81.5810)?.Id);
        // All four Walt Disney World parks resolve to themselves, and the resort's roads resolve to none.
        Assert.Equal("hollywood-studios", catalog.Lookup.Find(28.3590, -81.5620)?.Id);
        Assert.Equal("animal-kingdom", catalog.Lookup.Find(28.3600, -81.5910)?.Id);
        Assert.Null(catalog.Lookup.Find(28.3830, -81.5200));
        // Disneyland and California Adventure are across a promenade, and each resolves to itself.
        Assert.Equal("disneyland", catalog.Lookup.Find(33.8130, -117.9200)?.Id);
        Assert.Equal("california-adventure", catalog.Lookup.Find(33.8060, -117.9195)?.Id);
        // Tokyo Disneyland and DisneySea share an edge, and each resolves to itself.
        Assert.Equal("tokyo-disneyland", catalog.Lookup.Find(35.6330, 139.8820)?.Id);
        Assert.Equal("tokyo-disneysea", catalog.Lookup.Find(35.6265, 139.8830)?.Id);
        // Each of the two Paris parks resolves to itself.
        Assert.Equal("disneyland-paris", catalog.Lookup.Find(48.8735, 2.7750)?.Id);
        Assert.Equal("disney-adventure-world", catalog.Lookup.Find(48.8650, 2.7760)?.Id);
        Assert.Equal("hong-kong-disneyland", catalog.Lookup.Find(22.3130, 114.0413)?.Id);
        Assert.Equal("shanghai-disneyland", catalog.Lookup.Find(31.1465, 121.6560)?.Id);
        // All 12 Disney parks are in the file.
        Assert.Equal(12, disney.Places.Count);
        // The three Universal Orlando parks sit side by side, and each resolves to itself.
        var universal = Assert.Single(catalog.Collections, collection => collection.Id == "universal-parks");
        Assert.Equal(4, universal.Places.Count);
        Assert.Equal("islands-of-adventure", catalog.Lookup.Find(28.4700, -81.4720)?.Id);
        Assert.Equal("universal-studios-florida", catalog.Lookup.Find(28.4780, -81.4690)?.Id);
        Assert.Equal("epic-universe", catalog.Lookup.Find(28.4470, -81.4440)?.Id);
        Assert.Equal("universal-studios-hollywood", catalog.Lookup.Find(34.1395, -118.3550)?.Id);
    }
}
