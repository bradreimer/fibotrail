namespace Fibotrail.Detection;

/// <summary>
/// A trip you entered by hand, for photos that carry no position. See docs/specs/trip-detection.md.
/// </summary>
/// <param name="Start">The first day, in the local time of the photos.</param>
/// <param name="End">The last day, included.</param>
/// <param name="Name">The name you gave the trip. Null gives the name that detection would suggest.</param>
/// <param name="Place">The main place, such as "San Francisco". Null when you gave none.</param>
/// <param name="Country">The country you named. Null takes the country that the photos name, if any.</param>
/// <param name="Position">Where the trip was. It becomes the one stop when no photo in the range has a position.</param>
public sealed record ManualTripSpec(DateOnly Start, DateOnly End, string? Name, string? Place, string? Country, GeoPoint? Position);

/// <summary>Builds the trip that a <see cref="ManualTripSpec"/> describes. It's a pure function, like every rule.</summary>
public static class ManualTrips
{
    /// <summary>
    /// Takes every photo whose local day falls in the range, with or without a position, and returns
    /// the trip. When some photos have a position, the stops come from them, the way rule 6 makes
    /// them. When none has, the trip has one stop at <see cref="ManualTripSpec.Position"/>, or no
    /// stop when you gave none. The confidence is High, because you decided it. Returns null when no
    /// photo falls in the range.
    /// </summary>
    public static DetectedTrip? Build(IEnumerable<PhotoPoint> photos, ManualTripSpec spec, GeoPoint home, DetectionSettings? settings = null, IReadOnlySet<string>? onTheWay = null)
    {
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(spec);
        settings ??= DetectionSettings.Default;

        var inRange = photos
            .Where(photo => photo.LocalDay >= spec.Start && photo.LocalDay <= spec.End)
            .OrderBy(photo => photo.LocalTime)
            .ThenBy(photo => photo.Id, StringComparer.Ordinal)
            .ToList();
        if (inRange.Count == 0)
        {
            return null;
        }

        var ids = inRange.Select(photo => photo.Id).ToList();
        var placed = inRange.Where(photo => photo.Position is not null).ToList();
        var geometry = FindGeometry(placed, ids, spec, home, settings, onTheWay, inRange);

        var place = string.IsNullOrWhiteSpace(spec.Place) ? null : spec.Place.Trim();
        var country = string.IsNullOrWhiteSpace(spec.Country) ? TripDetector.MostCommon(inRange, photo => photo.Country) : spec.Country.Trim();
        var name = string.IsNullOrWhiteSpace(spec.Name) ? TripDetector.Name(place, spec.Start) : spec.Name.Trim();
        return new DetectedTrip(
            TripDetector.Fingerprint(spec.Start, place),
            name,
            place,
            country,
            spec.Start,
            spec.End,
            ids,
            placed.Count,
            geometry.Stops,
            geometry.DistanceKm,
            geometry.FarthestFromHomeKm,
            Confidence.High,
            geometry.CoverPhotoId);
    }

    private static TripGeometry FindGeometry(
        List<PhotoPoint> placed,
        List<string> ids,
        ManualTripSpec spec,
        GeoPoint home,
        DetectionSettings settings,
        IReadOnlySet<string>? onTheWay,
        List<PhotoPoint> inRange)
    {
        if (placed.Count > 0 || spec.Position is not { } position)
        {
            return TripDetector.Measure(placed, ids, home, settings, onTheWay);
        }

        // No photo says where it was, so the one stop is the position you gave. It holds every photo.
        var cover = inRange.FirstOrDefault(photo => !photo.IsVideo)?.Id ?? ids[0];
        var stop = new TripStop(
            position,
            inRange[0].LocalTime,
            inRange[^1].LocalTime,
            string.IsNullOrWhiteSpace(spec.Place) ? null : spec.Place.Trim(),
            ids);
        return new TripGeometry([stop], 0, Geo.DistanceKm(home, position), cover);
    }
}
