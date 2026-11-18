using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Fibotrail.Mcp;

/// <summary>The answer to one HTTP request to <c>/mcp</c>.</summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Body">The JSON body, or null when there is none (a notification gets 202).</param>
/// <param name="Tool">The tool that was called, for the log.</param>
/// <param name="ToolFailed">True when the tool returned an error.</param>
/// <param name="ArgumentNames">The names of the arguments, set only for a failed call. Never their values.</param>
public sealed record McpReply(int Status, string? Body, string? Tool = null, bool ToolFailed = false, IReadOnlyList<string>? ArgumentNames = null);

/// <summary>
/// The Model Context Protocol, as much of it as a read-only server with tools needs: JSON-RPC 2.0
/// over the streamable HTTP transport, answering each POST with one JSON body. It holds no state
/// and no session. Pure: the caller passes a loader for the data, so it is tested with plain text.
/// See ADR-018.
/// </summary>
public static class McpProtocol
{
    public const string LatestVersion = "2025-06-18";

    private static readonly string[] SupportedVersions = [LatestVersion, "2025-03-26", "2024-11-05"];

    private const string Instructions =
        "Fibotrail answers questions about the trips in a photo library: trips, countries, and collection places such as Disney parks. " +
        "Results hold counts, dates, and country, continent, collection, and park names only. They never hold photos, coordinates, or trip names. " +
        "Start with list_collections or list_countries to see what exists, then ask for the one you need.";

    private static readonly JsonSerializerOptions ResultOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The JSON text of a tool result, as the model reads it.</summary>
    public static string Serialize(object result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Serialize(result, result.GetType(), ResultOptions);
    }

    /// <summary>Answers one request body.</summary>
    /// <param name="loadData">Builds the data a tool reads. It runs only for a tool call.</param>
    public static async Task<McpReply> HandleAsync(string body, string serverVersion, Func<CancellationToken, Task<McpData>> loadData, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(loadData);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return Error(400, null, -32700, "The request isn't valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Error(400, null, -32600, "Send one JSON-RPC request at a time. Batches aren't supported.");
            }

            var hasId = root.TryGetProperty("id", out var id) && id.ValueKind is JsonValueKind.String or JsonValueKind.Number;
            var method = root.TryGetProperty("method", out var methodElement) && methodElement.ValueKind == JsonValueKind.String ? methodElement.GetString() : null;

            // A response from the client, or a notification, gets no answer body.
            if (method is null)
            {
                return root.TryGetProperty("result", out _) || root.TryGetProperty("error", out _)
                    ? new McpReply(202, null)
                    : Error(400, hasId ? id : null, -32600, "The request has no method.");
            }

            if (!hasId) return new McpReply(202, null);

            root.TryGetProperty("params", out var parameters);
            switch (method)
            {
                case "initialize":
                    return Result(id, Initialize(parameters, serverVersion));
                case "ping":
                    return Result(id, new JsonObject());
                case "tools/list":
                    return Result(id, ToolList());
                case "tools/call":
                    return await CallAsync(id, parameters, loadData, cancellationToken);
                default:
                    return Error(200, id, -32601, $"The method '{method}' isn't supported. This server offers tools only.");
            }
        }
    }

    private static JsonObject Initialize(JsonElement parameters, string serverVersion)
    {
        var requested = parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("protocolVersion", out var version) && version.ValueKind == JsonValueKind.String
            ? version.GetString()
            : null;
        return new JsonObject
        {
            ["protocolVersion"] = requested is not null && SupportedVersions.Contains(requested, StringComparer.Ordinal) ? requested : LatestVersion,
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
            ["serverInfo"] = new JsonObject { ["name"] = "fibotrail", ["version"] = serverVersion },
            ["instructions"] = Instructions,
        };
    }

    private static JsonObject ToolList() => new()
    {
        ["tools"] = new JsonArray([.. McpTools.All.Select(tool => (JsonNode)new JsonObject
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["inputSchema"] = JsonNode.Parse(tool.InputSchema),
        })]),
    };

    private static async Task<McpReply> CallAsync(JsonElement id, JsonElement parameters, Func<CancellationToken, Task<McpData>> loadData, CancellationToken cancellationToken)
    {
        var name = parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString()
            : null;
        if (name is null) return Error(200, id, -32602, "A tool call needs a tool name.");
        if (McpTools.Find(name) is not { } tool) return Error(200, id, -32602, $"There is no tool '{name}'.");

        var arguments = parameters.TryGetProperty("arguments", out var argumentsElement) ? argumentsElement : default;
        var args = new ToolArgs(arguments);
        var data = await loadData(cancellationToken);

        object? result = null;
        string? failure = null;
        try
        {
            result = tool.Run(data, args);
        }
        catch (ToolInputException exception)
        {
            failure = exception.Message;
        }

        var content = new JsonObject();
        if (failure is null)
        {
            content["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = Serialize(result!) });
            content["structuredContent"] = JsonNode.Parse(Serialize(result!));
            content["isError"] = false;
        }
        else
        {
            content["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = failure });
            content["isError"] = true;
        }

        var reply = Result(id, content);
        return reply with { Tool = tool.Name, ToolFailed = failure is not null, ArgumentNames = failure is null ? null : args.Names };
    }

    private static McpReply Result(JsonElement id, JsonNode result) =>
        new(200, new JsonObject { ["jsonrpc"] = "2.0", ["id"] = JsonNode.Parse(id.GetRawText()), ["result"] = result }.ToJsonString());

    private static McpReply Error(int status, JsonElement? id, int code, string message) =>
        new(status, new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id is { } element ? JsonNode.Parse(element.GetRawText()) : null,
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
        }.ToJsonString());

    private static McpReply Error(int status, JsonElement id, int code, string message) => Error(status, (JsonElement?)id, code, message);
}
