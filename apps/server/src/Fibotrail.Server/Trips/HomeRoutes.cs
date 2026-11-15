using Microsoft.AspNetCore.Http.HttpResults;
using Fibotrail.Server.Immich;

namespace Fibotrail.Server.Trips;

/// <summary>The position of the home location you chose.</summary>
public sealed record SetHomeRequest(double Latitude, double Longitude);

/// <summary>The saved home location, and what detection then found.</summary>
/// <param name="Trips">Trips found with this home location.</param>
/// <param name="NewSuggestions">Trips that are new in the inbox.</param>
public sealed record SetHomeResponse(PlaceInfo Home, int Trips, int NewSuggestions);

/// <summary>The API routes for the home location.</summary>
public static class HomeRoutes
{
    public const int MaxPlaces = 20;

    public static void MapHomeRoutes(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapGet("/home", async (HomeLocation home, CancellationToken cancellationToken) =>
            TypedResults.Ok(await home.GetStateAsync(cancellationToken)))
            .WithName("GetHome");

        api.MapGet("/home/places", async (string? query, HomeLocation home, CancellationToken cancellationToken) =>
            TypedResults.Ok(await home.SearchPlacesAsync(query, MaxPlaces, cancellationToken)))
            .WithName("SearchHomePlaces");

        api.MapPut("/home", async Task<Results<Ok<SetHomeResponse>, BadRequest<ProblemDetail>>> (
            SetHomeRequest request, HomeLocation home, DetectionService detection, CancellationToken cancellationToken) =>
        {
            if (!(request.Latitude is >= -90 and <= 90) || !(request.Longitude is >= -180 and <= 180))
            {
                return TypedResults.BadRequest(new ProblemDetail("That position isn't on the map. Choose a place, then try again."));
            }

            var saved = await home.SetAsync(request.Latitude, request.Longitude, cancellationToken);

            // A new home changes which photos are away, so every trip is detected again.
            var result = await detection.RunAsync(cancellationToken);
            return TypedResults.Ok(new SetHomeResponse(saved, result.Trips, result.NewSuggestions));
        }).WithName("SetHome");
    }
}
