using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Trips;

/// <summary>A trip in your library, as its card shows it.</summary>
/// <param name="Days">Calendar days from the first day to the last, counting both.</param>
/// <param name="Place">The main destination: the city with the most photos, or the state or country.</param>
public sealed record TripSummary(int Id, string Name, DateOnly StartDate, DateOnly EndDate, int Days, string? Place, string CoverPhotoId);

/// <summary>A new name, a new cover, or both. A value left out stays as it is.</summary>
public sealed record TripChange(string? Name = null, string? CoverPhotoId = null);

/// <summary>The API routes for the trips you added.</summary>
public static class TripRoutes
{
    public static void MapTripRoutes(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapGet("/trips", async (FibotrailDbContext db, CancellationToken cancellationToken) =>
            TypedResults.Ok<IReadOnlyList<TripSummary>>(await db.Trips.AsNoTracking()
                .Where(trip => trip.Status == TripStatus.Added)
                .OrderByDescending(trip => trip.StartDate)
                .ThenByDescending(trip => trip.Id)
                .Select(trip => new TripSummary(trip.Id, trip.Name, trip.StartDate, trip.EndDate, trip.Days, trip.MainPlace, trip.CoverPhotoId))
                .ToListAsync(cancellationToken)))
            .WithName("GetTrips");

        api.MapGet("/trips/{id:int}", async Task<Results<Ok<TripDetail>, NotFound>> (int id, FibotrailDbContext db, ServerSettings settings, CancellationToken cancellationToken) =>
            await TripPage.LoadAsync(db, settings, id, cancellationToken) is { } trip ? TypedResults.Ok(trip) : TypedResults.NotFound())
            .WithName("GetTrip");

        // Rename and Change cover. Both are decisions, so they survive every rescan. See ADR-002.
        api.MapPut("/trips/{id:int}", async Task<Results<NoContent, NotFound, BadRequest<ProblemDetail>>> (
            int id, TripChange change, TripDecisions decisions, CancellationToken cancellationToken) =>
        {
            var result = await decisions.SetDetailsAsync(id, change.Name, change.CoverPhotoId, cancellationToken);
            return result.Outcome switch
            {
                DecisionOutcome.Done => TypedResults.NoContent(),
                DecisionOutcome.NotFound => TypedResults.NotFound(),
                _ => TypedResults.BadRequest(new ProblemDetail(result.Problem!)),
            };
        }).WithName("ChangeTrip");
    }
}
