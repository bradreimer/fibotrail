using System.Globalization;
using System.Text.Json;

namespace Fibotrail.Mcp;

/// <summary>A tool was called with an input it can't use. The message goes back to the model, so it holds no private data.</summary>
public sealed class ToolInputException : Exception
{
    public ToolInputException()
    {
    }

    public ToolInputException(string message)
        : base(message)
    {
    }

    public ToolInputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The arguments of one tool call.</summary>
public sealed class ToolArgs(JsonElement arguments)
{
    /// <summary>The names of the arguments that were passed. Only names are ever logged.</summary>
    public IReadOnlyList<string> Names => arguments.ValueKind == JsonValueKind.Object
        ? [.. arguments.EnumerateObject().Select(property => property.Name)]
        : [];

    public string? Text(string name)
    {
        if (!Find(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw new ToolInputException($"'{name}' must be text.");
        var text = value.GetString()!.Trim();
        return text.Length == 0 ? null : text;
    }

    public string RequiredText(string name) =>
        Text(name) ?? throw new ToolInputException($"'{name}' is required.");

    public int? WholeNumber(string name)
    {
        if (!Find(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
        {
            throw new ToolInputException($"'{name}' must be a whole number.");
        }

        return number;
    }

    public DateOnly? Date(string name)
    {
        if (Text(name) is not { } text) return null;
        return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new ToolInputException($"'{name}' must be a date such as 2024-07-31.");
    }

    private bool Find(string name, out JsonElement value)
    {
        value = default;
        return arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out value);
    }
}

/// <summary>One tool: its name, the description the model reads, its input, and what it returns.</summary>
/// <param name="InputSchema">A JSON Schema object, as text.</param>
/// <param name="ResultType">The record the tool returns. A test walks it for the vocabulary.</param>
public sealed record McpTool(string Name, string Description, string InputSchema, Type ResultType, Func<McpData, ToolArgs, object> Run);
