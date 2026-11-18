using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fibotrail.Mcp.Tests;

public class ProtocolTests
{
    private static readonly McpData Data = Fixture.Data();

    private static Task<McpReply> SendAsync(string body, Func<CancellationToken, Task<McpData>>? load = null) =>
        McpProtocol.HandleAsync(body, "1.2.3", load ?? (_ => Task.FromResult(Data)), CancellationToken.None);

    private static JsonNode Json(McpReply reply) => JsonNode.Parse(reply.Body!)!;

    [Fact]
    public async Task Initialize_answers_with_the_requested_version_and_the_tools_capability()
    {
        var reply = await SendAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""");

        Assert.Equal(200, reply.Status);
        var result = Json(reply)["result"]!;
        Assert.Equal("2025-03-26", (string?)result["protocolVersion"]);
        Assert.NotNull(result["capabilities"]!["tools"]);
        Assert.Equal("fibotrail", (string?)result["serverInfo"]!["name"]);
        Assert.Equal("1.2.3", (string?)result["serverInfo"]!["version"]);
        Assert.Equal(1, (int?)Json(reply)["id"]);
    }

    [Fact]
    public async Task Initialize_offers_the_latest_version_when_the_client_asks_for_an_unknown_one()
    {
        var reply = await SendAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"1999-01-01"}}""");

        Assert.Equal(McpProtocol.LatestVersion, (string?)Json(reply)["result"]!["protocolVersion"]);
    }

    [Fact]
    public async Task A_notification_gets_202_and_no_body()
    {
        var reply = await SendAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        Assert.Equal(202, reply.Status);
        Assert.Null(reply.Body);
    }

    [Fact]
    public async Task A_response_from_the_client_gets_202_and_no_body()
    {
        var reply = await SendAsync("""{"jsonrpc":"2.0","id":7,"result":{}}""");

        Assert.Equal(202, reply.Status);
        Assert.Null(reply.Body);
    }

    [Fact]
    public async Task Ping_answers_with_an_empty_result_and_a_string_id_comes_back_as_a_string()
    {
        var reply = await SendAsync("""{"jsonrpc":"2.0","id":"abc","method":"ping"}""");

        Assert.Equal("abc", (string?)Json(reply)["id"]);
        Assert.Empty(Json(reply)["result"]!.AsObject());
    }

    [Fact]
    public async Task The_tool_list_has_every_tool_with_a_description_and_a_schema()
    {
        var reply = await SendAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        var tools = Json(reply)["result"]!["tools"]!.AsArray();
        Assert.Equal(McpTools.All.Count, tools.Count);
        Assert.Equal(7, tools.Count);
        Assert.All(tools, tool =>
        {
            Assert.False(string.IsNullOrWhiteSpace((string?)tool!["name"]));
            Assert.False(string.IsNullOrWhiteSpace((string?)tool["description"]));
            Assert.Equal("object", (string?)tool["inputSchema"]!["type"]);
        });
    }

    [Fact]
    public async Task A_tool_call_returns_text_and_structured_content()
    {
        var reply = await SendAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"collection_visits","arguments":{"collection":"disney-parks"}}}""");

        var result = Json(reply)["result"]!;
        Assert.False((bool)result["isError"]!);
        Assert.Equal(3, (int?)result["structuredContent"]!["trips"]);
        Assert.Equal(14, (int?)result["structuredContent"]!["days"]);
        var text = (string?)result["content"]![0]!["text"];
        Assert.Equal(14, (int?)JsonNode.Parse(text!)!["days"]);
        Assert.Equal("collection_visits", reply.Tool);
        Assert.False(reply.ToolFailed);
    }

    [Fact]
    public async Task A_bad_input_is_a_tool_error_with_a_message_and_only_the_argument_names()
    {
        var reply = await SendAsync("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"collection_visits","arguments":{"collection":"water-parks"}}}""");

        var result = Json(reply)["result"]!;
        Assert.True((bool)result["isError"]!);
        Assert.Contains("disney-parks", (string?)result["content"]![0]!["text"], StringComparison.Ordinal);
        Assert.True(reply.ToolFailed);
        Assert.Equal(["collection"], reply.ArgumentNames);
    }

    [Fact]
    public async Task An_unknown_tool_is_an_invalid_params_error()
    {
        var reply = await SendAsync("""{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"run_sql","arguments":{}}}""");

        Assert.Equal(-32602, (int?)Json(reply)["error"]!["code"]);
    }

    [Fact]
    public async Task An_unknown_method_is_a_method_not_found_error()
    {
        var reply = await SendAsync("""{"jsonrpc":"2.0","id":5,"method":"resources/list"}""");

        Assert.Equal(-32601, (int?)Json(reply)["error"]!["code"]);
    }

    [Fact]
    public async Task Text_that_isnt_json_is_a_400_parse_error()
    {
        var reply = await SendAsync("not json");

        Assert.Equal(400, reply.Status);
        Assert.Equal(-32700, (int?)Json(reply)["error"]!["code"]);
    }

    [Fact]
    public async Task A_batch_is_refused()
    {
        var reply = await SendAsync("""[{"jsonrpc":"2.0","id":1,"method":"ping"}]""");

        Assert.Equal(400, reply.Status);
        Assert.Equal(-32600, (int?)Json(reply)["error"]!["code"]);
    }

    [Fact]
    public async Task Data_is_loaded_only_for_a_tool_call()
    {
        var loads = 0;
        Task<McpData> Load(CancellationToken _)
        {
            loads++;
            return Task.FromResult(Data);
        }

        await SendAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""", Load);
        await SendAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""", Load);
        Assert.Equal(0, loads);

        await SendAsync("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"list_collections"}}""", Load);
        Assert.Equal(1, loads);
    }

    [Fact]
    public void The_serialized_result_uses_camel_case_names_and_leaves_out_nulls()
    {
        var text = McpProtocol.Serialize(Fixture.Run<CollectionVisits>("collection_visits", Fixture.Data([]), """{"collection":"disney-parks"}"""));

        using var document = JsonDocument.Parse(text);
        Assert.True(document.RootElement.TryGetProperty("parksVisited", out _));
        Assert.False(document.RootElement.TryGetProperty("firstVisit", out _));
    }
}
