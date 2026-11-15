using System.Globalization;
using Fibotrail.Detection;
using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Trips;

/// <summary>A place on the map, with the name Fibotrail shows for it.</summary>
public sealed record PlaceInfo(double Latitude, double Longitude, string Name);

/// <summary>A place that might be home, and how often you took photos there.</summary>
/// <param name="Days">Distinct days with a photo there, in the last 12 months of the library.</param>
public sealed record HomeSuggestionInfo(double Latitude, double Longitude, string Name, int Days);

/// <summary>What the home location step shows.</summary>
/// <param name="Confirmed">The home location you confirmed, or null before you did.</param>
/// <param name="Suggestion">Where Fibotrail thinks home is, or null when it can't tell.</param>
/// <param name="HasPositions">False when no scanned photo has a position, so no trip can be found.</param>
/// <param name="PhotoCount">Photos in Fibotrail's database.</param>
public sealed record HomeState(PlaceInfo? Confirmed, HomeSuggestionInfo? Suggestion, bool HasPositions, int PhotoCount);

/// <summary>Reads and saves the home location, which lives in the setting table.</summary>
public sealed class HomeLocation(FibotrailDbContext db)
{
    public const string LatitudeKey = "home.latitude";
    public const string LongitudeKey = "home.longitude";
    public const string NameKey = "home.name";

    /// <summary>A pin this close to a city in your photos takes that city's name.</summary>
    public const double NameWithinKm = 50;

    public async Task<PlaceInfo?> GetAsync(CancellationToken cancellationToken)
    {
        var values = await db.Settings.AsNoTracking()
            .Where(setting => setting.Key.StartsWith("home."))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);
        return values.TryGetValue(LatitudeKey, out var latitude) && values.TryGetValue(LongitudeKey, out var longitude)
            ? new PlaceInfo(
                double.Parse(latitude, CultureInfo.InvariantCulture),
                double.Parse(longitude, CultureInfo.InvariantCulture),
                values.GetValueOrDefault(NameKey) ?? Coordinates(double.Parse(latitude, CultureInfo.InvariantCulture), double.Parse(longitude, CultureInfo.InvariantCulture)))
            : null;
    }

    public async Task<HomeState> GetStateAsync(CancellationToken cancellationToken)
    {
        var confirmed = await GetAsync(cancellationToken);
        var photos = await LoadPhotosAsync(db, placedOnly: true, cancellationToken);
        var suggestion = HomeFinder.Suggest(photos);
        return new HomeState(
            confirmed,
            suggestion is null
                ? null
                : new HomeSuggestionInfo(
                    suggestion.Position.Latitude,
                    suggestion.Position.Longitude,
                    suggestion.Name ?? Coordinates(suggestion.Position.Latitude, suggestion.Position.Longitude),
                    suggestion.Days),
            photos.Count > 0,
            await db.Photos.CountAsync(cancellationToken));
    }

    /// <summary>The cities in your photos whose names contain the text, most photos first.</summary>
    public async Task<IReadOnlyList<PlaceInfo>> SearchPlacesAsync(string? query, int limit, CancellationToken cancellationToken)
    {
        var places = HomeFinder.KnownPlaces(await LoadPhotosAsync(db, placedOnly: true, cancellationToken));
        var text = query?.Trim() ?? string.Empty;
        return [.. places
            .Where(place => place.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(place => new PlaceInfo(place.Position.Latitude, place.Position.Longitude, place.Name))];
    }

    /// <summary>Saves the home location. The name comes from the nearest city in your photos.</summary>
    public async Task<PlaceInfo> SetAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        var places = HomeFinder.KnownPlaces(await LoadPhotosAsync(db, placedOnly: true, cancellationToken));
        var name = HomeFinder.NearestPlaceName(places, new GeoPoint(latitude, longitude), NameWithinKm) ?? Coordinates(latitude, longitude);
        await SaveAsync(LatitudeKey, latitude.ToString("R", CultureInfo.InvariantCulture), cancellationToken);
        await SaveAsync(LongitudeKey, longitude.ToString("R", CultureInfo.InvariantCulture), cancellationToken);
        await SaveAsync(NameKey, name, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new PlaceInfo(latitude, longitude, name);
    }

    /// <summary>The photos, as detection reads them.</summary>
    public static async Task<List<PhotoPoint>> LoadPhotosAsync(FibotrailDbContext db, bool placedOnly, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var photos = db.Photos.AsNoTracking();
        if (placedOnly)
        {
            photos = photos.Where(photo => photo.Latitude != null && photo.Longitude != null);
        }

        return await photos
            .Select(photo => new PhotoPoint(photo.Id, photo.LocalTime, photo.Latitude, photo.Longitude, photo.City, photo.State, photo.Country, photo.IsVideo))
            .ToListAsync(cancellationToken);
    }

    private static string Coordinates(double latitude, double longitude) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Abs(latitude):0.00}° {(latitude >= 0 ? "N" : "S")}, {Math.Abs(longitude):0.00}° {(longitude >= 0 ? "E" : "W")}");

    private async Task SaveAsync(string key, string value, CancellationToken cancellationToken)
    {
        var setting = await db.Settings.FindAsync([key], cancellationToken);
        if (setting is null)
        {
            db.Settings.Add(new Setting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
    }
}
