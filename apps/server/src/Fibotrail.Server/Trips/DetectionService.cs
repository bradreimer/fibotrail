using System.Text.Json;
using Fibotrail.Detection;
using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Trips;

/// <summary>A stop, as it's stored with its trip.</summary>
/// <param name="Place">The city, state, or country that the photos there name.</param>
/// <param name="FirstTime">The local time of the first photo at the stop.</param>
public sealed record StoredStop(double Latitude, double Longitude, string? Place, DateTime FirstTime, DateTime LastTime, int PhotoCount);

/// <summary>What one detection run did.</summary>
/// <param name="Ran">False when no home location is confirmed, so nothing was detected.</param>
/// <param name="Trips">Trips found.</param>
/// <param name="NewSuggestions">Trips that weren't stored before and that no decision covers.</param>
public sealed record DetectionResult(bool Ran, int Trips, int NewSuggestions);

/// <summary>
/// Runs the detection rules on the photo table and stores the result. Detected data is replaced.
/// Decisions are applied on top and are never changed, except to follow a trip whose fingerprint
/// moved. See docs/specs/trip-detection.md and ADR-002.
/// </summary>
public sealed partial class DetectionService(FibotrailDbContext db, HomeLocation home, ILogger<DetectionService> logger) : IScanCompletedHandler
{
    private static readonly JsonSerializerOptions StopJson = new(JsonSerializerDefaults.Web);

    public Task OnScanCompletedAsync(CancellationToken cancellationToken) => RunAsync(cancellationToken);

    public static IReadOnlyList<StoredStop> ReadStops(string json) =>
        JsonSerializer.Deserialize<List<StoredStop>>(json, StopJson) ?? [];

    public async Task<DetectionResult> RunAsync(CancellationToken cancellationToken)
    {
        var confirmed = await home.GetAsync(cancellationToken);
        if (confirmed is null)
        {
            // Without a home location, every photo would look like a trip.
            return new DetectionResult(false, 0, 0);
        }

        var photos = await HomeLocation.LoadPhotosAsync(db, placedOnly: false, cancellationToken);
        var detected = TripDetector.Detect(photos, new GeoPoint(confirmed.Latitude, confirmed.Longitude));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var trips = await db.Trips.ToListAsync(cancellationToken);
        var decisions = await db.Decisions.ToListAsync(cancellationToken);
        var tripMatches = Reconciler.Match(detected, trips.Select(trip => new StoredTrip<int>(trip.Id, trip.Fingerprint, trip.StartDate, trip.EndDate)));
        var decisionMatches = Reconciler.Match(detected, decisions.Select(decision => new StoredTrip<int>(decision.Id, decision.Fingerprint, decision.StartDate, decision.EndDate)));

        // Trip photos are detected data: they're replaced as a whole.
        await db.TripPhotos.ExecuteDeleteAsync(cancellationToken);

        // Free every fingerprint first, so that two trips can swap fingerprints without breaking the unique index.
        foreach (var trip in trips)
        {
            trip.Fingerprint = $"replaced-{trip.Id}";
        }

        await db.SaveChangesAsync(cancellationToken);

        var kept = new HashSet<int>();
        var newSuggestions = 0;
        for (var index = 0; index < detected.Count; index++)
        {
            var found = detected[index];
            var trip = tripMatches[index] is { } tripId ? trips.Single(candidate => candidate.Id == tripId) : null;
            var decision = decisionMatches[index] is { } decisionId ? decisions.Single(candidate => candidate.Id == decisionId) : null;
            if (trip is null)
            {
                trip = new Trip { Fingerprint = found.Fingerprint, Name = found.Name, SuggestedName = found.Name, CoverPhotoId = found.CoverPhotoId, SuggestedCoverPhotoId = found.CoverPhotoId, StopsJson = "[]" };
                db.Trips.Add(trip);
                if (decision is null)
                {
                    newSuggestions++;
                }
            }
            else
            {
                kept.Add(trip.Id);
            }

            if (decision is not null)
            {
                // The decision follows its trip: a later run then matches it by fingerprint.
                decision.Fingerprint = found.Fingerprint;
                decision.StartDate = found.StartDate;
                decision.EndDate = found.EndDate;
            }

            Apply(trip, found, decision);
        }

        // A stored trip that detection no longer finds is detected data that is gone. Its decision stays.
        db.Trips.RemoveRange(trips.Where(trip => !kept.Contains(trip.Id)));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        LogDetected(logger, detected.Count, newSuggestions);
        return new DetectionResult(true, detected.Count, newSuggestions);
    }

    /// <summary>Writes the detected data, then the decision on top. The decision always wins.</summary>
    private static void Apply(Trip trip, DetectedTrip found, Decision? decision)
    {
        trip.Fingerprint = found.Fingerprint;
        trip.SuggestedName = found.Name;
        trip.SuggestedCoverPhotoId = found.CoverPhotoId;
        trip.MainPlace = found.MainPlace;
        trip.Country = found.Country;
        trip.StartDate = found.StartDate;
        trip.EndDate = found.EndDate;
        trip.Days = found.Days;
        trip.PhotoCount = found.PhotoIds.Count;
        trip.DistanceKm = found.DistanceKm;
        trip.FarthestFromHomeKm = found.FarthestFromHomeKm;
        trip.Confidence = found.Confidence switch
        {
            Confidence.High => TripConfidence.High,
            Confidence.Low => TripConfidence.Low,
            _ => TripConfidence.Medium,
        };
        trip.StopsJson = JsonSerializer.Serialize(
            found.Stops.Select(stop => new StoredStop(stop.Center.Latitude, stop.Center.Longitude, stop.Place, stop.FirstTime, stop.LastTime, stop.PhotoIds.Count)),
            StopJson);

        var stopOf = found.Stops
            .SelectMany((stop, index) => stop.PhotoIds.Select(id => (id, index)))
            .ToDictionary(pair => pair.id, pair => pair.index, StringComparer.Ordinal);
        trip.Photos.Clear();
        trip.Photos.AddRange(found.PhotoIds.Select(id => new TripPhoto { PhotoId = id, StopIndex = stopOf.TryGetValue(id, out var stop) ? stop : null }));

        trip.Status = decision?.Status ?? TripStatus.Suggested;
        trip.Name = decision?.Name ?? found.Name;

        // A cover you chose stays for as long as its photo is part of the trip.
        trip.CoverPhotoId = decision?.CoverPhotoId is { } cover && found.PhotoIds.Contains(cover) ? cover : found.CoverPhotoId;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Detection found {Trips} trips. {New} are new suggestions.")]
    private static partial void LogDetected(ILogger logger, int trips, int @new);
}
