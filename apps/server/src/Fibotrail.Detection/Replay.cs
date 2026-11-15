namespace Fibotrail.Detection;

/// <summary>A trip you know you took, for the replay test.</summary>
public sealed record KnownTrip(string Name, DateOnly Start, DateOnly End);

/// <summary>How the detected trips compare with a list of known trips.</summary>
/// <param name="Matched">Each known trip that was found, with the detected trip that matches it.</param>
/// <param name="Missed">Known trips that no detected trip matches.</param>
/// <param name="Extra">Detected trips that match no known trip.</param>
public sealed record ReplayResult(
    IReadOnlyList<(KnownTrip Known, DetectedTrip Detected)> Matched,
    IReadOnlyList<KnownTrip> Missed,
    IReadOnlyList<DetectedTrip> Extra)
{
    /// <summary>The share of known trips that must be matched to pass the gate of MVP step 3a.</summary>
    public const double Gate = 0.8;

    /// <summary>True when at least 8 of every 10 known trips were matched.</summary>
    public bool PassesGate => Matched.Count + Missed.Count > 0 && Matched.Count >= Gate * (Matched.Count + Missed.Count);
}

/// <summary>Compares what the detector found with what you know.</summary>
public static class Replay
{
    /// <summary>How far a detected start or end may be from the known one, in days.</summary>
    public const int ToleranceDays = 1;

    /// <summary>
    /// A known trip is matched when a detected trip starts and ends within one day of it. Each
    /// detected trip matches at most one known trip.
    /// </summary>
    public static ReplayResult Compare(IEnumerable<KnownTrip> known, IEnumerable<DetectedTrip> detected)
    {
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(detected);

        var unused = detected.OrderBy(trip => trip.StartDate).ToList();
        var matched = new List<(KnownTrip, DetectedTrip)>();
        var missed = new List<KnownTrip>();
        foreach (var trip in known.OrderBy(trip => trip.Start))
        {
            var match = unused.FirstOrDefault(candidate =>
                Math.Abs(candidate.StartDate.DayNumber - trip.Start.DayNumber) <= ToleranceDays
                && Math.Abs(candidate.EndDate.DayNumber - trip.End.DayNumber) <= ToleranceDays);
            if (match is null)
            {
                missed.Add(trip);
            }
            else
            {
                unused.Remove(match);
                matched.Add((trip, match));
            }
        }

        return new ReplayResult(matched, missed, unused);
    }
}
