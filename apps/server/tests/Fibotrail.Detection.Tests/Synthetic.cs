namespace Fibotrail.Detection.Tests;

/// <summary>Builds small made-up photo sets. Every place and position is invented or approximate.</summary>
public static class Synthetic
{
    /// <summary>Home, for every test.</summary>
    public static readonly GeoPoint Home = new(49.25, -123.1);

    /// <summary>About 200 km east of home.</summary>
    public static readonly GeoPoint Lake = new(49.25, -120.35);

    /// <summary>About 3,300 km from home.</summary>
    public static readonly GeoPoint Park = new(28.4, -81.55);

    private static int _next;

    /// <summary>Photos at one place, spread evenly from the first time to the last.</summary>
    public static List<PhotoPoint> At(GeoPoint? place, int count, DateTime first, DateTime last, string? city = null, string? state = null, string? country = null)
    {
        var step = count > 1 ? (last - first) / (count - 1) : TimeSpan.Zero;
        return [.. Enumerable.Range(0, count).Select(index => new PhotoPoint(
            $"photo-{Interlocked.Increment(ref _next):D6}",
            first + (step * index),
            place?.Latitude,
            place?.Longitude,
            place is null ? null : city,
            place is null ? null : state,
            place is null ? null : country))];
    }

    /// <summary>A point a number of kilometers north of another.</summary>
    public static GeoPoint North(GeoPoint from, double km) => new(from.Latitude + (km / 111.195), from.Longitude);

    public static DateTime Time(int year, int month, int day, int hour = 12, int minute = 0) => new(year, month, day, hour, minute, 0);
}
