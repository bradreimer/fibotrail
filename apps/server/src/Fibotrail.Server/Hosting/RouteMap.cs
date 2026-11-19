using Fibotrail.Server.Auth;
using Fibotrail.Server.Collections;
using Fibotrail.Server.Games;
using Fibotrail.Server.Immich;
using Fibotrail.Server.Insights;
using Fibotrail.Server.Map;
using Fibotrail.Server.Mcp;
using Fibotrail.Server.Trips;
using Fibotrail.Server.Web;

namespace Fibotrail.Server.Hosting;

/// <summary>Every route. Each feature maps its own routes from its own folder.</summary>
public static class RouteMap
{
    public static void MapFibotrailRoutes(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthRoutes(app.Configuration);
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

        // Last, so every address that no route above claims goes to the web app.
        app.MapWebAppFallback();
    }
}
