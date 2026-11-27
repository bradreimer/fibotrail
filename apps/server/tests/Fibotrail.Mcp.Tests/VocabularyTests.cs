using System.Reflection;
using System.Text.Json;

namespace Fibotrail.Mcp.Tests;

/// <summary>
/// The guard of ADR-018. Every field of every tool result has a declared kind from the allowed
/// vocabulary, so a coordinate, a photo, or a trip name can't reach a result by accident.
/// </summary>
public class VocabularyTests
{
    // Words that mark data the vocabulary leaves out. A field name with one of them fails the test.
    private static readonly string[] Forbidden =
    [
        "latitude", "longitude", "coordinate", "position", "route", "stop", "photo", "thumbnail",
        "file", "camera", "immich", "url", "key", "token", "user", "city", "address", "note text",
    ];

    // The one field allowed to carry a city, and so to hold the word, per ADR-023.
    private static readonly (Type Owner, string Property) MainCity = (typeof(TripEntry), nameof(TripEntry.MainCity));

    private static IEnumerable<(Type Owner, PropertyInfo Property)> Walk(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.DeclaringType != type) continue;
            yield return (type, property);
            if (property.GetCustomAttribute<NestedAttribute>() is not null)
            {
                var element = property.PropertyType.IsGenericType ? property.PropertyType.GetGenericArguments()[0] : property.PropertyType;
                foreach (var inner in Walk(element)) yield return inner;
            }
        }
    }

    public static TheoryData<string> ToolNames() => new(McpTools.All.Select(tool => tool.Name));

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void Every_field_of_a_result_has_a_kind_or_holds_another_result(string name)
    {
        var tool = McpTools.Find(name)!;

        var fields = Walk(tool.ResultType).ToList();

        Assert.NotEmpty(fields);
        foreach (var (owner, property) in fields)
        {
            var kind = property.GetCustomAttribute<CoarseAttribute>();
            var nested = property.GetCustomAttribute<NestedAttribute>();
            Assert.True(kind is not null || nested is not null, $"{owner.Name}.{property.Name} has no [Coarse] kind and isn't [Nested].");
            Assert.False(kind is not null && nested is not null, $"{owner.Name}.{property.Name} is both [Coarse] and [Nested].");
        }
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void No_field_is_named_for_data_the_vocabulary_leaves_out(string name)
    {
        foreach (var (owner, property) in Walk(McpTools.Find(name)!.ResultType))
        {
            var lowered = property.Name.ToUpperInvariant();
            foreach (var word in Forbidden)
            {
                if (word == "city" && (owner, property.Name) == MainCity) continue;
                Assert.False(lowered.Contains(word.ToUpperInvariant(), StringComparison.Ordinal), $"{owner.Name}.{property.Name} is named for '{word}'.");
            }
        }
    }

    [Fact]
    public void A_field_that_is_a_name_names_a_country_a_continent_or_a_place()
    {
        // The only fields called Name carry one of the three name kinds. A trip has no Name at all.
        foreach (var tool in McpTools.All)
        {
            foreach (var (owner, property) in Walk(tool.ResultType).Where(item => item.Property.Name == "Name"))
            {
                var kind = property.GetCustomAttribute<CoarseAttribute>()!.Kind;
                Assert.True(kind is Coarse.Country or Coarse.Continent or Coarse.Place, $"{owner.Name}.Name has the kind {kind}.");
            }
        }

        Assert.DoesNotContain(typeof(TripEntry).GetProperties(), property => property.Name == "Name");
    }

    [Fact]
    public void A_city_is_only_ever_the_main_city_of_a_trip()
    {
        var cities = McpTools.All
            .SelectMany(tool => Walk(tool.ResultType))
            .Where(item => item.Property.GetCustomAttribute<CoarseAttribute>()?.Kind == Coarse.City)
            .Select(item => (item.Owner, item.Property.Name))
            .Distinct()
            .ToList();

        Assert.Equal([MainCity], cities);
        Assert.Equal(typeof(string), typeof(TripEntry).GetProperty(nameof(TripEntry.MainCity))!.PropertyType);
    }

    [Fact]
    public void No_city_but_a_main_city_reaches_any_tool()
    {
        // Trip 3 stops in Kissimmee and Anaheim, and has the main city Orlando.
        var data = Fixture.Data();
        foreach (var tool in McpTools.All)
        {
            var json = tool.Name switch
            {
                "collection_visits" => """{"collection":"disney-parks"}""",
                "country_summary" => """{"country":"United States of America"}""",
                "get_trip" => """{"id":3}""",
                "days_away" => """{"by":"month"}""",
                _ => "{}",
            };
            using var document = JsonDocument.Parse(json);
            var text = McpProtocol.Serialize(tool.Run(data, new ToolArgs(document.RootElement.Clone())));

            Assert.DoesNotContain("Kissimmee", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Anaheim", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_distance_field_is_a_number_of_kilometers_and_a_date_field_is_a_date()
    {
        foreach (var tool in McpTools.All)
        {
            foreach (var (owner, property) in Walk(tool.ResultType))
            {
                var kind = property.GetCustomAttribute<CoarseAttribute>()?.Kind;
                var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (kind == Coarse.DistanceKm) Assert.Equal(typeof(double), type);
                if (kind == Coarse.Date) Assert.Equal(typeof(DateOnly), type);
                if (kind is Coarse.Count or Coarse.TripId) Assert.Equal(typeof(int), type);
                if (kind == Coarse.Flag) Assert.Equal(typeof(bool), type);
                _ = owner;
            }
        }
    }

    [Fact]
    public void A_trip_with_a_name_and_a_position_leaks_neither_through_any_tool()
    {
        // Trip 3 is named "Mom's 70th" and has photos at known positions in Florida and California.
        var data = Fixture.Data();
        var calls = new (string Tool, string Json)[]
        {
            ("list_collections", "{}"),
            ("collection_visits", """{"collection":"disney-parks"}"""),
            ("list_countries", "{}"),
            ("country_summary", """{"country":"United States of America"}"""),
            ("list_trips", "{}"),
            ("get_trip", """{"id":3}"""),
            ("days_away", """{"by":"month"}"""),
        };

        foreach (var (tool, json) in calls)
        {
            using var document = JsonDocument.Parse(json);
            var text = McpProtocol.Serialize(McpTools.Find(tool)!.Run(data, new ToolArgs(document.RootElement.Clone())));

            Assert.DoesNotContain("Mom", text, StringComparison.Ordinal);
            Assert.DoesNotContain("70th", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Spring break", text, StringComparison.Ordinal);
            Assert.DoesNotContain("28.4", text, StringComparison.Ordinal);
            Assert.DoesNotContain("-81.5", text, StringComparison.Ordinal);
            Assert.DoesNotContain("33.8", text, StringComparison.Ordinal);
            Assert.DoesNotContain("-117.9", text, StringComparison.Ordinal);
        }
    }
}
