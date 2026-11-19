using System.Data.Common;
using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fibotrail.Server.Tests;

/// <summary>
/// Stops trip detection right before its transaction commits, the last moment a crash can still
/// lose its work. Armed, it either waits until the server stops or throws.
/// </summary>
public sealed class DetectionCommitStop : DbTransactionInterceptor
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool Armed { get; set; }

    public bool Throw { get; set; }

    /// <summary>Completes when detection is about to commit.</summary>
    public Task Reached => _reached.Task;

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        // Only detection's transaction holds trips. The scan's own saves don't.
        if (Armed && eventData.Context?.ChangeTracker.Entries<Trip>().Any() == true)
        {
            Armed = false;
            _reached.TrySetResult();
            if (Throw)
            {
                throw new InvalidOperationException("A made-up bug in detection.");
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        return result;
    }
}

public class DetectionRestartTests
{
    private sealed record TripRow(int Id, string Fingerprint, string Name, DateOnly StartDate, int PhotoCount, string Photos);

    private static async Task<List<TripRow>> TripRowsAsync(TestApp app) =>
        [.. (await TripLibrary.TripsAsync(app)).Select(trip => new TripRow(
            trip.Id,
            trip.Fingerprint,
            trip.Name,
            trip.StartDate,
            trip.PhotoCount,
            string.Join(',', trip.Photos.Select(photo => photo.PhotoId).Order(StringComparer.Ordinal))))];

    private static Task<ScanRun> LatestRunAsync(TestApp app) =>
        app.WithDbAsync(db => db.ScanRuns.AsNoTracking().OrderByDescending(run => run.Id).FirstAsync());

    private static Task<string?> IntegrityAsync(TestApp app) =>
        app.WithDbAsync(async db =>
        {
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check";
            return (string?)await command.ExecuteScalarAsync();
        });

    /// <summary>One trip to Orlando is stored. A trip to Lisbon waits in Immich for the next scan.</summary>
    private static async Task<TestApp> StartWithOneTripAsync(DetectionCommitStop stop)
    {
        var app = await TestApp.StartAsync(interceptors: [stop]);
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 2, 1, 9, 0, 0), days: 4, photos: 40));
        await TripLibrary.ScanAndConfirmHomeAsync(app);
        Assert.Single(await TripLibrary.TripsAsync(app));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 10, 9, 0, 0), days: 5, photos: 50, latitude: 38.72, longitude: -9.14, city: "Lisbon", state: "Lisbon", country: "Portugal"));
        return app;
    }

    [Fact]
    public async Task A_crash_right_before_detection_commits_leaves_the_trips_intact_and_the_next_start_detects_again()
    {
        var stop = new DetectionCommitStop();
        var first = await StartWithOneTripAsync(stop);
        var before = await TripRowsAsync(first);
        var browser = await first.SignedInBrowserAsync();

        stop.Armed = true;
        Assert.True(first.Scans.TryStart());
        await stop.Reached;
        var during = await browser.GetJsonAsync("/api/scan");

        // A kill leaves the files as they are now: the commit hasn't happened. The shared-memory
        // index isn't copied, because SQLite rebuilds it from the WAL file after a crash.
        var crashed = Directory.CreateTempSubdirectory("fibotrail-crash-").FullName;
        foreach (var suffix in new[] { "", "-wal" })
        {
            if (File.Exists(first.Settings.DatabasePath + suffix))
            {
                File.Copy(first.Settings.DatabasePath + suffix, Path.Combine(crashed, "fibotrail.db" + suffix));
            }
        }

        var immich = first.Immich;
        await first.DisposeAsync();

        await using var second = await TestApp.StartAsync(crashed, immich);
        Assert.Equal("ok", await IntegrityAsync(second));
        Assert.Equal(before, await TripRowsAsync(second));
        Assert.Equal(ScanState.Detecting, (await LatestRunAsync(second)).State);
        Assert.Equal("running", during.GetProperty("state").GetString());

        var searchesBefore = immich.SearchCount;
        await second.App.StartImmichAsync();
        await second.Scans.Current;

        var after = await TripLibrary.TripsAsync(second);
        Assert.Equal(["Orlando", "Lisbon"], after.Select(trip => trip.MainPlace));
        Assert.Equal(ScanState.Completed, (await LatestRunAsync(second)).State);
        // One search checks the connection. No page is read again: only detection was left.
        Assert.Equal(searchesBefore + 1, immich.SearchCount);
    }

    [Fact]
    public async Task A_restart_during_detection_runs_detection_again_at_the_next_start()
    {
        var stop = new DetectionCommitStop();
        var first = await StartWithOneTripAsync(stop);
        stop.Armed = true;
        Assert.True(first.Scans.TryStart());
        await stop.Reached;
        var interrupted = first.Scans.Current;
        await first.StopAsync();
        await interrupted;

        await using var second = await TestApp.StartAsync(first.Settings.DataDirectory, first.Immich);
        Assert.Equal(ScanState.Detecting, (await LatestRunAsync(second)).State);
        Assert.Single(await TripLibrary.TripsAsync(second));
        await second.App.StartImmichAsync();
        await second.Scans.Current;

        Assert.Equal(2, (await TripLibrary.TripsAsync(second)).Count);
        Assert.Equal(ScanState.Completed, (await LatestRunAsync(second)).State);
    }

    [Fact]
    public async Task A_failure_in_detection_fails_the_run_keeps_the_trips_and_the_next_scan_finishes()
    {
        var stop = new DetectionCommitStop { Throw = true };
        await using var app = await StartWithOneTripAsync(stop);
        var before = await TripRowsAsync(app);

        stop.Armed = true;
        await app.ScanAsync();

        var failed = await LatestRunAsync(app);
        Assert.Equal(ScanState.Failed, failed.State);
        Assert.Equal(ScanService.DetectionFailedMessage, failed.Message);
        Assert.Equal(before, await TripRowsAsync(app));

        await app.ScanAsync();
        Assert.Equal(2, (await TripLibrary.TripsAsync(app)).Count);
        Assert.Equal(ScanState.Completed, (await LatestRunAsync(app)).State);
    }
}
