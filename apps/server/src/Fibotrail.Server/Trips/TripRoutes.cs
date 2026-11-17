using Fibotrail.Server.Collections;
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

/// <summary>Put a photo on the way (true), or bring it back (false).</summary>
public sealed record OnTheWayChange(bool OnTheWay);

/// <summary>The parks you visited on a trip. Replaces the old list.</summary>
public sealed record TripParksChange(IReadOnlyList<string> Parks);

/// <summary>An open park of a collection, for the list on the trip page.</summary>
public sealed record ParkChoice(string Id, string Name, string Group, string Collection);

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

        api.MapGet("/trips/{id:int}", async Task<Results<Ok<TripDetail>, NotFound>> (int id, FibotrailDbContext db, ServerSettings settings, CollectionCatalog catalog, CancellationToken cancellationToken) =>
            await TripPage.LoadAsync(db, settings, id, cancellationToken, catalog) is { } trip ? TypedResults.Ok(trip) : TypedResults.NotFound())
            .WithName("GetTrip");

        // The Hide checkbox in the photo viewer. It answers with the trip as the page shows it, after the recompute.
        api.MapPut("/photos/{id}/on-the-way", async Task<Results<Ok<TripDetail>, NotFound>> (
            string id, OnTheWayChange change, OnTheWayPhotos photos, FibotrailDbContext db, ServerSettings settings, CollectionCatalog catalog, CancellationToken cancellationToken) =>
        {
            var result = await photos.SetAsync(id, change.OnTheWay, cancellationToken);
            return result.Outcome == OnTheWayOutcome.Done && await TripPage.LoadAsync(db, settings, result.TripId!.Value, cancellationToken, catalog) is { } trip
                ? TypedResults.Ok(trip)
                : TypedResults.NotFound();
        }).WithName("SetPhotoOnTheWay");

        // The parks you visited on a trip with no photo positions. A decision, so it survives every rescan.
        api.MapPut("/trips/{id:int}/parks", async Task<Results<Ok<TripDetail>, NotFound, BadRequest<ProblemDetail>>> (
            int id, TripParksChange change, TripDecisions decisions, FibotrailDbContext db, ServerSettings settings, CollectionCatalog catalog, CancellationToken cancellationToken) =>
        {
            var known = catalog.Collections.SelectMany(collection => collection.Places).Where(place => !place.Planned).Select(place => place.Park.Id).ToHashSet(StringComparer.Ordinal);
            var result = await decisions.SetParksAsync(id, change.Parks ?? [], known, cancellationToken);
            if (result.Outcome == DecisionOutcome.Rejected) return TypedResults.BadRequest(new ProblemDetail(result.Problem!));
            return result.Outcome == DecisionOutcome.Done && await TripPage.LoadAsync(db, settings, id, cancellationToken, catalog) is { } trip
                ? TypedResults.Ok(trip)
                : TypedResults.NotFound();
        }).WithName("SetTripParks");

        // Every open park, grouped by its collection, for the list you tick from.
        api.MapGet("/parks", (CollectionCatalog catalog) =>
            TypedResults.Ok<IReadOnlyList<ParkChoice>>([.. catalog.Collections.SelectMany(collection => collection.Places
                .Where(place => !place.Planned)
                .Select(place => new ParkChoice(place.Park.Id, place.Park.Name, place.Park.Group, collection.Name)))]))
            .WithName("GetParks");

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
