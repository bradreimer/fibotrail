using Fibotrail.Detection;

namespace Fibotrail.Trails;

/// <summary>A photo to place on a trail.</summary>
/// <param name="LocalTime">The wall-clock time where the photo was taken. It has no zone.</param>
/// <param name="Latitude">The photo's own position, when it has one.</param>
public sealed record AlignPhoto(string Id, DateTime LocalTime, double? Latitude = null, double? Longitude = null)
{
    public bool HasPosition => Latitude is not null && Longitude is not null;
}

/// <summary>Where a photo lands on a trail.</summary>
/// <param name="Position">The trail position at the photo's time, or null when the photo is outside the trail.</param>
public sealed record AlignedPhoto(AlignPhoto Photo, TrailPosition? Position)
{
    public bool Outside => Position is null;
}

/// <summary>
/// Places photos on a GPS trail by time. A trail's points are in UTC and a photo's time is local, so
/// a trail has one offset, in minutes: photo local time = trail UTC time + offset. Pure functions.
/// See docs/specs/gps-trails.md#photo-alignment.
/// </summary>
public static class PhotoAlignment
{
    /// <summary>The widest offset from UTC, in minutes: 14 hours.</summary>
    public const int MaxOffsetMinutes = 14 * 60;

    /// <summary>The fewest placed photos an estimate needs.</summary>
    public const int MinPlacedPhotos = 3;

    /// <summary>A placed photo this close to its trail position counts as a match.</summary>
    public const double MatchMeters = 200;

    /// <summary>A best median distance above this gives no estimate.</summary>
    public const double MaxMedianMeters = 500;

    /// <summary>The most placed photos an estimate uses. More are thinned, evenly in time.</summary>
    public const int MaxEstimatePhotos = 500;

    /// <summary>
    /// The trail position at the photo's time, interpolated between the two points either side.
    /// Null when the moment falls in a gap between segments, or before or after the trail.
    /// </summary>
    public static TrailPosition? PositionAt(IReadOnlyList<IReadOnlyList<TrailPoint>> segments, DateTime localTime, int offsetMinutes)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var moment = DateTime.SpecifyKind(localTime.AddMinutes(-offsetMinutes), DateTimeKind.Utc);
        foreach (var segment in segments)
        {
            if (segment.Count == 0 || moment < segment[0].Time || moment > segment[^1].Time)
            {
                continue;
            }

            // The first point at or after the moment.
            var low = 0;
            var high = segment.Count - 1;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (segment[middle].Time < moment)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            var after = segment[low];
            if (after.Time == moment || low == 0)
            {
                return new TrailPosition(after.Latitude, after.Longitude);
            }

            var before = segment[low - 1];
            var span = (after.Time - before.Time).TotalSeconds;
            var share = span <= 0 ? 0 : (moment - before.Time).TotalSeconds / span;
            return new TrailPosition(
                before.Latitude + ((after.Latitude - before.Latitude) * share),
                before.Longitude + ((after.Longitude - before.Longitude) * share));
        }

        return null;
    }

    /// <summary>Places each photo on the trail with the offset, in the order given.</summary>
    public static IReadOnlyList<AlignedPhoto> Align(IEnumerable<AlignPhoto> photos, IReadOnlyList<IReadOnlyList<TrailPoint>> segments, int offsetMinutes)
    {
        ArgumentNullException.ThrowIfNull(photos);
        return [.. photos.Select(photo => new AlignedPhoto(photo, PositionAt(segments, photo.LocalTime, offsetMinutes)))];
    }

    /// <summary>
    /// The offset that puts the photos with a position of their own closest to the trail. Each
    /// offset from −14 h to +14 h is scored by how many placed photos land within 200 m of their
    /// trail position, then by the median distance of the photos that land on the trail, then by
    /// how near the offset is to 0. Null when fewer than 3 photos match, or the median is over 500 m.
    /// A count comes first because a wrong offset can put 3 photos close by chance.
    /// </summary>
    public static int? EstimateOffset(IEnumerable<AlignPhoto> photos, IReadOnlyList<IReadOnlyList<TrailPoint>> segments)
    {
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(segments);
        var placed = photos.Where(photo => photo.HasPosition).OrderBy(photo => photo.LocalTime).ToList();
        if (placed.Count < MinPlacedPhotos)
        {
            return null;
        }

        if (placed.Count > MaxEstimatePhotos)
        {
            var step = placed.Count / (double)MaxEstimatePhotos;
            placed = [.. Enumerable.Range(0, MaxEstimatePhotos).Select(index => placed[(int)(index * step)])];
        }

        int? best = null;
        var bestMatches = 0;
        var bestMedian = double.MaxValue;
        var distances = new List<double>(placed.Count);

        // Nearer 0 first, so a tie keeps the smaller offset.
        foreach (var offset in OffsetsNearestZeroFirst())
        {
            distances.Clear();
            foreach (var photo in placed)
            {
                if (PositionAt(segments, photo.LocalTime, offset) is { } position)
                {
                    distances.Add(Geo.DistanceKm(new GeoPoint(photo.Latitude!.Value, photo.Longitude!.Value), new GeoPoint(position.Latitude, position.Longitude)) * 1000);
                }
            }

            if (distances.Count < MinPlacedPhotos)
            {
                continue;
            }

            var matches = distances.Count(distance => distance <= MatchMeters);
            var median = Median(distances);
            if (matches > bestMatches || (matches == bestMatches && median < bestMedian))
            {
                bestMatches = matches;
                bestMedian = median;
                best = offset;
            }
        }

        return best is not null && bestMatches >= MinPlacedPhotos && bestMedian <= MaxMedianMeters ? best : null;
    }

    /// <summary>0, 1, −1, 2, −2, … up to ±14 hours.</summary>
    private static IEnumerable<int> OffsetsNearestZeroFirst()
    {
        yield return 0;
        for (var minutes = 1; minutes <= MaxOffsetMinutes; minutes++)
        {
            yield return minutes;
            yield return -minutes;
        }
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2;
    }
}
