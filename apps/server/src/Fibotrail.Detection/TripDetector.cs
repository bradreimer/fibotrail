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

    /// <summary>
    /// Runs rules 1 to 10 and returns the trips, oldest first. A photo whose ID is in
    /// <paramref name="onTheWay"/> still belongs to its trip, but rules 6 to 8 skip it: it adds no
    /// stop, no line, and no distance. See docs/specs/on-the-way.md.
    /// </summary>
    public static IReadOnlyList<DetectedTrip> Detect(IEnumerable<PhotoPoint> photos, GeoPoint home, DetectionSettings? settings = null, IReadOnlySet<string>? onTheWay = null)
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
            trips.Add(Describe(away, [.. attached], home, settings, onTheWay));
        }

        trips.AddRange(DetectUnlocated(all, trips, home, settings, onTheWay));
        return [.. trips.OrderBy(trip => trip.StartDate).ThenBy(trip => trip.Fingerprint, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Rule 3b, for photos from before cameras recorded a position. A run of consecutive days that
    /// each have at least <see cref="DetectionSettings.BurstPhotosPerDay"/> photos with no position
    /// is a trip, when the run has at least <see cref="DetectionSettings.BurstMinimumDays"/> days.
    /// A day already inside a trip, or with any photo placed at home, is not a burst day. The trip
    /// has no stops and no place, and it's always Low confidence: only its dates and photos are known.
    /// </summary>
    public static IReadOnlyList<DetectedTrip> DetectUnlocated(IReadOnlyList<PhotoPoint> allByTime, IReadOnlyList<DetectedTrip> found, GeoPoint home, DetectionSettings settings, IReadOnlySet<string>? onTheWay = null)
    {
        ArgumentNullException.ThrowIfNull(allByTime);
        ArgumentNullException.ThrowIfNull(found);
        ArgumentNullException.ThrowIfNull(settings);

        var byDay = allByTime.GroupBy(photo => photo.LocalDay).ToDictionary(group => group.Key, group => group.ToList());
        bool Free(KeyValuePair<DateOnly, List<PhotoPoint>> day) =>
            !day.Value.Any(photo => photo.Position is { } position && Geo.DistanceKm(home, position) <= settings.HomeRadiusKm)
            && !found.Any(trip => trip.StartDate <= day.Key && day.Key <= trip.EndDate);
        int Unplaced(KeyValuePair<DateOnly, List<PhotoPoint>> day) => day.Value.Count(photo => photo.Position is null);

        var burstDays = byDay
            .Where(day => Unplaced(day) >= settings.BurstPhotosPerDay && Free(day))
            .Select(day => day.Key)
            .Order()
            .ToList();

        // A travel day next to a burst is lighter, so it joins with a lower bar, for a few days at most.
        DateOnly Extend(DateOnly edge, int step)
        {
            for (var added = 0; added < settings.BurstEdgeDays; added++)
            {
                var next = edge.AddDays(step);
                if (!byDay.TryGetValue(next, out var photos))
                {
                    break;
                }

                var day = KeyValuePair.Create(next, photos);
                if (Unplaced(day) < settings.BurstEdgePhotosPerDay || !Free(day))
                {
                    break;
                }

                edge = next;
            }

            return edge;
        }

        var trips = new List<DetectedTrip>();
        var run = new List<DateOnly>();
        void Close()
        {
            if (run.Count >= settings.BurstMinimumDays)
            {
                var first = Extend(run[0], -1);
                var last = Extend(run[^1], 1);
                var photos = Enumerable.Range(0, last.DayNumber - first.DayNumber + 1)
                    .SelectMany(offset => byDay.GetValueOrDefault(first.AddDays(offset)) ?? [])
                    .Where(photo => photo.Position is null)
                    .ToList();
                var ids = photos.Select(photo => photo.Id).ToList();
                var geometry = Measure([], ids, home, settings, onTheWay);
                trips.Add(new DetectedTrip(
                    Fingerprint(first, null), Name(null, first), null, null, first, last, ids, 0,
                    geometry.Stops, geometry.DistanceKm, geometry.FarthestFromHomeKm, Confidence.Low, geometry.CoverPhotoId));
            }

            run = [];
        }

        foreach (var day in burstDays)
        {
            if (run.Count > 0 && day.DayNumber - run[^1].DayNumber > 1)
            {
                Close();
            }

            run.Add(day);
        }

        Close();
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

        // Only photos with a position matter here. A photo with no position joins a trip later, by rule 5.
        var placed = photosByTime.Where(photo => photo.Position is not null).ToList();
        for (var index = 0; index < placed.Count; index++)
        {
            var photo = placed[index];
            var position = photo.Position!.Value;
            if (Geo.DistanceKm(home, position) <= settings.HomeRadiusKm)
            {
                // A short run at home between two close away photos is a stray photo, such as a
                // household phone syncing. It doesn't end the trip. See open question 2 of the spec.
                var next = index;
                while (next < placed.Count && Geo.DistanceKm(home, placed[next].Position!.Value) <= settings.HomeRadiusKm)
                {
                    next++;
                }

                if (current.Count > 0 && next < placed.Count && next - index <= settings.HomeBlipPhotos
                    && placed[next].LocalTime - current[^1].LocalTime <= settings.HomeBlipSpan)
                {
                    index = next - 1;
                    continue;
                }

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

    /// <summary>
    /// Rules 6 to 8 and the suggested cover: the stops, the route's distance, the farthest stop from
    /// home, and the cover photo. Photos in <paramref name="onTheWay"/> join no stop. Detection and
    /// the recompute after you hide a photo both call this, so they always agree.
    /// </summary>
    /// <param name="awayPhotosByTime">The trip's photos that have a position, oldest first.</param>
    /// <param name="tripPhotoIds">Every photo of the trip, oldest first. The cover comes from here when no stop is left.</param>
    public static TripGeometry Measure(
        IReadOnlyList<PhotoPoint> awayPhotosByTime,
        IReadOnlyList<string> tripPhotoIds,
        GeoPoint home,
        DetectionSettings settings,
        IReadOnlySet<string>? onTheWay = null)
    {
        ArgumentNullException.ThrowIfNull(awayPhotosByTime);
        ArgumentNullException.ThrowIfNull(tripPhotoIds);
        ArgumentNullException.ThrowIfNull(settings);

        var onTheRoute = onTheWay is null || onTheWay.Count == 0
            ? awayPhotosByTime
            : [.. awayPhotosByTime.Where(photo => !onTheWay.Contains(photo.Id))];
        var stops = FindStops(onTheRoute, settings);

        // Rules 7 and 8: the route connects the stops in order, and the distance is its length.
        var distance = stops.Zip(stops.Skip(1), (from, to) => Geo.DistanceKm(from.Center, to.Center)).Sum();
        var farthest = stops.Count == 0 ? 0 : stops.Max(stop => Geo.DistanceKm(home, stop.Center));

        // The cover is the first image at the stop with the most photos. A video's still frame is the last resort.
        var videos = awayPhotosByTime.Where(photo => photo.IsVideo).Select(photo => photo.Id).ToHashSet(StringComparer.Ordinal);
        string cover;
        if (stops.Count > 0)
        {
            var busiest = stops.MaxBy(stop => stop.PhotoIds.Count)!;
            cover = busiest.PhotoIds.FirstOrDefault(id => !videos.Contains(id)) ?? busiest.PhotoIds[0];
        }
        else
        {
            // Every away photo is on the way, so the trip has no stop. Its cover is still one of its photos.
            cover = tripPhotoIds.FirstOrDefault(id => !videos.Contains(id)) ?? tripPhotoIds[0];
        }

        return new TripGeometry(stops, distance, farthest, cover);
    }

    private static DetectedTrip Describe(IReadOnlyList<PhotoPoint> away, List<PhotoPoint> attached, GeoPoint home, DetectionSettings settings, IReadOnlySet<string>? onTheWay)
    {
        var mainPlace = MainPlace(away);
        var start = away[0].LocalDay;
        var end = away[^1].LocalDay;
        var photos = away.Concat(attached).OrderBy(photo => photo.LocalTime).ThenBy(photo => photo.Id, StringComparer.Ordinal).ToList();
        var geometry = Measure(away, [.. photos.Select(photo => photo.Id)], home, settings, onTheWay);

        return new DetectedTrip(
            Fingerprint(start, mainPlace),
            Name(mainPlace, start),
            mainPlace,
            MostCommon(away, photo => photo.Country),
            start,
            end,
            [.. photos.Select(photo => photo.Id)],
            away.Count,
            geometry.Stops,
            geometry.DistanceKm,
            geometry.FarthestFromHomeKm,
            Score(photos.Count, away.Count, end.DayNumber - start.DayNumber + 1, geometry.FarthestFromHomeKm, settings),
            geometry.CoverPhotoId);
    }

    /// <summary>The city with the most days, or the state, or the country, when no photo has a city.</summary>
    private static string? MainPlace(IReadOnlyList<PhotoPoint> away)
    {
        foreach (var place in new Func<PhotoPoint, string?>[] { photo => photo.City, photo => photo.State, photo => photo.Country })
        {
            var ranked = RankPlaces(away, photo => photo.LocalDay, place);
            if (ranked.Count > 0)
            {
                return ranked[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Ranks places by the number of distinct days with a photo there, most first. A day with photos in
    /// two places counts for both. A tie goes to the place with more photos, then to the one whose
    /// first photo comes earlier, so the photos must be in time order. Photos with no place are
    /// skipped. This is the main destination rule of docs/specs/main-destination.md.
    /// </summary>
    public static IReadOnlyList<string> RankPlaces<T>(IEnumerable<T> photos, Func<T, DateOnly> day, Func<T, string?> place)
    {
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(day);
        ArgumentNullException.ThrowIfNull(place);
        return [.. photos
            .Select((photo, index) => (Place: place(photo), Day: day(photo), Index: index))
            .Where(item => !string.IsNullOrWhiteSpace(item.Place))
            .GroupBy(item => item.Place!, StringComparer.Ordinal)
            .OrderByDescending(group => group.Select(item => item.Day).Distinct().Count())
            .ThenByDescending(group => group.Count())
            .ThenBy(group => group.Min(item => item.Index))
            .Select(group => group.Key)];
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
