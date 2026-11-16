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
    }
}
