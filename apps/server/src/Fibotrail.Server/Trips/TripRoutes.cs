using Fibotrail.Server.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Trips;

/// <summary>A trip in your library, as its card shows it.</summary>
/// <param name="Days">Calendar days from the first day to the last, counting both.</param>
/// <param name="Place">The main destination: the city with the most photos, or the state or country.</param>
public sealed record TripSummary(int Id, string Name, DateOnly StartDate, DateOnly EndDate, int Days, string? Place, string CoverPhotoId);

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
    }
}
