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
/// What Fibotrail keeps about one Immich asset: when and where it was taken. "Photo" covers images
/// and videos. Nothing else about the asset is stored. See docs/specs/immich-scanning.md.
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
