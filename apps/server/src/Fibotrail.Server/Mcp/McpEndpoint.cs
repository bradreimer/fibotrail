using System.Security.Cryptography;
using System.Text;
using Fibotrail.Mcp;
using Fibotrail.Places;
using Fibotrail.Server.Collections;
using Fibotrail.Server.Data;
using Fibotrail.Server.Stats;

namespace Fibotrail.Server.Mcp;

/// <summary>
/// The MCP server's route, <c>/mcp</c>. It checks the bearer token, then hands the body to
/// <see cref="McpProtocol"/>. It is off, and answers 404, until <c>MCP_TOKEN</c> is set. A web
/// session can't call it and its token can't call <c>/api</c>. See ADR-018 and
/// docs/specs/mcp-server.md.
/// </summary>
public static partial class McpEndpoint
{
    public const string Route = "/mcp";
    public const long MaxBodyBytes = 1_048_576;

    private static readonly string ServerVersion = typeof(McpEndpoint).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>Answers <c>/mcp</c> before the session checks, so the cookie and anti-forgery rules don't apply to it.</summary>
    public static void UseMcp(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Map(Route, branch => branch.Run(HandleAsync));
    }

    private static async Task HandleAsync(HttpContext context)
    {
        var services = context.RequestServices;
        var settings = services.GetRequiredService<ServerSettings>();
        var response = context.Response;

        if (settings.McpToken is not { } token)
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!TokenMatches(context.Request.Headers.Authorization.ToString(), token))
        {
            response.StatusCode = StatusCodes.Status401Unauthorized;
            response.Headers.WWWAuthenticate = "Bearer";
            return;
        }

        if (!HttpMethods.IsPost(context.Request.Method))
        {
            response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            response.Headers.Allow = "POST";
            return;
        }

        // A page in a browser has an Origin. It must be this server's own, which stops another
        // site from reaching /mcp through a browser. A client with no Origin is let through.
        if (context.Request.Headers.Origin.ToString() is { Length: > 0 } origin && !IsOwnOrigin(origin, context.Request.Host))
        {
            response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var limiter = services.GetRequiredService<McpRateLimiter>();
        if (!limiter.TryAcquire(out var retryAfter))
        {
            response.StatusCode = StatusCodes.Status429TooManyRequests;
            response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return;
        }

        if (context.Request.ContentLength > MaxBodyBytes)
        {
            response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        var body = await ReadBodyAsync(context.Request, context.RequestAborted);
        if (body is null)
        {
            response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Fibotrail.Mcp");
        var time = services.GetRequiredService<TimeProvider>();
        var started = time.GetTimestamp();

        var reply = await McpProtocol.HandleAsync(body, ServerVersion, cancellationToken => LoadAsync(services, cancellationToken), context.RequestAborted);

        if (reply.Tool is { } tool)
        {
            var milliseconds = (long)time.GetElapsedTime(started).TotalMilliseconds;
            LogCall(logger, tool, reply.ToolFailed ? "error" : "ok", milliseconds);
            if (reply.ToolFailed)
            {
                LogFailedArguments(logger, tool, string.Join(", ", reply.ArgumentNames ?? []));
            }
        }

        response.StatusCode = reply.Status;
        if (reply.Body is null) return;
        response.ContentType = "application/json";
        await response.WriteAsync(reply.Body, context.RequestAborted);
    }

    /// <summary>
    /// Compares the whole token in constant time. Both sides are hashed first, so the comparison
    /// takes the same time for a wrong token of any length.
    /// </summary>
    internal static bool TokenMatches(string header, string token)
    {
        const string Prefix = "Bearer ";
        var offered = header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ? header[Prefix.Length..].Trim() : string.Empty;
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(offered)), SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            && offered.Length > 0;
    }

    private static bool IsOwnOrigin(string origin, HostString host) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && string.Equals(uri.Authority, host.Value, StringComparison.OrdinalIgnoreCase);

    private static async Task<string?> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var builder = new StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            builder.Append(buffer, 0, read);
            if (builder.Length > MaxBodyBytes) return null;
        }

        return builder.ToString();
    }

    /// <summary>Builds what the tools read, from the same code the Stats and Collections screens use.</summary>
    public static async Task<McpData> LoadAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        var catalog = services.GetRequiredService<CollectionCatalog>();
        var library = await StatsRoutes.LoadAsync(
            services.GetRequiredService<FibotrailDbContext>(),
            services.GetRequiredService<CountryFinder>(),
            services.GetRequiredService<CountryOutlinesCache>(),
            catalog,
            cancellationToken);
        return new McpData(
            library,
            [.. catalog.Collections.Select(collection => new McpCatalogEntry(
                collection.Id,
                collection.Name,
                [.. collection.Places.Select(place => new McpPlace(place.Park.Id, place.Park.Name, place.Park.Group, place.Planned))]))]);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "MCP tool {Tool}: {Status} in {Milliseconds} ms.")]
    private static partial void LogCall(ILogger logger, string tool, string status, long milliseconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "MCP tool {Tool} failed. Argument names: {Names}.")]
    private static partial void LogFailedArguments(ILogger logger, string tool, string names);
}

/// <summary>
/// At most 60 authenticated requests a minute, in fixed windows. A model can call several tools
/// to answer one question, so the limit is generous. See open question 2 of the MCP spec.
/// </summary>
public sealed class McpRateLimiter(TimeProvider time)
{
    public const int RequestsPerMinute = 60;

    private readonly Lock _gate = new();
    private DateTimeOffset _windowStart = DateTimeOffset.MinValue;
    private int _count;

    public bool TryAcquire(out int retryAfterSeconds)
    {
        lock (_gate)
        {
            var now = time.GetUtcNow();
            if (now - _windowStart >= TimeSpan.FromMinutes(1))
            {
                _windowStart = now;
                _count = 0;
            }

            if (_count >= RequestsPerMinute)
            {
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((_windowStart.AddMinutes(1) - now).TotalSeconds));
                return false;
            }

            _count++;
            retryAfterSeconds = 0;
            return true;
        }
    }
}

/// <summary>Whether the MCP server is on, and the tools it offers. The token is never part of it.</summary>
public sealed record McpStatus(bool Enabled, IReadOnlyList<string> Tools);

/// <summary>The route the MCP screen reads.</summary>
public static class McpStatusRoutes
{
    public static void MapMcpRoutes(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);
        api.MapGet("/mcp/status", (ServerSettings settings) =>
            TypedResults.Ok(new McpStatus(settings.McpEnabled, [.. McpTools.All.Select(tool => tool.Name)])))
            .WithName("GetMcpStatus");
    }
}
