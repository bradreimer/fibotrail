using System.Globalization;
using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Trips;

/// <summary>One photo on the trip page.</summary>
/// <param name="LocalTime">The wall-clock time where the photo was taken.</param>
/// <param name="Place">The city, state, or country of the photo, when it has a position.</param>
/// <param name="StopIndex">The stop where the photo was taken, or null for a photo with no position.</param>
/// <param name="DurationMs">The length of a video, in milliseconds.</param>
/// <param name="ImmichUrl">The photo in Immich, at the address your browser uses. Null when IMMICH_EXTERNAL_URL is empty.</param>
/// <param name="OnTheWay">True when you put the photo on the way: it stays in the trip but adds no stop to the map.</param>
public sealed record TripPhotoInfo(
    string Id,
    string Alt,
    DateTime LocalTime,
    double? Latitude,
    double? Longitude,
    string? Place,
    int? StopIndex,
    bool IsVideo,
    int? DurationMs,
    string? ImmichUrl,
    bool OnTheWay);

/// <summary>The photos of one local day.</summary>
/// <param name="Number">1 for the trip's first day.</param>
/// <param name="Place">The city named by the most photos that day.</param>
public sealed record TripDay(int Number, DateOnly Date, string? Place, IReadOnlyList<TripPhotoInfo> Photos);

/// <summary>A stop on the route.</summary>
/// <param name="Index">The position of the stop in the route, from 0.</param>
public sealed record TripStopInfo(int Index, double Latitude, double Longitude, string? Place, int PhotoCount);

/// <summary>The numbers of the stat tiles.</summary>
/// <param name="Unplaced">Photos with no position. They belong to the trip and to no stop.</param>
public sealed record TripStats(int Days, int Photos, int Stops, double DistanceKm, double FarthestFromHomeKm, int Unplaced);

/// <summary>Everything the trip page shows for one trip.</summary>
public sealed record TripDetail(
    int Id,
    TripStatus Status,
    string Name,
    string SuggestedName,
    string? Place,
    string? Country,
    DateOnly StartDate,
    DateOnly EndDate,
    string CoverPhotoId,
    TripStats Stats,
    IReadOnlyList<TripStopInfo> Stops,
    IReadOnlyList<TripDay> Days);

/// <summary>Builds the trip page from the stored trip and its photos.</summary>
public static class TripPage
{
    /// <summary>The address of a photo in Immich, or null when IMMICH_EXTERNAL_URL is empty.</summary>
    public static string? ImmichPhotoUrl(Uri? externalUrl, string photoId)
    {
        ArgumentNullException.ThrowIfNull(photoId);
        return externalUrl is null ? null : $"{externalUrl.AbsoluteUri.TrimEnd('/')}/photos/{Uri.EscapeDataString(photoId)}";
    }

    public static async Task<TripDetail?> LoadAsync(FibotrailDbContext db, ServerSettings settings, int id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(settings);
        var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        var links = await db.TripPhotos.AsNoTracking().Where(link => link.TripId == id).ToDictionaryAsync(link => link.PhotoId, link => link.StopIndex, cancellationToken);
        var photos = await InboxRoutes.TripPhotosAsync(db, id, cancellationToken);
        var stops = DetectionService.ReadStops(trip.StopsJson);
        var onTheWay = (await db.OnTheWayPhotos.AsNoTracking().Select(photo => photo.PhotoId).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

        // Photos are grouped by the local day they were taken, so 23:30 abroad stays on its day.
        var days = photos
            .GroupBy(photo => DateOnly.FromDateTime(photo.LocalTime))
            .OrderBy(group => group.Key)
            .Select(group => new TripDay(
                group.Key.DayNumber - trip.StartDate.DayNumber + 1,
                group.Key,
                group.Where(photo => photo.City is not null)
                    .GroupBy(photo => photo.City!)
                    .OrderByDescending(city => city.Count())
                    .ThenBy(city => city.Key, StringComparer.Ordinal)
                    .Select(city => city.Key)
                    .FirstOrDefault(),
                [.. group.Select(photo => new TripPhotoInfo(
                    photo.Id,
                    onTheWay.Contains(photo.Id) ? $"{InboxRoutes.ToPhotoInfo(photo).Alt}, hidden from the map" : InboxRoutes.ToPhotoInfo(photo).Alt,
                    photo.LocalTime,
                    photo.Latitude,
                    photo.Longitude,
                    photo.City ?? photo.State ?? photo.Country,
                    links.GetValueOrDefault(photo.Id),
                    photo.IsVideo,
                    photo.DurationMs,
                    ImmichPhotoUrl(settings.ImmichExternalUrl, photo.Id),
                    onTheWay.Contains(photo.Id)))]))
            .ToList();

        return new TripDetail(
            trip.Id,
            trip.Status,
            trip.Name,
            trip.SuggestedName,
            trip.MainPlace,
            trip.Country,
            trip.StartDate,
            trip.EndDate,
            trip.CoverPhotoId,
            new TripStats(
                trip.Days,
                photos.Count,
                stops.Count,
                Math.Round(trip.DistanceKm, 1),
                Math.Round(trip.FarthestFromHomeKm, 1),
                photos.Count(photo => photo.Latitude is null)),
            [.. stops.Select((stop, index) => new TripStopInfo(index, stop.Latitude, stop.Longitude, stop.Place, stop.PhotoCount))],
            days);
    }

    /// <summary>A day heading, such as "Day 3 · Wed, Mar 17 · Orlando". The web app builds its own; this is for tests and logs.</summary>
    public static string DayHeading(TripDay day)
    {
        ArgumentNullException.ThrowIfNull(day);
        var date = day.Date.ToString("ddd, MMM d", CultureInfo.InvariantCulture);
        return day.Place is null ? $"Day {day.Number} · {date}" : $"Day {day.Number} · {date} · {day.Place}";
    }
}
