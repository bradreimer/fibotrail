namespace Fibotrail.Detection;

/// <summary>What the on-the-way rule needs to know about one stop. It holds no position and no photo IDs.</summary>
/// <param name="Place">The city, state, or country that the photos name.</param>
/// <param name="FirstTime">The local time of the first photo at the stop.</param>
/// <param name="LastTime">The local time of the last photo at the stop.</param>
/// <param name="PhotoCount">The photos at the stop.</param>
public sealed record StopSummary(string? Place, DateTime FirstTime, DateTime LastTime, int PhotoCount)
{
    /// <summary>The time from the first photo to the last. It is 0 for a stop with one photo.</summary>
    public TimeSpan Span => LastTime - FirstTime;
}

/// <summary>The thresholds of the candidate rule. The defaults are the ones in docs/specs/on-the-way.md.</summary>
public sealed record OnTheWaySettings
{
    public static OnTheWaySettings Default { get; } = new();

    /// <summary>A first or last stop with fewer photos than this is flagged.</summary>
    public int FewPhotos { get; init; } = 5;

    /// <summary>A stop whose photos span less time than this is flagged.</summary>
    public TimeSpan ShortSpan { get; init; } = TimeSpan.FromHours(3);
}

/// <summary>
/// How one stop measures against the rule. The three facts are kept apart so that a report can
/// show which condition held, and so the thresholds can be tuned one at a time.
/// </summary>
/// <param name="Stop">The stop that was measured.</param>
/// <param name="Position">The place of the stop in the route, starting at 1.</param>
/// <param name="IsFirstOrLast">The stop is the first or the last of the trip. A trip with one stop has a stop that is both.</param>
/// <param name="HasFewPhotos">The stop has fewer photos than <see cref="OnTheWaySettings.FewPhotos"/>.</param>
/// <param name="HasShortSpan">The photos at the stop span less than <see cref="OnTheWaySettings.ShortSpan"/>.</param>
public sealed record StopAssessment(StopSummary Stop, int Position, bool IsFirstOrLast, bool HasFewPhotos, bool HasShortSpan)
{
    /// <summary>The first or last stop with few photos. This is the first half of the rule.</summary>
    public bool IsEdgeStopWithFewPhotos => IsFirstOrLast && HasFewPhotos;

    /// <summary>The rule flags the stop: an edge stop with few photos, or a short span at any position.</summary>
    public bool IsFlagged => IsEdgeStopWithFewPhotos || HasShortSpan;
}

/// <summary>
/// The candidate rule for a stop that is on the way, from docs/specs/on-the-way.md: the stop is the
/// first or last of its trip and has fewer than 5 photos, or its photos span less than 3 hours. It
/// measures only. Nothing applies the result, and it's here to be tuned on real trips first.
/// </summary>
public static class OnTheWayRule
{
    /// <summary>Measures every stop of one trip. The stops are in route order, and the result is in the same order.</summary>
    public static IReadOnlyList<StopAssessment> Assess(IReadOnlyList<StopSummary> stops, OnTheWaySettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(stops);
        settings ??= OnTheWaySettings.Default;

        return [.. stops.Select((stop, index) => new StopAssessment(
            stop,
            index + 1,
            IsFirstOrLast: index == 0 || index == stops.Count - 1,
            HasFewPhotos: stop.PhotoCount < settings.FewPhotos,
            HasShortSpan: stop.Span < settings.ShortSpan))];
    }
}
