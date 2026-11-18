namespace Fibotrail.Detection;

/// <summary>
/// Moves photos you pinned into their trip's days, before the rules run. A pin names a day, and the
/// photo keeps its time of day and its position. Pure: it takes photos and pins, and returns photos.
/// See docs/specs/trip-detection.md and ADR-021.
/// </summary>
public static class PinnedPhotos
{
    /// <summary>
    /// The day a photo moves to: the day of the trip nearest the photo's own day, so a photo that is
    /// already inside the trip stays where it is.
    /// </summary>
    public static DateOnly DayIn(DateOnly photoDay, DateOnly tripStart, DateOnly tripEnd)
    {
        if (tripEnd < tripStart)
        {
            throw new ArgumentException("A trip can't end before it starts.", nameof(tripEnd));
        }

        return photoDay < tripStart ? tripStart : photoDay > tripEnd ? tripEnd : photoDay;
    }

    /// <summary>Returns the photos with each pinned photo moved to its pinned day. Other photos are returned as they are.</summary>
    public static IReadOnlyList<PhotoPoint> Apply(IReadOnlyList<PhotoPoint> photos, IReadOnlyDictionary<string, DateOnly> pins)
    {
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(pins);
        if (pins.Count == 0)
        {
            return photos;
        }

        return [.. photos.Select(photo => pins.TryGetValue(photo.Id, out var day)
            ? photo with { LocalTime = day.ToDateTime(TimeOnly.FromDateTime(photo.LocalTime)) }
            : photo)];
    }
}
