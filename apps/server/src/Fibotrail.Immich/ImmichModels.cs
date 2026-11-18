namespace Fibotrail.Immich;

/// <summary>How to reach Immich. <see cref="ApiKey"/> is a secret: never log it.</summary>
public sealed record ImmichOptions(Uri BaseUrl, string ApiKey, TimeSpan Timeout)
{
    /// <summary>The waits before the second, third, and fourth try of a request.</summary>
    public IReadOnlyList<TimeSpan> RetryDelays { get; init; } =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)];

    /// <summary>Leaves out the key, so a logged options object can't leak it.</summary>
    public override string ToString() => $"ImmichOptions {{ BaseUrl = {BaseUrl}, Timeout = {Timeout} }}";
}

public sealed record ImmichVersion(int Major, int Minor, int Patch)
{
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

/// <summary>The part of an Immich asset that Fibotrail reads. "Photo" covers images and videos.</summary>
/// <param name="Id">The Immich asset ID.</param>
/// <param name="LocalTime">The wall-clock time at the place of capture, with no zone. Null when Immich has none.</param>
/// <param name="UpdatedAt">When Immich last changed its record, in UTC.</param>
/// <param name="DurationMs">The length of a video, in milliseconds.</param>
/// <param name="Thumbhash">
/// A small hash of the thumbnail. Immich makes a new one when it makes a new thumbnail, such as
/// after a rotation, and uses it to bust its own cache. Null when Immich has none yet.
/// </param>
public sealed record ImmichAsset(
    Guid Id,
    DateTime? LocalTime,
    double? Latitude,
    double? Longitude,
    string? City,
    string? State,
    string? Country,
    DateTime UpdatedAt,
    bool IsVideo,
    int? DurationMs,
    bool IsTrashed,
    string? Thumbhash = null);

/// <summary>One page of a search. A null <see cref="NextCursor"/> means it was the last page.</summary>
public sealed record ImmichPage(IReadOnlyList<ImmichAsset> Assets, string? NextCursor);

public enum ImmichImageSize
{
    /// <summary>A small image for grids.</summary>
    Thumbnail,

    /// <summary>A larger image for the photo viewer.</summary>
    Preview,
}

/// <summary>Immich can't be used right now: it didn't answer, or it answered with an error.</summary>
public class ImmichException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Immich didn't answer in time, or failed, on every try.</summary>
public sealed class ImmichUnavailableException(string message, Exception? inner = null) : ImmichException(message, inner);

/// <summary>Immich refused the request: the key is wrong, or it lacks a permission.</summary>
public sealed class ImmichRejectedException(int statusCode, Exception? inner = null)
    : ImmichException($"Immich refused the request with status {statusCode}.", inner)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>Immich has no such asset.</summary>
public sealed class ImmichNotFoundException(Exception? inner = null) : ImmichException("Immich has no such asset.", inner);
