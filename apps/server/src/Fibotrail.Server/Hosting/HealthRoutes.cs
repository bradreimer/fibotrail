using Fibotrail.Server.Data;

namespace Fibotrail.Server.Hosting;

/// <summary><c>/healthz</c>, which the container health check calls. It needs no session.</summary>
public static class HealthRoutes
{
    public const string Route = "/healthz";

    public static void MapHealthRoutes(this IEndpointRouteBuilder app, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configuration);

        // The release image sets FIBOTRAIL_VERSION. A build from source reports "dev".
        var version = configuration["FIBOTRAIL_VERSION"] is { Length: > 0 } configured ? configured : "dev";

        app.MapGet(Route, async (FibotrailDbContext db, CancellationToken cancellationToken) =>
            await db.Database.CanConnectAsync(cancellationToken)
                ? Results.Text($"ok {version}")
                : Results.Text("The database didn't open.", statusCode: StatusCodes.Status503ServiceUnavailable))
            .AllowAnonymous();
    }
}
