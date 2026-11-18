namespace Fibotrail.Server.Data;

/// <summary>The one password. The table holds at most one row, with Id 1.</summary>
public sealed class Credential
{
    public const int SingleId = 1;

    public int Id { get; set; } = SingleId;

    /// <summary>An Argon2id hash in PHC string format. Never the password itself.</summary>
    public required string PasswordHash { get; set; }

    public DateTime UpdatedAt { get; set; }
}

/// <summary>A signed-in browser. The cookie holds the token, and the table holds only its hash.</summary>
public sealed class Session
{
    public int Id { get; set; }

    public required string TokenHash { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastUsedAt { get; set; }
}

/// <summary>One failed sign-in attempt, kept for the sign-in delay.</summary>
public sealed class SignInFailure
{
    public int Id { get; set; }

    public required string Address { get; set; }

    public DateTime OccurredAt { get; set; }
}

/// <summary>
/// What Fibotrail keeps about one Immich asset: when and where it was taken, and which version of its
/// thumbnail Immich has. "Photo" covers images and videos. Nothing else about the asset is stored. See docs/specs/immich-scanning.md.
/// </summary>
public sealed class Photo
{
    /// <summary>The Immich asset ID, in lowercase with hyphens.</summary>
    public required string Id { get; set; }

    /// <summary>The wall-clock time at the place of capture. It has no zone.</summary>
    public DateTime LocalTime { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Country { get; set; }

    /// <summary>When Immich last changed its record of the asset, in UTC.</summary>
    public DateTime ImmichUpdatedAt { get; set; }

    public bool IsVideo { get; set; }

    /// <summary>The length of a video, in milliseconds.</summary>
    public int? DurationMs { get; set; }

    /// <summary>
    /// Immich's hash of the thumbnail. It changes when Immich makes a new thumbnail, such as after a
    /// rotation, so the image cache knows to fetch again.
    /// </summary>
    public string? Thumbhash { get; set; }
}

public enum ScanState
{
    /// <summary>The scan is reading pages, or it was interrupted and resumes from its cursor.</summary>
    Running,

    Completed,

    /// <summary>Immich stopped answering. The next scan resumes from the cursor.</summary>
    Failed,
}

/// <summary>One scan of the Immich library, with the cursor that lets it resume.</summary>
public sealed class ScanRun
{
    public int Id { get; set; }

    public ScanState State { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    /// <summary>The cursor of the last page read. Null before the first page and after the last.</summary>
    public string? Cursor { get; set; }

    /// <summary>Assets read so far, including ones that were skipped.</summary>
    public int PhotosSeen { get; set; }

    /// <summary>The size of the library when the scan started. Null for a scan of changes only.</summary>
    public int? Total { get; set; }

    /// <summary>When set, the scan reads only assets that Immich changed after this time, in UTC.</summary>
    public DateTime? ChangedSince { get; set; }

    /// <summary>The latest Immich update time in the photo table when the scan completed, in UTC.</summary>
    public DateTime? LatestUpdatedAt { get; set; }

    /// <summary>Why the scan failed, in plain words.</summary>
    public string? Message { get; set; }
}

public enum TripStatus
{
    /// <summary>Detected, and waiting for you in the inbox.</summary>
    Suggested,

    /// <summary>You chose Add trip. The trip is in your library.</summary>
    Added,

    /// <summary>You chose Not a trip. It stays hidden, however many photos it gains.</summary>
    Ignored,

    /// <summary>You chose Later. It stays in the inbox, and it's left out of the badge count.</summary>
    Later,
}

public enum TripConfidence
{
    Low,
    Medium,
    High,
}

/// <summary>
/// A trip that detection found. Everything here except <see cref="Status"/>, <see cref="Name"/>,
/// and <see cref="CoverPhotoId"/> is detected data, rebuilt by every detection run. Those three
/// are copies of your decision, kept here so that a list of trips needs one table. See ADR-002.
/// </summary>
public sealed class Trip
{
    public int Id { get; set; }

    /// <summary>A hash of the start date and the main place. It lets a rescan recognize the trip.</summary>
    public required string Fingerprint { get; set; }

    public TripStatus Status { get; set; }

    /// <summary>The name you gave the trip, or the suggested name.</summary>
    public required string Name { get; set; }

    /// <summary>The name detection suggests, such as "Orlando, March 2027".</summary>
    public required string SuggestedName { get; set; }

    /// <summary>The cover you chose, or the suggested cover.</summary>
    public required string CoverPhotoId { get; set; }

