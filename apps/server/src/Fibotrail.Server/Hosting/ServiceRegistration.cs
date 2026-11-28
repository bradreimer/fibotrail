using System.Text.Json;
using System.Text.Json.Serialization;
using Fibotrail.Immich;
using Fibotrail.Places;
using Fibotrail.Server.Auth;
using Fibotrail.Server.Collections;
using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
using Fibotrail.Server.Stats;
using Fibotrail.Server.Mcp;
using Fibotrail.Server.Trails;
using Fibotrail.Server.Trips;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fibotrail.Server.Hosting;

/// <summary>
/// Registers the services. The commands use the core and Immich sets only. The web server adds
/// the feature, web, and auth sets on top.
/// </summary>
public static class ServiceRegistration
{
    private static readonly string[] PrivateNetworks = ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "fc00::/7"];

    /// <summary>Every service the web server needs.</summary>
    public static void AddFibotrailServer(this IServiceCollection services, ServerSettings settings, Argon2Cost? cost = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        services.AddFibotrailCore(settings, cost);

        // A test registers its own Immich options and a stand-in transport before this runs.
        services.AddFibotrailImmich(settings);
        services.AddFibotrailFeatures();
        services.AddFibotrailWeb();
        services.AddFibotrailAuth(settings);
    }

    /// <summary>The settings, the database, and sign-in. Every command uses this set.</summary>
    public static void AddFibotrailCore(this IServiceCollection services, ServerSettings settings, Argon2Cost? cost = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        services.TryAddSingleton(settings);
        services.TryAddSingleton<McpRateLimiter>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new PasswordHasher(cost ?? Argon2Cost.Default));
        services.TryAddSingleton<ISignInDelay, SignInDelay>();
        // A test registers interceptors to stop detection at a set point. The server registers none.
        services.AddDbContext<FibotrailDbContext>((provider, options) => options
            .UseSqlite(settings.ConnectionString)
            .AddInterceptors(provider.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>()));
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

    /// <summary>The services behind the routes: the connection check, thumbnails, decisions, outlines, and collections.</summary>
    private static void AddFibotrailFeatures(this IServiceCollection services)
    {
        services.AddScoped<ImmichConnection>();
        services.AddScoped<ThumbnailStore>();
        services.AddScoped<TripDecisions>();
        services.AddScoped<OnTheWayPhotos>();
        services.AddScoped<TrailStore>();
        services.AddSingleton<TrailFileCache>();

        // The country outlines are read from an embedded resource the first time the map asks.
        services.AddSingleton(_ => CountryOutlines.Load());
        services.AddSingleton(_ => RegionOutlines.Load());
        services.AddSingleton<CountryOutlinesCache>();

        // The collection files are checked at startup, so a bad file stops the app with its name.
        services.TryAddSingleton(_ => CollectionFiles.Load(CollectionFiles.DefaultDirectory));
    }

    /// <summary>JSON, compression, the OpenAPI document, and the reverse proxy headers.</summary>
    private static void AddFibotrailWeb(this IServiceCollection services)
    {
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
    }

    /// <summary>The data protection keys, the anti-forgery cookie, the session scheme, and the rule that every route needs a session.</summary>
    private static void AddFibotrailAuth(this IServiceCollection services, ServerSettings settings)
    {
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

        services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, null);

        // Every route needs a session unless it says otherwise.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
    }
}
