using System.Globalization;
using Fibotrail.Immich;
using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Cli;

/// <summary>
/// <c>fibotrail rescan</c>. It rereads every photo from Immich, which a normal scan doesn't do, and then
/// runs trip detection. Use it after changing photo locations in Immich. It only reads from Immich, and
/// your decisions aren't touched. See docs/specs/immich-scanning.md.
/// </summary>
public static class RescanCommand
{
    public const string Name = "rescan";

    public static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        IConsoleIo console,
        FibotrailDbContext db,
        ScanService scans,
        ServerSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(scans);
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

        var latest = await db.ScanRuns.AsNoTracking().OrderByDescending(run => run.Id).FirstOrDefaultAsync(cancellationToken);
        if (latest?.State is ScanState.Running or ScanState.Detecting)
        {
            console.WriteLine("A scan is running. Wait for the banner in Fibotrail to finish, then run this command again. If no scan is running, restart the container, which resumes it.");
            return 1;
        }

        var suggestionsBefore = await CountSuggestionsAsync(db, cancellationToken);
        console.WriteLine("Rereading every photo from Immich. This takes as long as a first scan.");

        var lastPrinted = 0;
        var outcome = await scans.RunFullPassAsync(
            run =>
            {
                // One line for about every 5,000 photos keeps a long scan readable.
                if (run.PhotosSeen - lastPrinted >= 5000 || run.Cursor is null)
                {
                    lastPrinted = run.PhotosSeen;
                    console.WriteLine(run.Total is { } total
                        ? $"Scanning: {run.PhotosSeen.ToString("N0", CultureInfo.InvariantCulture)} of {total.ToString("N0", CultureInfo.InvariantCulture)} photos."
                        : $"Scanning: {run.PhotosSeen.ToString("N0", CultureInfo.InvariantCulture)} photos so far.");
                }
            },
            cancellationToken);

        if (outcome.State != ScanState.Completed)
        {
            console.WriteLine(outcome.Message ?? "The scan didn't finish.");
            return 1;
        }

        var suggestionsAfter = await CountSuggestionsAsync(db, cancellationToken);
        console.WriteLine($"Done. Read {outcome.PhotosSeen.ToString("N0", CultureInfo.InvariantCulture)} photos. {outcome.RowsChanged.ToString("N0", CultureInfo.InvariantCulture)} changed.");
        console.WriteLine(suggestionsAfter > suggestionsBefore
            ? $"{suggestionsAfter - suggestionsBefore} new {(suggestionsAfter - suggestionsBefore == 1 ? "suggestion is" : "suggestions are")} in your inbox."
            : "No new suggestions.");
        return 0;
    }

    private static async Task<int> CountSuggestionsAsync(FibotrailDbContext db, CancellationToken cancellationToken)
    {
        // The command's own context saw no tracked rows, and detection ran in another scope. Read afresh.
        db.ChangeTracker.Clear();
        return await db.Trips.AsNoTracking().CountAsync(trip => trip.Status == TripStatus.Suggested, cancellationToken);
    }
}

/// <summary>A command has no web host to stop, so nothing signals the scan to end.</summary>
internal sealed class CommandLifetime : IHostApplicationLifetime
{
    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => CancellationToken.None;

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication()
    {
    }
}
