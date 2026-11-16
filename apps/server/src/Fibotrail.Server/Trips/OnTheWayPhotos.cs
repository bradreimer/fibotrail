using Fibotrail.Detection;
using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Trips;

public enum OnTheWayOutcome
{
    Done,

    /// <summary>The photo isn't in any trip, so it has no stop to leave.</summary>
    NotFound,
}

/// <param name="TripId">The trip that was recomputed.</param>
public sealed record OnTheWayResult(OnTheWayOutcome Outcome, int? TripId = null);

/// <summary>
/// Puts a photo on the way, or brings it back. The photo stays in its trip. The trip's stops,
/// route, distance, farthest point, and cover are recomputed at once, because they're stored with
/// the trip (ADR-010), and no other trip is touched. The choice is a decision keyed by the photo,
/// so every later detection run applies it. See docs/specs/on-the-way.md and ADR-002.
/// </summary>
public sealed class OnTheWayPhotos(FibotrailDbContext db, HomeLocation home, TimeProvider time)
{
    public async Task<OnTheWayResult> SetAsync(string photoId, bool onTheWay, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(photoId);
        var link = await db.TripPhotos.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.PhotoId == photoId, cancellationToken);
        if (link is null)
        {
            return new OnTheWayResult(OnTheWayOutcome.NotFound);
        }

        var row = await db.OnTheWayPhotos.FindAsync([photoId], cancellationToken);
        if (onTheWay && row is null)
        {
            db.OnTheWayPhotos.Add(new OnTheWayPhoto { PhotoId = photoId, DecidedAt = time.GetUtcNow().UtcDateTime });
        }
        else if (!onTheWay && row is not null)
        {
            db.OnTheWayPhotos.Remove(row);
        }

        await db.SaveChangesAsync(cancellationToken);
        await RecomputeAsync(link.TripId, cancellationToken);
        return new OnTheWayResult(OnTheWayOutcome.Done, link.TripId);
    }

    /// <summary>Runs rules 6 to 8 again on one trip's photos, with the photos on the way left out.</summary>
    private async Task RecomputeAsync(int tripId, CancellationToken cancellationToken)
    {
        var confirmed = await home.GetAsync(cancellationToken);
        if (confirmed is null)
        {
            return;
        }

        var trip = await db.Trips.Include(candidate => candidate.Photos).SingleAsync(candidate => candidate.Id == tripId, cancellationToken);
        var points = await db.TripPhotos.AsNoTracking()
            .Where(link => link.TripId == tripId)
            .Join(db.Photos.AsNoTracking(), link => link.PhotoId, photo => photo.Id, (_, photo) => new PhotoPoint(photo.Id, photo.LocalTime, photo.Latitude, photo.Longitude, photo.City, photo.State, photo.Country, photo.IsVideo))
            .ToListAsync(cancellationToken);
        points = [.. points.OrderBy(point => point.LocalTime).ThenBy(point => point.Id, StringComparer.Ordinal)];

        var away = points.Where(point => point.Position is not null).ToList();
        var photoIds = points.Select(point => point.Id).ToList();
        var onTheWay = (await db.OnTheWayPhotos.AsNoTracking().Select(photo => photo.PhotoId).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var settings = DetectionSettings.Default;
        var geometry = TripDetector.Measure(away, photoIds, new GeoPoint(confirmed.Latitude, confirmed.Longitude), settings, onTheWay);

        trip.StopsJson = DetectionService.StoreStops(geometry.Stops);
        trip.DistanceKm = geometry.DistanceKm;
        trip.FarthestFromHomeKm = geometry.FarthestFromHomeKm;
        trip.Confidence = TripDetector.Score(photoIds.Count, away.Count, trip.Days, geometry.FarthestFromHomeKm, settings) switch
        {
            Confidence.High => TripConfidence.High,
            Confidence.Low => TripConfidence.Low,
            _ => TripConfidence.Medium,
        };

        // A cover you chose stays for as long as its photo is part of the trip.
        trip.SuggestedCoverPhotoId = geometry.CoverPhotoId;
        var decision = await db.Decisions.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Fingerprint == trip.Fingerprint, cancellationToken);
        trip.CoverPhotoId = decision?.CoverPhotoId is { } cover && photoIds.Contains(cover) ? cover : geometry.CoverPhotoId;

        var stopOf = geometry.Stops
            .SelectMany((stop, index) => stop.PhotoIds.Select(id => (id, index)))
            .ToDictionary(pair => pair.id, pair => pair.index, StringComparer.Ordinal);
        foreach (var link in trip.Photos)
        {
            link.StopIndex = stopOf.TryGetValue(link.PhotoId, out var stop) ? stop : null;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
