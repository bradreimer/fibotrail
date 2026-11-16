using System.Globalization;
using System.Text.Json;
using Fibotrail.Detection;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Cli;

/// <summary>
/// <c>fibotrail stops</c>. It measures the candidate on-the-way rule from docs/specs/on-the-way.md
/// against the stops stored with each added trip, so the rule can be tuned on a real library before
/// anything is built on it. It changes nothing in the database. The output holds trip names, place
/// names, counts, and times only: no positions, no photo IDs, and no Immich key, so it can be pasted
/// into a conversation.
/// </summary>
public static class StopsCommand
{
    public const string Name = "stops";

    public static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        IConsoleIo console,
        FibotrailDbContext db,
        ServerSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(settings);

        if (arguments.Count > 0)
        {
            console.WriteLine("This command takes no arguments.");
            return 2;
        }

        if (!File.Exists(settings.DatabasePath))
        {
            console.WriteLine($"No database at {settings.DatabasePath}. Start Fibotrail and scan your library, then run this command again.");
            return 1;
        }

        // Only the columns the report needs. Photos and decisions aren't read.
        var trips = (await db.Trips
            .AsNoTracking()
            .Where(trip => trip.Status == TripStatus.Added)
            .Select(trip => new { trip.Id, trip.Name, trip.StartDate, trip.EndDate, trip.StopsJson })
            .ToListAsync(cancellationToken))
            .OrderBy(trip => trip.StartDate)
            .ThenBy(trip => trip.Id)
            .ToList();

        if (trips.Count == 0)
        {
            console.WriteLine("No trips are added yet. Add trips from the inbox in Fibotrail, then run this command again.");
            return 1;
        }

        var rule = OnTheWaySettings.Default;
        console.WriteLine($"The candidate rule flags a stop that is the first or last of its trip with fewer than {rule.FewPhotos} photos, or whose photos span less than {Hours(rule.ShortSpan)}.");
        console.WriteLine(string.Empty);

        var stopTotal = 0;
        var flagged = 0;
        var edgeFew = 0;
        var shortSpan = 0;
        var both = 0;
        var unreadable = 0;

        foreach (var trip in trips)
        {
            console.WriteLine($"{trip.Name}, {Date(trip.StartDate)} to {Date(trip.EndDate)}");

            IReadOnlyList<StoredStop> stored;
            try
            {
                stored = DetectionService.ReadStops(trip.StopsJson);
            }
            catch (JsonException)
            {
                unreadable++;
                console.WriteLine("  Its stored stops couldn't be read.");
                continue;
            }

            if (stored.Count == 0)
            {
                console.WriteLine("  No stops.");
                continue;
            }

            var assessments = OnTheWayRule.Assess([.. stored.Select(stop => new StopSummary(stop.Place, stop.FirstTime, stop.LastTime, stop.PhotoCount))], rule);
            foreach (var assessment in assessments)
            {
                console.WriteLine($"  {Describe(assessment, assessments.Count)}");
                stopTotal++;
                if (!assessment.IsFlagged)
                {
                    continue;
                }

                flagged++;
                edgeFew += assessment.IsEdgeStopWithFewPhotos ? 1 : 0;
                shortSpan += assessment.HasShortSpan ? 1 : 0;
                both += assessment.IsEdgeStopWithFewPhotos && assessment.HasShortSpan ? 1 : 0;
            }
        }

        console.WriteLine(string.Empty);
        console.WriteLine($"Trips: {trips.Count}");
        console.WriteLine($"Stops: {stopTotal}");
        console.WriteLine($"Flagged stops: {flagged}");
        console.WriteLine($"  First or last with fewer than {rule.FewPhotos} photos: {edgeFew}");
        console.WriteLine($"  Photos span less than {Hours(rule.ShortSpan)}: {shortSpan}");
        console.WriteLine($"  Both: {both}");
        if (unreadable > 0)
        {
            console.WriteLine($"Trips whose stops couldn't be read: {unreadable}");
        }

        return 0;
    }

    private static string Describe(StopAssessment assessment, int stopCount)
    {
        var place = string.IsNullOrWhiteSpace(assessment.Stop.Place) ? "(no place name)" : assessment.Stop.Place;
        var where = (assessment.Position == 1, assessment.Position == stopCount) switch
        {
            (true, true) => "only stop",
            (true, false) => "first",
            (false, true) => "last",
            _ => "middle",
        };

        var reasons = new List<string>();
        if (assessment.IsEdgeStopWithFewPhotos)
        {
            reasons.Add("first or last with few photos");
        }

        if (assessment.HasShortSpan)
        {
            reasons.Add("short span");
        }

        var verdict = assessment.IsFlagged ? $"FLAGGED: {string.Join(" and ", reasons)}" : "not flagged";
        var photos = assessment.Stop.PhotoCount == 1 ? "1 photo" : $"{assessment.Stop.PhotoCount.ToString(CultureInfo.InvariantCulture)} photos";
        return $"{assessment.Position.ToString(CultureInfo.InvariantCulture)}. {place} ({where}), {photos}, {Span(assessment.Stop.Span)}, {verdict}";
    }

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Hours(TimeSpan span) =>
        string.Create(CultureInfo.InvariantCulture, $"{span.TotalHours:0.##} hours");

    /// <summary>A span, rounded to the minute, as "2 d 4 h" from a day up and as "3 h 05 min" below that.</summary>
    private static string Span(TimeSpan span)
    {
        var rounded = TimeSpan.FromMinutes(Math.Round(span.TotalMinutes));
        return rounded.TotalDays >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)rounded.TotalDays} d {rounded.Hours} h")
            : string.Create(CultureInfo.InvariantCulture, $"{rounded.Hours} h {rounded.Minutes:00} min");
    }
}
