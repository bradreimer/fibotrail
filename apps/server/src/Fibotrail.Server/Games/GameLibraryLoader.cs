using Fibotrail.Games;
using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Games;

/// <summary>
/// Builds the game library from the database, once per request. It's the only game code that reads
/// another feature's tables, so it's the one place to change when a game needs a new fact. See
/// docs/specs/games.md#the-game-library-the-games-api and ADR-022.
/// </summary>
public static class GameLibraryLoader
{
    public static async Task<GameLibrary> LoadAsync(FibotrailDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        // Only added trips, as for the insights. A suggestion never shows up in a game.
        var trips = await db.Trips.AsNoTracking()
            .Where(trip => trip.Status == TripStatus.Added)
            .Select(trip => new { trip.Id, trip.Name, trip.StartDate, trip.EndDate, trip.MainPlace })
            .ToListAsync(cancellationToken);
        var ids = trips.Select(trip => trip.Id).ToList();
        var onTheWay = db.OnTheWayPhotos.Select(photo => photo.PhotoId);
        var photos = await (from tripPhoto in db.TripPhotos.AsNoTracking()
                            join photo in db.Photos.AsNoTracking() on tripPhoto.PhotoId equals photo.Id
                            where ids.Contains(tripPhoto.TripId) && !photo.IsVideo && !onTheWay.Contains(photo.Id)
                            select new { tripPhoto.TripId, photo.Id, photo.LocalTime, photo.Latitude, photo.Longitude })
            .ToListAsync(cancellationToken);
        var byTrip = photos.ToLookup(photo => photo.TripId);

        return new GameLibrary([.. trips.Select(trip => new GameTrip(
            trip.Id,
            trip.Name,
            trip.StartDate,
            trip.EndDate,
            trip.MainPlace,
            [.. byTrip[trip.Id].Select(photo => new GamePhoto(photo.Id, photo.LocalTime, photo.Latitude, photo.Longitude))]))]);
    }
}
