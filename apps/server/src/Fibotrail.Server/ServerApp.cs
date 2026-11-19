using Fibotrail.Server.Collections;
using Fibotrail.Server.Games;
using Fibotrail.Server.Hosting;
using Fibotrail.Server.Map;
using Fibotrail.Server.Mcp;
using Fibotrail.Server.Insights;
using Fibotrail.Server.Stats;
using Fibotrail.Server.Auth;
using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
using Fibotrail.Server.Trips;
using Fibotrail.Server.Web;

namespace Fibotrail.Server;

/// <summary>Builds the web server: services, the request pipeline, and the routes.</summary>
public static partial class ServerApp
{
    /// <summary>The routes that need no session. Static files are also public.</summary>
    public static readonly IReadOnlyList<string> PublicRoutes = ["/healthz", "/setup", "/sign-in"];

    public static WebApplication Create(WebApplicationBuilder builder, ServerSettings settings, Argon2Cost? cost = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddFibotrailServer(settings, cost);

        var app = builder.Build();

        app.UseForwardedHeaders();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        // The MCP server answers before the session and anti-forgery checks. It has its own token.
        app.UseMcp();
        // API answers aren't compressed. They're small, and a compressed answer that holds a secret
        // next to text an attacker chose can leak the secret through its length.
        app.UseWhen(
            context => !context.Request.Path.StartsWithSegments("/api"),
            branch => branch.UseResponseCompression());
        app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = WebApp.SetCacheHeaders });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<AntiforgeryMiddleware>();

        MapRoutes(app);
        return app;
    }

    public static async Task PrepareDatabaseAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FibotrailDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ServerSettings>();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var backup = await DatabaseStartup.PrepareAsync(db, settings, time);
        if (backup is not null)
        {
            LogBackup(app.Logger, backup);
        }
    }

    /// <summary>
    /// Checks the Immich connection and writes the result to the log, then continues a scan that
    /// the last shutdown interrupted. The server starts either way: Settings shows the problem.
    /// </summary>
    public static async Task StartImmichAsync(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        await using var scope = app.Services.CreateAsyncScope();
        var status = await scope.ServiceProvider.GetRequiredService<ImmichConnection>().CheckAsync(CancellationToken.None);
        if (status.Connected)
        {
            LogConnected(app.Logger, status.Version, status.Warning ?? "The version is one that Fibotrail was tested with.");
        }
        else
        {
            LogNotConnected(app.Logger, status.Message);
        }

        if (await app.Services.GetRequiredService<ScanService>().ResumeInterruptedAsync(CancellationToken.None))
        {
            LogResumed(app.Logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to Immich {Version}. {Note}")]
    private static partial void LogConnected(ILogger logger, string? version, string note);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message}")]
    private static partial void LogNotConnected(ILogger logger, string? message);

    /// <summary>Writes whether the MCP server is on. The token is never part of the line.</summary>
    public static void LogMcpState(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.Services.GetRequiredService<ServerSettings>().McpEnabled)
        {
            LogMcpOn(app.Logger);
        }
        else
        {
            LogMcpOff(app.Logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "MCP is on at /mcp.")]
    private static partial void LogMcpOn(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "MCP is off. Set MCP_TOKEN to turn it on.")]
    private static partial void LogMcpOff(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Continuing the scan that was interrupted.")]
    private static partial void LogResumed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved a copy of the database at {Path} before updating it.")]
    private static partial void LogBackup(ILogger logger, string path);

    private static void MapRoutes(WebApplication app)
    {
        // The release image sets FIBOTRAIL_VERSION. A build from source reports "dev".
        var version = app.Configuration["FIBOTRAIL_VERSION"] is { Length: > 0 } configured ? configured : "dev";

        app.MapGet("/healthz", async (FibotrailDbContext db, CancellationToken cancellationToken) =>
            await db.Database.CanConnectAsync(cancellationToken)
                ? Results.Text($"ok {version}")
                : Results.Text("The database didn't open.", statusCode: StatusCodes.Status503ServiceUnavailable))
            .AllowAnonymous();

        app.MapAuthRoutes();

        var api = app.MapGroup("/api").WithTags("Fibotrail");

        api.MapSessionRoute();
        api.MapImmichRoutes();
        api.MapHomeRoutes();
        api.MapInboxRoutes();
        api.MapTripRoutes();
        api.MapMapRoutes();
        api.MapInsightsRoutes();
        api.MapCollectionRoutes();
        api.MapMcpRoutes();
        api.MapGameRoutes();

        app.MapOpenApi();

        // Every other page address belongs to the web app, which does its own routing.
        app.MapFallback("{*path:nonfile}", (HttpContext context, IWebHostEnvironment environment) =>
            HttpMethods.IsGet(context.Request.Method) && !context.Request.Path.StartsWithSegments("/api")
                ? WebApp.Shell(environment)
                : Results.NotFound());
    }
}