    public required string SuggestedCoverPhotoId { get; set; }

    /// <summary>The city with the most photos, or the state or country when no city is known.</summary>
    public string? MainPlace { get; set; }

    public string? Country { get; set; }

    /// <summary>The local day of the first photo away from home.</summary>
    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int Days { get; set; }

    public int PhotoCount { get; set; }

    /// <summary>The stops in the order of the route, as JSON. See <see cref="Trips.StoredStop"/>.</summary>
    public required string StopsJson { get; set; }

    public double DistanceKm { get; set; }

    public double FarthestFromHomeKm { get; set; }

    public TripConfidence Confidence { get; set; }

    public List<TripPhoto> Photos { get; } = [];
}

/// <summary>One photo of a trip.</summary>
public sealed class TripPhoto
{
    public int TripId { get; set; }

    public required string PhotoId { get; set; }

    /// <summary>The stop where the photo was taken. Null for a photo with no position.</summary>
    public int? StopIndex { get; set; }
}

/// <summary>
/// What you decided about a trip. Detection never changes or deletes a row here, except to follow
/// a trip whose fingerprint changed. The dates let a decision find its trip again when that happens.
/// </summary>
public sealed class Decision
{
    public int Id { get; set; }

    public required string Fingerprint { get; set; }

    /// <summary>Added, Ignored, or Later. Null when you only renamed the trip or chose a cover.</summary>
    public TripStatus? Status { get; set; }

    /// <summary>The name you gave the trip.</summary>
    public string? Name { get; set; }

    /// <summary>The cover photo you chose.</summary>
    public string? CoverPhotoId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    /// <summary>
    /// The parks you marked as visited on this trip, as place ids joined by commas. Null when you
    /// marked none. For trips whose photos have no position. See docs/specs/collections.md.
    /// </summary>
    public string? VisitedParks { get; set; }

    public DateTime DecidedAt { get; set; }
}

/// <summary>
/// A join you chose: the trips that touch these days are one trip, even when no photo was taken for
/// longer than the gap limit. It's a decision, so a rescan keeps it, and removing it splits the trips
/// again. Detection reads this table and never changes it. See docs/specs/trip-detection.md and ADR-016.
/// </summary>
public sealed class TripJoin
{
    public int Id { get; set; }

    /// <summary>The first day of the joined trip.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>The last day of the joined trip.</summary>
    public DateOnly EndDate { get; set; }

    public DateTime DecidedAt { get; set; }
}

/// <summary>
/// A trip you entered by hand with <c>fibotrail trip add</c>, for photos that carry no position.
/// It's a decision, so detection reads this table and never changes it. Detection builds the trip
/// from the photos in the days, and the trip wins over any detected trip that covers the same days.
/// See docs/specs/trip-detection.md and ADR-017.
/// </summary>
public sealed class ManualTrip
{
    public int Id { get; set; }

    /// <summary>The name you gave the trip. Null gives the name that detection would suggest.</summary>
    public string? Name { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    /// <summary>The main place, such as "San Francisco".</summary>
    public string? Place { get; set; }

    public string? Country { get; set; }

    /// <summary>Where the trip was. It becomes the one stop when no photo in the days has a position.</summary>
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public DateTime DecidedAt { get; set; }
}

/// <summary>
/// A photo you put on the way: it stays in its trip, but it adds no stop, line, or distance to the
/// map. A row here is a decision, keyed by the Immich asset ID, so it belongs to the photo and not to
/// a trip. Detection reads this table and never changes it. See docs/specs/on-the-way.md and ADR-002.
/// </summary>
public sealed class OnTheWayPhoto
{
    public required string PhotoId { get; set; }

    public DateTime DecidedAt { get; set; }
}

/// <summary>
/// A photo you pinned to a trip, to repair it. The row names a day inside the trip, and detection
/// reads the photo as taken on that day, with its own time of day and position. The row is a decision,
/// keyed by the Immich asset ID. Detection reads this table and never changes it. See ADR-021 and ADR-002.
/// </summary>
public sealed class PinnedPhoto
{
    public required string PhotoId { get; set; }

    /// <summary>The day the photo counts as taken. It is a day of the trip you pinned it to.</summary>
    public DateOnly Day { get; set; }

    public DateTime DecidedAt { get; set; }
}

/// <summary>One setting, such as the home location.</summary>
public sealed class Setting
{
    public required string Key { get; set; }

    public required string Value { get; set; }
}
