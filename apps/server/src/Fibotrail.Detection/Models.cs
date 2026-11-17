namespace Fibotrail.Detection;

/// <summary>A position in decimal degrees.</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>What detection needs to know about one photo. "Photo" covers images and videos.</summary>
/// <param name="Id">The Immich asset ID.</param>
/// <param name="LocalTime">The wall-clock time at the place of capture, with no zone.</param>
public sealed record PhotoPoint(
    string Id,
    DateTime LocalTime,
    double? Latitude = null,
    double? Longitude = null,
    string? City = null,
    string? State = null,
    string? Country = null,
    bool IsVideo = false)
{
    public GeoPoint? Position => Latitude is { } latitude && Longitude is { } longitude ? new GeoPoint(latitude, longitude) : null;

    /// <summary>The calendar day of capture, in the local time of the photo.</summary>
    public DateOnly LocalDay => DateOnly.FromDateTime(LocalTime);
}

/// <summary>A run of calendar days, first and last day included.</summary>
public readonly record struct DateRange(DateOnly Start, DateOnly End);

/// <summary>The detection rules that can be tuned. The defaults are the ones in the trip detection spec.</summary>
public sealed record DetectionSettings
{
    public static DetectionSettings Default { get; } = new();

    /// <summary>Photos closer to home than this count as home.</summary>
    public double HomeRadiusKm { get; init; } = 50;

    /// <summary>A pause this long between away photos ends a trip.</summary>
    public TimeSpan GapLimit { get; init; } = TimeSpan.FromHours(72);

    /// <summary>Fewer away photos than this isn't a trip.</summary>
    public int MinimumPhotos { get; init; } = 20;

    /// <summary>A trip covers at least this many calendar days.</summary>
    public int MinimumDays { get; init; } = 2;

    /// <summary>Photos this close together form one stop.</summary>
    public double StopRadiusKm { get; init; } = 1;

    /// <summary>A run of at most this many photos at home, between two away photos, doesn't end a trip.</summary>
    public int HomeBlipPhotos { get; init; } = 2;

    /// <summary>A blip at home only bridges away photos that are no more than this far apart in time.</summary>
    public TimeSpan HomeBlipSpan { get; init; } = TimeSpan.FromHours(24);

    /// <summary>A day with at least this many photos that have no position counts as a burst day.</summary>
    public int BurstPhotosPerDay { get; init; } = 30;

    /// <summary>Consecutive burst days that make a trip when no photo says where they were taken.</summary>
    public int BurstMinimumDays { get; init; } = 3;

    /// <summary>A day next to a burst joins it with at least this many photos with no position. Travel days are lighter.</summary>
    public int BurstEdgePhotosPerDay { get; init; } = 10;

    /// <summary>The most days that can join each end of a burst.</summary>
    public int BurstEdgeDays { get; init; } = 2;

    /// <summary>Photos farther from home than this can form a trip with fewer photos and days. See rule 4b.</summary>
    public double FarDistanceKm { get; init; } = 1000;

    /// <summary>A pause this long between far photos ends a far visit.</summary>
    public TimeSpan FarGapLimit { get; init; } = TimeSpan.FromDays(7);

    /// <summary>A far visit needs at least this many photos in all, counting photos with no position.</summary>
    public int FarMinimumPhotos { get; init; } = 2;
}

public enum Confidence
{
    Low,
    Medium,
    High,
}

/// <summary>A place where a group of a trip's photos were taken.</summary>
/// <param name="Center">The average position of the photos.</param>
/// <param name="Place">The city named most often by the photos, or the state or country.</param>
/// <param name="PhotoIds">The photos taken here, oldest first.</param>
public sealed record TripStop(GeoPoint Center, DateTime FirstTime, DateTime LastTime, string? Place, IReadOnlyList<string> PhotoIds);

/// <summary>What rules 6 to 8 make of a trip's photos.</summary>
/// <param name="Stops">The stops, in the order they were first visited.</param>
/// <param name="DistanceKm">The sum of the straight lines between consecutive stops.</param>
/// <param name="FarthestFromHomeKm">The greatest distance from home to a stop. 0 when the trip has no stop.</param>
/// <param name="CoverPhotoId">The suggested cover photo.</param>
public sealed record TripGeometry(IReadOnlyList<TripStop> Stops, double DistanceKm, double FarthestFromHomeKm, string CoverPhotoId);

/// <summary>A trip that the rules found.</summary>
/// <param name="Fingerprint">A stable hash of the start date and the main place.</param>
/// <param name="Name">The suggested name, such as "Orlando, March 2027".</param>
/// <param name="MainPlace">The city with the most photos, or the state or country when no city is known.</param>
/// <param name="Country">The country with the most photos.</param>
/// <param name="StartDate">The local day of the first away photo.</param>
/// <param name="EndDate">The local day of the last away photo.</param>
/// <param name="PhotoIds">Every photo of the trip, oldest first, including photos with no position.</param>
/// <param name="AwayPhotoCount">Photos with a position away from home. The minimum photos rule counts these.</param>
/// <param name="Stops">The stops, in the order they were first visited. The route connects them in this order.</param>
/// <param name="DistanceKm">The sum of the straight lines between consecutive stops.</param>
/// <param name="FarthestFromHomeKm">The greatest distance from home to a stop.</param>
/// <param name="CoverPhotoId">The suggested cover photo.</param>
public sealed record DetectedTrip(
    string Fingerprint,
    string Name,
    string? MainPlace,
    string? Country,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<string> PhotoIds,
    int AwayPhotoCount,
    IReadOnlyList<TripStop> Stops,
    double DistanceKm,
    double FarthestFromHomeKm,
    Confidence Confidence,
    string CoverPhotoId)
{
    /// <summary>Calendar days from the first day to the last, counting both.</summary>
    public int Days => EndDate.DayNumber - StartDate.DayNumber + 1;
}
