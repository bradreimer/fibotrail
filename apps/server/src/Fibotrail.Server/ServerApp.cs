using Fibotrail.Server.Auth;
using Fibotrail.Server.Hosting;

namespace Fibotrail.Server;

/// <summary>
/// The composition root of the web server. It registers the services, adds the middleware, and
/// maps the routes. Each step lives in Hosting/, and each feature owns its own routes.
/// </summary>
public static class ServerApp
{
    /// <summary>The routes that need no session. Static files are also public.</summary>
    public static readonly IReadOnlyList<string> PublicRoutes = ["/healthz", "/setup", "/sign-in"];

    public static WebApplication Create(WebApplicationBuilder builder, ServerSettings settings, Argon2Cost? cost = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddFibotrailServer(settings, cost);

        var app = builder.Build();
        app.UseFibotrailPipeline();
        app.MapFibotrailRoutes();
        return app;
    }
}
