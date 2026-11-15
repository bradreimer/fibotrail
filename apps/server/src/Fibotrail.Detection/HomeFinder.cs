namespace Fibotrail.Detection;

/// <summary>A place that might be home.</summary>
/// <param name="Name">The city, state, and country that the photos there name, such as "Vancouver, British Columbia, Canada".</param>
/// <param name="Days">The distinct days with a photo there, in the months looked at.</param>
public sealed record HomeSuggestion(GeoPoint Position, string? Name, int Days);

/// <summary>A city found in the photos, for choosing a home location by name.</summary>
public sealed record KnownPlace(string Name, GeoPoint Position, int PhotoCount);

/// <summary>Suggests a home location from the photos.</summary>
public static class HomeFinder
{
    public const double AreaRadiusKm = 1;
    public const int MonthsLookedAt = 12;

    /// <summary>
    /// The 1 km area with photos on the most distinct days, in the last 12 months of the library.
    /// A tie goes to the area with more photos. Returns null when no photo has a position.
    /// </summary>
    public static HomeSuggestion? Suggest(IEnumerable<PhotoPoint> photos)
    {
        ArgumentNullException.ThrowIfNull(photos);
        var placed = photos.Where(photo => photo.Position is not null)
            .OrderBy(photo => photo.LocalTime)
            .ThenBy(photo => photo.Id, StringComparer.Ordinal)
            .ToList();
        if (placed.Count == 0)
        {
            return null;
        }

        // "The last 12 months" counts back from the newest photo, so an old library still gets a suggestion.
        var since = placed[^1].LocalTime.AddMonths(-MonthsLookedAt);
        var best = Geo.Group(placed.Where(photo => photo.LocalTime >= since), photo => photo.Position!.Value, AreaRadiusKm)
            .Select(area => (Photos: area, Days: area.Select(photo => photo.LocalDay).Distinct().Count()))
            .OrderByDescending(area => area.Days)
            .ThenByDescending(area => area.Photos.Count)
            .First();

        return new HomeSuggestion(Geo.Center(best.Photos.Select(photo => photo.Position!.Value)), PlaceName(best.Photos), best.Days);
    }

    /// <summary>Every city named by the photos, with its average position, most photos first.</summary>
    public static IReadOnlyList<KnownPlace> KnownPlaces(IEnumerable<PhotoPoint> photos)
    {
        ArgumentNullException.ThrowIfNull(photos);
        return [.. photos
            .Where(photo => photo.Position is not null && !string.IsNullOrWhiteSpace(photo.City))
            .GroupBy(photo => (photo.City, photo.State, photo.Country))
            .Select(group => new KnownPlace(
                JoinName(group.Key.City, group.Key.State, group.Key.Country)!,
                Geo.Center(group.Select(photo => photo.Position!.Value)),
                group.Count()))
            .OrderByDescending(place => place.PhotoCount)
            .ThenBy(place => place.Name, StringComparer.Ordinal)];
    }

    /// <summary>The name of the known place nearest to a point, when one is within the distance given.</summary>
    public static string? NearestPlaceName(IEnumerable<KnownPlace> places, GeoPoint point, double withinKm)
    {
        ArgumentNullException.ThrowIfNull(places);
        return places
            .Select(place => (place.Name, Distance: Geo.DistanceKm(place.Position, point)))
            .Where(place => place.Distance <= withinKm)
            .OrderBy(place => place.Distance)
            .Select(place => place.Name)
            .FirstOrDefault();
    }

    private static string? PlaceName(IReadOnlyList<PhotoPoint> photos) => JoinName(
        TripDetector.MostCommon(photos, photo => photo.City),
        TripDetector.MostCommon(photos, photo => photo.State),
        TripDetector.MostCommon(photos, photo => photo.Country));

    private static string? JoinName(params string?[] parts)
    {
        var known = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();
        return known.Count == 0 ? null : string.Join(", ", known);
    }
}
