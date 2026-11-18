using System.Text.Json;

namespace Fibotrail.Mcp.Tests;

/// <summary>
/// The example questions on the MCP screen are in <c>apps/web/src/mcp/examples.json</c>. Each one
/// lists the tool calls a model would make. This test runs those calls against a made-up library,
/// so an example can't promise a tool that is missing or an input a tool refuses.
/// </summary>
public class ExamplesTests
{
    private static string ExamplesPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "apps", "web", "src", "mcp", "examples.json");
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException("apps/web/src/mcp/examples.json wasn't found above the test folder.");
    }

    private static JsonElement Load() => JsonDocument.Parse(File.ReadAllText(ExamplesPath())).RootElement.Clone();

    public static TheoryData<string> ExampleIds() => [.. Load().GetProperty("examples").EnumerateArray().Select(example => example.GetProperty("id").GetString()!)];

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Every_call_of_an_example_runs_without_an_error(string id)
    {
        var example = Load().GetProperty("examples").EnumerateArray().Single(item => item.GetProperty("id").GetString() == id);
        var data = Fixture.Data();

        var calls = example.GetProperty("calls").EnumerateArray().ToList();
        Assert.NotEmpty(calls);
        foreach (var call in calls)
        {
            var tool = McpTools.Find(call.GetProperty("tool").GetString()!);
            Assert.NotNull(tool);
            var result = tool.Run(data, new ToolArgs(call.GetProperty("arguments")));
            Assert.NotNull(result);
        }

        Assert.False(string.IsNullOrWhiteSpace(example.GetProperty("question").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(example.GetProperty("answer").GetString()));
    }

    [Fact]
    public void Every_tool_has_at_least_one_example()
    {
        var used = Load().GetProperty("examples").EnumerateArray()
            .SelectMany(example => example.GetProperty("calls").EnumerateArray())
            .Select(call => call.GetProperty("tool").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var tool in McpTools.All)
        {
            Assert.Contains(tool.Name, used);
        }
    }

    [Fact]
    public void The_disney_question_is_the_first_example_and_uses_collection_visits()
    {
        var first = Load().GetProperty("examples")[0];

        Assert.Equal("How many times have I been to a Disney park?", first.GetProperty("question").GetString());
        Assert.Equal("collection_visits", first.GetProperty("calls")[0].GetProperty("tool").GetString());
    }

    [Fact]
    public void The_screen_also_says_what_it_cannot_answer()
    {
        var cannot = Load().GetProperty("cannot").EnumerateArray().ToList();

        Assert.NotEmpty(cannot);
        Assert.All(cannot, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("question").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("why").GetString()));
        });
    }
}
