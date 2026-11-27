using Fibotrail.Detection;
using Fibotrail.Detection.Parks;
using Fibotrail.Places;
using Fibotrail.Server.Collections;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Fibotrail.Stats;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Stats;

/// <summary>
/// Builds the stats library from the database, once per request. The Insights totals, the
/// collections, and the MCP server all read it, so the same question gets the same answer
/// everywhere. See docs/specs/insights.md.
/// </summary>
public static class StatsLibraryLoader
{
    /// <summary>Reads the added trips and their positioned away photos, and builds the library.</summary>
    public static async Task<StatsLibrary> LoadAsync(FibotrailDbContext db, CountryFinder finder, CountryOutlinesCache outlines, CollectionCatalog catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(finder);
        ArgumentNullException.ThrowIfNull(outlines);
        ArgumentNullException.ThrowIfNull(catalog);

        // Only added trips count. A suggested, later, or ignored trip never changes a number.
        var trips = await db.Trips.AsNoTracking().Where(trip => trip.Status == TripStatus.Added).ToListAsync(cancellationToken);
        var ids = trips.Select(trip => trip.Id).ToList();
        var onTheWay = db.OnTheWayPhotos.Select(photo => photo.PhotoId);
        var photos = await (from tripPhoto in db.TripPhotos.AsNoTracking()
                            join photo in db.Photos.AsNoTracking() on tripPhoto.PhotoId equals photo.Id
                            where ids.Contains(tripPhoto.TripId) && photo.Latitude != null && photo.Longitude != null && !onTheWay.Contains(photo.Id)
                            select new { tripPhoto.TripId, photo.LocalTime, Latitude = photo.Latitude!.Value, Longitude = photo.Longitude!.Value })
            .ToListAsync(cancellationToken);
        var byTrip = photos.ToLookup(photo => photo.TripId);

        // The main place is a city only when a photo of the trip has that city name. Otherwise it is a
        // state or a country, and the trip has no main city. See ADR-023.
        var withMainCity = (await (from tripPhoto in db.TripPhotos.AsNoTracking()
                                   join photo in db.Photos.AsNoTracking() on tripPhoto.PhotoId equals photo.Id
                                   join trip in db.Trips.AsNoTracking() on tripPhoto.TripId equals trip.Id
                                   where ids.Contains(tripPhoto.TripId) && photo.City != null && photo.City == trip.MainPlace
                                   select tripPhoto.TripId)
            .Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();
        var fingerprints = trips.Select(trip => trip.Fingerprint).ToList();
        var marked = (await db.Decisions.AsNoTracking().Where(decision => fingerprints.Contains(decision.Fingerprint)).ToListAsync(cancellationToken))
            .ToDictionary(decision => decision.Fingerprint, decision => TripDecisions.ReadParks(decision.VisitedParks), StringComparer.Ordinal);

        return StatsLibrary.Build(
            trips.Select(trip => new StatsTrip(
                trip.Id,
                trip.Name,
                trip.StartDate,
                trip.EndDate,
                trip.Days,
                trip.DistanceKm,
                trip.FarthestFromHomeKm,
                [.. DetectionService.ReadStops(trip.StopsJson).Select(stop => (stop.Latitude, stop.Longitude))],
                [.. byTrip[trip.Id].Select(photo => new PhotoPosition(DateOnly.FromDateTime(photo.LocalTime), photo.Latitude, photo.Longitude))],
                [.. DetectionService.ReadStops(trip.StopsJson).Select(stop => new NamedStop(stop.Place, stop.Latitude, stop.Longitude, stop.PhotoCount))],
                marked.GetValueOrDefault(trip.Fingerprint),
                withMainCity.Contains(trip.Id) ? trip.MainPlace : null)),
            finder,
            outlines.Outlines,
            catalog.Lookup);
    }
}

/// <summary>The bundled outlines, read once, for the countries total.</summary>
public sealed class CountryOutlinesCache
{
    public CountryOutlinesCache() => Outlines = CountryOutlines.Read();

    public IReadOnlyList<CountryOutline> Outlines { get; }
}
