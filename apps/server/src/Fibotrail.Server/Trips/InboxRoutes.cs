using Fibotrail.Detection;
using System.Globalization;
using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Trips;

/// <summary>A photo to show, with the text that describes it.</summary>
/// <param name="Alt">Alt text built from the date and the place, such as "Photo taken Mar 15, 2027 in Orlando".</param>
public sealed record PhotoInfo(string Id, string Alt, bool IsVideo);

/// <summary>A point of a route.</summary>
public sealed record RoutePoint(double Latitude, double Longitude, int PhotoCount);

/// <summary>One suggested trip, as the inbox shows it.</summary>
/// <param name="Name">The name the trip gets when you add it, unless you change it.</param>
/// <param name="Place">The main place, for the sentence "We found a possible trip: Orlando, …".</param>
/// <param name="Places">Up to three city names, by days spent, then by photo count.</param>
/// <param name="Strip">Up to eight photos, spread evenly across the trip's days.</param>
/// <param name="Route">The stops, in the order of the route.</param>
public sealed record Suggestion(
    int Id,
    TripStatus Status,
    string Name,
    string? Place,
    IReadOnlyList<string> Places,
    DateOnly StartDate,
    DateOnly EndDate,
    int PhotoCount,
    TripConfidence Confidence,
    string CoverPhotoId,
    IReadOnlyList<PhotoInfo> Strip,
    IReadOnlyList<RoutePoint> Route);

/// <summary>The inbox.</summary>
/// <param name="Suggestions">New suggestions first, then the ones you set aside with Later. Newest trip first in each group.</param>
/// <param name="Ignored">The trips you marked Not a trip, newest first.</param>
/// <param name="Count">The number for the badge: new suggestions only.</param>
public sealed record Inbox(IReadOnlyList<Suggestion> Suggestions, IReadOnlyList<Suggestion> Ignored, int Count);

/// <param name="Count">New suggestions. Trips you set aside with Later aren't counted.</param>
public sealed record InboxCount(int Count);

public enum DecisionAction
{
    /// <summary>Add trip. The name and the cover are optional.</summary>
    Add,

    /// <summary>Not a trip.</summary>
    Ignore,

    Later,

    /// <summary>Returns an ignored trip to the inbox.</summary>
    Restore,
}

public sealed record DecisionRequest(DecisionAction Action, string? Name = null, string? CoverPhotoId = null);

/// <summary>The API routes for the inbox and for decisions.</summary>
public static class InboxRoutes
{
    public const int StripLength = 8;
    public const int PlaceCount = 3;

    public static void MapInboxRoutes(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapGet("/inbox", async (FibotrailDbContext db, CancellationToken cancellationToken) =>
        {
            var trips = await db.Trips.AsNoTracking()
                .Where(trip => trip.Status != TripStatus.Added)
                .OrderByDescending(trip => trip.StartDate)
                .ToListAsync(cancellationToken);
            var cards = new List<Suggestion>();
            foreach (var trip in trips)
            {
                cards.Add(await ToSuggestionAsync(db, trip, cancellationToken));
            }

            return TypedResults.Ok(new Inbox(
                [.. cards.Where(card => card.Status == TripStatus.Suggested), .. cards.Where(card => card.Status == TripStatus.Later)],
                [.. cards.Where(card => card.Status == TripStatus.Ignored)],
                cards.Count(card => card.Status == TripStatus.Suggested)));
        }).WithName("GetInbox");

        api.MapGet("/inbox/count", async (FibotrailDbContext db, CancellationToken cancellationToken) =>
            TypedResults.Ok(new InboxCount(await db.Trips.CountAsync(trip => trip.Status == TripStatus.Suggested, cancellationToken))))
            .WithName("GetInboxCount");

        api.MapGet("/trips/{id:int}/photos", async Task<Results<Ok<IReadOnlyList<PhotoInfo>>, NotFound>> (int id, FibotrailDbContext db, CancellationToken cancellationToken) =>
        {
            if (!await db.Trips.AnyAsync(trip => trip.Id == id, cancellationToken))
            {
                return TypedResults.NotFound();
            }

            var photos = await TripPhotosAsync(db, id, cancellationToken);
            return TypedResults.Ok<IReadOnlyList<PhotoInfo>>([.. photos.Select(ToPhotoInfo)]);
        }).WithName("GetTripPhotos");

        api.MapPost("/trips/{id:int}/decision", async Task<Results<NoContent, NotFound, BadRequest<ProblemDetail>>> (
            int id, DecisionRequest request, TripDecisions decisions, CancellationToken cancellationToken) =>
        {
            var result = request.Action switch
            {
                DecisionAction.Add => await decisions.AddAsync(id, request.Name, request.CoverPhotoId, cancellationToken),
                DecisionAction.Ignore => await decisions.SetStatusAsync(id, TripStatus.Ignored, cancellationToken),
                DecisionAction.Later => await decisions.SetStatusAsync(id, TripStatus.Later, cancellationToken),
                _ => await decisions.SetStatusAsync(id, TripStatus.Suggested, cancellationToken),
            };
            return result.Outcome switch
            {
                DecisionOutcome.Done => TypedResults.NoContent(),
                DecisionOutcome.NotFound => TypedResults.NotFound(),
                _ => TypedResults.BadRequest(new ProblemDetail(result.Problem!)),
            };
        }).WithName("DecideTrip");
    }

    /// <summary>The photos of a trip, oldest first.</summary>
    public static Task<List<Photo>> TripPhotosAsync(FibotrailDbContext db, int tripId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.TripPhotos.AsNoTracking()
            .Where(link => link.TripId == tripId)
            .Join(db.Photos.AsNoTracking(), link => link.PhotoId, photo => photo.Id, (_, photo) => photo)
            .OrderBy(photo => photo.LocalTime)
            .ThenBy(photo => photo.Id)
            .ToListAsync(cancellationToken);
    }

    public static PhotoInfo ToPhotoInfo(Photo photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        var date = photo.LocalTime.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        var place = photo.City ?? photo.State ?? photo.Country;
        var kind = photo.IsVideo ? "Video" : "Photo";
        return new PhotoInfo(photo.Id, place is null ? $"{kind} taken {date}" : $"{kind} taken {date} in {place}", photo.IsVideo);
    }

    private static async Task<Suggestion> ToSuggestionAsync(FibotrailDbContext db, Trip trip, CancellationToken cancellationToken)
    {
        var photos = await TripPhotosAsync(db, trip.Id, cancellationToken);
        var places = TripDetector.RankPlaces(photos, photo => DateOnly.FromDateTime(photo.LocalTime), photo => photo.City)
            .Take(PlaceCount)
            .ToList();
        var strip = PhotoStrip.Pick(photos, photo => DateOnly.FromDateTime(photo.LocalTime), StripLength);
        return new Suggestion(
            trip.Id,
            trip.Status,
            trip.Name,
            trip.MainPlace,
            places,
            trip.StartDate,
            trip.EndDate,
            trip.PhotoCount,
            trip.Confidence,
            trip.CoverPhotoId,
            [.. strip.Select(ToPhotoInfo)],
            [.. DetectionService.ReadStops(trip.StopsJson).Select(stop => new RoutePoint(stop.Latitude, stop.Longitude, stop.PhotoCount))]);
    }
}
