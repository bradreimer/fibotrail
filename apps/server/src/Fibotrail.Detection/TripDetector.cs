using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Fibotrail.Detection;

/// <summary>
/// Turns photos into trips with the rules of docs/specs/trip-detection.md. Every method is a pure
/// function: the same photos, home, and settings always give the same trips.
/// </summary>
public static class TripDetector
{
    /// <summary>The share above a minimum that a trip must clear to be more than Low confidence.</summary>
    public const double LowMargin = 1.25;

    public const int HighPhotos = 50;
    public const int HighDays = 3;
    public const double HighDistanceKm = 150;

    /// <summary>Runs rules 1 to 10 and returns the trips, oldest first.</summary>
    public static IReadOnlyList<DetectedTrip> Detect(IEnumerable<PhotoPoint> photos, GeoPoint home, DetectionSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(photos);
        settings ??= DetectionSettings.Default;

        // Rule 1: every photo with a capture time. The ID breaks ties, so the order never depends on the input order.
        var all = photos.OrderBy(photo => photo.LocalTime).ThenBy(photo => photo.Id, StringComparer.Ordinal).ToList();
        var unplaced = all.Where(photo => photo.Position is null).ToList();

        var trips = new List<DetectedTrip>();
        foreach (var away in SplitIntoTrips(all, home, settings).Where(group => PassesFilter(group, settings)))
        {
            // Rule 5: a photo with no position joins the trip that was under way when it was taken.
            var first = away[0].LocalTime;
            var last = away[^1].LocalTime;
            var attached = unplaced.Where(photo => photo.LocalTime >= first && photo.LocalTime <= last);
            trips.Add(Describe(away, [.. attached], home, settings));
        }

        return trips;
    }

    /// <summary>
    /// Rules 2 and 3. A photo is away when it's farther from home than the home radius. A trip ends
    /// when a photo appears at home, or when no away photo appears for the gap limit.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<PhotoPoint>> SplitIntoTrips(IEnumerable<PhotoPoint> photosByTime, GeoPoint home, DetectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(photosByTime);
        ArgumentNullException.ThrowIfNull(settings);

        var trips = new List<IReadOnlyList<PhotoPoint>>();
        var current = new List<PhotoPoint>();
        void Close()
        {
            if (current.Count > 0)
            {
                trips.Add(current);
                current = [];
            }
        }

        foreach (var photo in photosByTime)
        {
            if (photo.Position is not { } position)
            {
                continue;
            }

            if (Geo.DistanceKm(home, position) <= settings.HomeRadiusKm)
            {
                Close();
                continue;
            }

            if (current.Count > 0 && photo.LocalTime - current[^1].LocalTime >= settings.GapLimit)
            {
                Close();
            }

            current.Add(photo);
        }

        Close();
        return trips;
    }

    /// <summary>Rule 4: enough away photos, over enough calendar days.</summary>
    public static bool PassesFilter(IReadOnlyList<PhotoPoint> awayPhotos, DetectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(awayPhotos);
        ArgumentNullException.ThrowIfNull(settings);
        return awayPhotos.Count >= settings.MinimumPhotos
            && awayPhotos[^1].LocalDay.DayNumber - awayPhotos[0].LocalDay.DayNumber + 1 >= settings.MinimumDays;
    }

    /// <summary>Rule 6: the photos with a position, grouped into stops and ordered by first visit.</summary>
    public static IReadOnlyList<TripStop> FindStops(IEnumerable<PhotoPoint> awayPhotosByTime, DetectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return [.. Geo.Group(awayPhotosByTime, photo => photo.Position!.Value, settings.StopRadiusKm)
            .Select(group => new TripStop(
                Geo.Center(group.Select(photo => photo.Position!.Value)),
                group[0].LocalTime,
                group[^1].LocalTime,
                MostCommon(group, photo => photo.City) ?? MostCommon(group, photo => photo.State) ?? MostCommon(group, photo => photo.Country),
                [.. group.Select(photo => photo.Id)]))];
    }

    /// <summary>
    /// Rule 10. High needs at least 50 photos, 3 days, and 150 km from home. Low is a trip that
    /// clears the minimum photos by less than 25 percent. Everything else is Medium.
    /// </summary>
    /// <remarks>
    /// The day count has no Low margin: with a minimum of 2 days, a quarter of a day can't be
    /// measured, and every weekend trip would be Low.
    /// </remarks>
    public static Confidence Score(int photoCount, int awayPhotoCount, int days, double farthestFromHomeKm, DetectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (awayPhotoCount < settings.MinimumPhotos * LowMargin)
        {
            return Confidence.Low;
        }

        return photoCount >= HighPhotos && days >= HighDays && farthestFromHomeKm >= HighDistanceKm
            ? Confidence.High
            : Confidence.Medium;
    }

    /// <summary>
    /// The fingerprint that lets a rescan recognize a trip: a hash of the start date and the main
    /// place. It doesn't change when the trip gains photos, later days, or stops.
    /// </summary>
    public static string Fingerprint(DateOnly startDate, string? mainPlace)
    {
        var text = $"{startDate:yyyy-MM-dd}|{mainPlace?.Trim().ToUpperInvariant()}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
    }

    /// <summary>Rule 9: the main place, then the month and year of the first day.</summary>
    public static string Name(string? mainPlace, DateOnly startDate)
    {
        var month = startDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        return mainPlace is null ? $"Trip, {month}" : $"{mainPlace}, {month}";
    }

    private static DetectedTrip Describe(IReadOnlyList<PhotoPoint> away, List<PhotoPoint> attached, GeoPoint home, DetectionSettings settings)
    {
        var stops = FindStops(away, settings);

        // Rules 7 and 8: the route connects the stops in order, and the distance is its length.
        var distance = stops.Zip(stops.Skip(1), (from, to) => Geo.DistanceKm(from.Center, to.Center)).Sum();
        var farthest = stops.Max(stop => Geo.DistanceKm(home, stop.Center));

        var mainPlace = MostCommon(away, photo => photo.City) ?? MostCommon(away, photo => photo.State) ?? MostCommon(away, photo => photo.Country);
        var start = away[0].LocalDay;
        var end = away[^1].LocalDay;
        var photos = away.Concat(attached).OrderBy(photo => photo.LocalTime).ThenBy(photo => photo.Id, StringComparer.Ordinal).ToList();

        // The cover is the first image at the stop with the most photos. A video's still frame is the last resort.
        var busiest = stops.MaxBy(stop => stop.PhotoIds.Count)!;
        var videos = away.Where(photo => photo.IsVideo).Select(photo => photo.Id).ToHashSet(StringComparer.Ordinal);
        var cover = busiest.PhotoIds.FirstOrDefault(id => !videos.Contains(id)) ?? busiest.PhotoIds[0];

        return new DetectedTrip(
            Fingerprint(start, mainPlace),
            Name(mainPlace, start),
            mainPlace,
            MostCommon(away, photo => photo.Country),
            start,
            end,
            [.. photos.Select(photo => photo.Id)],
            away.Count,
            stops,
            distance,
            farthest,
            Score(photos.Count, away.Count, end.DayNumber - start.DayNumber + 1, farthest, settings),
            cover);
    }

    /// <summary>The value that the most photos share. A tie goes to the value seen first.</summary>
    internal static string? MostCommon(IEnumerable<PhotoPoint> photos, Func<PhotoPoint, string?> value) =>
        photos.Select(value)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select((text, index) => (Text: text!, Index: index))
            .GroupBy(pair => pair.Text, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Min(pair => pair.Index))
            .FirstOrDefault()?.Key;
}
