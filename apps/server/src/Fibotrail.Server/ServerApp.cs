using Fibotrail.Places;
using Fibotrail.Server.Collections;
using Fibotrail.Server.Map;
using Fibotrail.Server.Mcp;
using Fibotrail.Server.Stats;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fibotrail.Server.Auth;
using Fibotrail.Immich;
using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
using Fibotrail.Server.Trips;
using Fibotrail.Server.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fibotrail.Server;

/// <summary>Builds the web server: services, the request pipeline, and the routes.</summary>
public static partial class ServerApp
{
    /// <summary>The routes that need no session. Static files are also public.</summary>
    public static readonly IReadOnlyList<string> PublicRoutes = ["/healthz", "/setup", "/sign-in"];

    private static readonly string[] PrivateNetworks = ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "fc00::/7"];

    public static void AddFibotrailCore(this IServiceCollection services, ServerSettings settings, Argon2Cost? cost = null)
    {
        services.TryAddSingleton(settings);
        services.TryAddSingleton<McpRateLimiter>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new PasswordHasher(cost ?? Argon2Cost.Default));
        services.TryAddSingleton<ISignInDelay, SignInDelay>();
        services.AddDbContext<FibotrailDbContext>(options => options.UseSqlite(settings.ConnectionString));
        services.AddScoped<AuthService>();
        services.AddScoped<SignInThrottle>();
        services.AddScoped<HomeLocation>();
    }

    /// <summary>The Immich client, the scanner, and trip detection after a scan. The rescan command uses the same set.</summary>
    public static void AddFibotrailImmich(this IServiceCollection services, ServerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        services.TryAddSingleton(new ImmichOptions(settings.ImmichUrl!, settings.ImmichApiKey!, settings.ImmichTimeout));
        services.TryAddSingleton<IImmichClient>(provider =>
            new ImmichClient(provider.GetRequiredService<ImmichOptions>(), provider.GetService<ImmichTransport>()?.Handler));
        services.TryAddSingleton<ScanService>();
        services.AddScoped<DetectionService>();
        services.AddScoped<IScanCompletedHandler>(provider => provider.GetRequiredService<DetectionService>());
    }

    public static WebApplication Create(WebApplicationBuilder builder, ServerSettings settings, Argon2Cost? cost = null)
    {
        var services = builder.Services;
        services.AddFibotrailCore(settings, cost);

        // Immich. A test registers its own options and a stand-in transport before this runs.
        services.AddFibotrailImmich(settings);
        services.AddScoped<ImmichConnection>();
        services.AddScoped<ThumbnailStore>();
        services.AddScoped<TripDecisions>();
        services.AddScoped<OnTheWayPhotos>();

        // The country outlines are read from an embedded resource the first time the map asks.
        services.AddSingleton(_ => CountryOutlines.Load());
        services.AddSingleton(_ => RegionOutlines.Load());
        services.AddSingleton<CountryOutlinesCache>();

        // The collection files are checked at startup, so a bad file stops the app with its name.
        services.TryAddSingleton(_ => CollectionFiles.Load(CollectionFiles.DefaultDirectory));

        // Enums travel as words, such as "running", so the web app's types name each value.
        // Numbers travel as numbers only, so the types say "number" and not "number or string".
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });

        // The files of the web app are sent compressed. The map outlines shrink from 756 KB to about 200 KB.
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes = [.. ResponseCompressionDefaults.MimeTypes, "image/svg+xml", "application/manifest+json"];
        });

        // The keys that protect the anti-forgery cookie live in /data, the one writable folder.
        services.AddDataProtection()
            .SetApplicationName("Fibotrail")
            .PersistKeysToFileSystem(new DirectoryInfo(settings.KeysDirectory));

        services.AddAntiforgery(options =>
        {
            options.FormFieldName = "csrf";
            options.HeaderName = "X-CSRF-Token";
            options.Cookie.Name = "fibotrail_csrf";
            options.Cookie.HttpOnly = true;

            // The session cookie is always Secure. This one follows the request, so that over plain
            // HTTP the sign-in screen still loads and can say that sign-in needs HTTPS.
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        // The OpenAPI document is the source of the TypeScript types in apps/web.
        // It lists the JSON routes under /api only. The HTML screens aren't part of the API.
        services.AddOpenApi(options =>
        {
            options.ShouldInclude = description => description.RelativePath?.StartsWith("api/", StringComparison.Ordinal) == true;
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new() { Title = "Fibotrail API", Version = "v1" };
                document.Servers = [];
                return Task.CompletedTask;
            });
        });

        services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, null);

        // Every route needs a session unless it says otherwise.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        // The reverse proxy tells the server the browser's address and whether it used HTTPS.
        // Only a proxy on the same machine or on a private network is believed.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var network in PrivateNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        var app = builder.Build();

        app.UseForwardedHeaders();
        app.Use(SecurityHeaders);
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
        app.Use(RejectForgedRequests);

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

    private static Task SecurityHeaders(HttpContext context, RequestDelegate next)
    {
        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'; form-action 'self'; base-uri 'none'";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        return next(context);
    }

    /// <summary>Every request that changes state must carry a valid anti-forgery token.</summary>
    private static async Task RejectForgedRequests(HttpContext context, RequestDelegate next)
    {
        var method = context.Request.Method;
        var readsOnly = HttpMethods.IsGet(method) || HttpMethods.IsHead(method)
            || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
        if (!readsOnly && context.GetEndpoint() is not null)
        {
            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    await context.Response.WriteAsJsonAsync(new ProblemDetail("This page is out of date. Reload it, then try again."));
                }
                else
                {
                    await WriteHtmlAsync(context.Response, Pages.Expired());
                }

                return;
            }
        }

        await next(context);
    }

    private static void MapRoutes(WebApplication app)
    {
        // The release image sets FIBOTRAIL_VERSION. A build from source reports "dev".
        var version = app.Configuration["FIBOTRAIL_VERSION"] is { Length: > 0 } configured ? configured : "dev";

        app.MapGet("/healthz", async (FibotrailDbContext db, CancellationToken cancellationToken) =>
            await db.Database.CanConnectAsync(cancellationToken)
                ? Results.Text($"ok {version}")
                : Results.Text("The database didn't open.", statusCode: StatusCodes.Status503ServiceUnavailable))
            .AllowAnonymous();

        app.MapGet("/setup", async (HttpContext context, AuthService auth, IAntiforgery antiforgery) =>
            await auth.IsPasswordSetAsync(context.RequestAborted)
                ? Results.Redirect("/sign-in")
                : Html(Pages.Setup(CsrfField(context, antiforgery), null, IsInsecure(context.Request))))
            .AllowAnonymous();

        app.MapPost("/setup", async (HttpContext context, AuthService auth, IAntiforgery antiforgery) =>
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var change = await auth.SetUpAsync(form["password"], form["confirmation"], context.RequestAborted);
            switch (change.Outcome)
            {
                case SetupOutcome.AlreadySetUp:
                    // The setup route stops working once a password is stored.
                    return Results.NotFound();
                case SetupOutcome.Rejected:
                    return Html(
                        Pages.Setup(CsrfField(context, antiforgery), change.Problem, IsInsecure(context.Request)),
                        StatusCodes.Status400BadRequest);
                default:
                    SessionCookie.Write(context.Response, await auth.CreateSessionAsync(context.RequestAborted));
                    return Results.Redirect("/");
            }
        }).AllowAnonymous();

        app.MapGet("/sign-in", async (HttpContext context, AuthService auth, IAntiforgery antiforgery) =>
            await auth.IsPasswordSetAsync(context.RequestAborted)
                ? Html(Pages.SignIn(CsrfField(context, antiforgery), null, IsInsecure(context.Request)))
                : Results.Redirect("/setup"))
            .AllowAnonymous();

        app.MapPost("/sign-in", async (HttpContext context, AuthService auth, SignInThrottle throttle, IAntiforgery antiforgery) =>
        {
            if (!await auth.IsPasswordSetAsync(context.RequestAborted))
            {
                return Results.Redirect("/setup");
            }

            var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await throttle.WaitIfNeededAsync(address, context.RequestAborted);

            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            if (!await auth.VerifyPasswordAsync(form["password"], context.RequestAborted))
            {
                await throttle.RecordFailureAsync(address, context.RequestAborted);
                return Html(
                    Pages.SignIn(CsrfField(context, antiforgery), Pages.WrongPassword, IsInsecure(context.Request)),
                    StatusCodes.Status401Unauthorized);
            }

            await throttle.ClearAsync(address, context.RequestAborted);
            SessionCookie.Write(context.Response, await auth.CreateSessionAsync(context.RequestAborted));
            return Results.Redirect("/");
        }).AllowAnonymous();

        app.MapPost("/sign-out", async (HttpContext context, AuthService auth) =>
        {
            await auth.EndSessionAsync(context.Request.Cookies[SessionCookie.Name], context.RequestAborted);
            SessionCookie.Clear(context.Response);
            return Results.Redirect("/sign-in");
        });

        var api = app.MapGroup("/api").WithTags("Fibotrail");

        api.MapGet("/session", (HttpContext context, IAntiforgery antiforgery) =>
            TypedResults.Ok(new SessionInfo(antiforgery.GetAndStoreTokens(context).RequestToken ?? string.Empty)))
            .WithName("GetSession");

        api.MapImmichRoutes();
        api.MapHomeRoutes();
        api.MapInboxRoutes();
        api.MapTripRoutes();
        api.MapMapRoutes();
        api.MapStatsRoutes();
        api.MapCollectionRoutes();
        api.MapMcpRoutes();

        app.MapOpenApi();

        // Every other page address belongs to the web app, which does its own routing.
        app.MapFallback("{*path:nonfile}", (HttpContext context, IWebHostEnvironment environment) =>
            HttpMethods.IsGet(context.Request.Method) && !context.Request.Path.StartsWithSegments("/api")
                ? WebApp.Shell(environment)
                : Results.NotFound());
    }

    private static string CsrfField(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        var value = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(tokens.RequestToken ?? string.Empty);
        return $"<input type=\"hidden\" name=\"{tokens.FormFieldName}\" value=\"{value}\">";
    }

    /// <summary>
    /// True when the browser won't keep the Secure session cookie: the request isn't HTTPS and
    /// isn't to this machine. The screen then says so.
    /// </summary>
    private static bool IsInsecure(HttpRequest request) =>
        !request.IsHttps
        && !string.Equals(request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase)
        && !(IPAddress.TryParse(request.Host.Host, out var address) && IPAddress.IsLoopback(address));

    private static IResult Html(string html, int statusCode = StatusCodes.Status200OK) =>
        Results.Content(html, "text/html; charset=utf-8", statusCode: statusCode);

    private static Task WriteHtmlAsync(HttpResponse response, string html)
    {
        response.ContentType = "text/html; charset=utf-8";
        return response.WriteAsync(html);
    }
}
