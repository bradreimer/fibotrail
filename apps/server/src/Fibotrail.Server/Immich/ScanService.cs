using Fibotrail.Immich;
using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Immich;

/// <summary>Work that runs after a scan completes, such as trip detection.</summary>
public interface IScanCompletedHandler
{
    Task OnScanCompletedAsync(CancellationToken cancellationToken);
}

/// <summary>How a scan ended. <paramref name="RowsChanged"/> counts photo rows added, changed, or removed.</summary>
public sealed record ScanOutcome(ScanState State, int PhotosSeen, int RowsChanged, string? Message);

/// <summary>
/// Reads the Immich library into the photo table, one page at a time, in the background. One scan
/// runs at a time. The cursor is saved after each page, so a scan that stops resumes where it was.
/// After the last page the run is "detecting" until trip detection commits, so a restart during
/// detection runs it again. See docs/specs/immich-scanning.md.
/// </summary>
public sealed partial class ScanService(
    IServiceScopeFactory scopes,
    IImmichClient immich,
    TimeProvider time,
    IHostApplicationLifetime lifetime,
    ILogger<ScanService> logger)
{
    public const string TimeoutMessage = "Immich didn't answer in time. Your progress is saved.";

    public const string StoppedMessage = "The scan was stopped from the command line. Your progress is saved.";

    public const string DetectionFailedMessage = "Finding trips stopped because of a problem in Fibotrail. Your photos are saved, and your trips are as they were. The log has the details.";

    private readonly Lock _gate = new();
    private Task _current = Task.CompletedTask;

    /// <summary>The scan that is running, or the last one that ran. A test waits on it.</summary>
    public Task Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public bool IsRunning => !Current.IsCompleted;

    /// <summary>
    /// Starts a scan in the background. A full pass rereads every photo, as <c>fibotrail rescan</c> does.
    /// </summary>
    /// <returns>False when a scan is already running.</returns>
    public bool TryStart(bool fullPass = false)
    {
        lock (_gate)
        {
            if (!_current.IsCompleted)
            {
                return false;
            }

            _current = Task.Run(() => RunAsync(fullPass, onPage: null, lifetime.ApplicationStopping));
            return true;
        }
    }

    /// <summary>
    /// Reads every photo from Immich, ignoring the changed-since filter, then runs trip detection. It
    /// backs <c>fibotrail rescan</c>. The caller makes sure no other scan is running.
    /// </summary>
    /// <param name="onPage">Called after each page, with the run as saved.</param>
    public Task<ScanOutcome> RunFullPassAsync(Action<ScanRun>? onPage, CancellationToken cancellationToken) =>
        RunAsync(fullPass: true, onPage, cancellationToken);

    /// <summary>Continues a scan, or the trip detection after it, that the last shutdown interrupted.</summary>
    /// <returns>True when there was one.</returns>
    public async Task<bool> ResumeInterruptedAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FibotrailDbContext>();
        var latest = await db.ScanRuns.OrderByDescending(run => run.Id).FirstOrDefaultAsync(cancellationToken);
        return latest?.State is ScanState.Running or ScanState.Detecting && TryStart();
    }

    private async Task<ScanOutcome> RunAsync(bool fullPass, Action<ScanRun>? onPage, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FibotrailDbContext>();
        ScanRun? run = null;
        var rowsChanged = 0;
        try
        {
            run = await OpenRunAsync(db, fullPass, cancellationToken);
            if (run.State != ScanState.Detecting)
            {
                await ReadPagesAsync(db, run, onPage, count => rowsChanged += count, cancellationToken);
            }

            // Detection commits in its own transaction. Until it does, the run stays "detecting".
            foreach (var handler in scope.ServiceProvider.GetServices<IScanCompletedHandler>())
            {
                await handler.OnScanCompletedAsync(cancellationToken);
            }

            run.State = ScanState.Completed;
            run.FinishedAt = time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The server is stopping. The run stays "running" or "detecting", and the next start resumes it. A
            // command that was stopped has no restart to resume it, so its run is marked failed and the next scan resumes it.
            if (fullPass && !lifetime.ApplicationStopping.IsCancellationRequested)
            {
                await FailAsync(db, run, StoppedMessage);
                return new ScanOutcome(ScanState.Failed, run?.PhotosSeen ?? 0, rowsChanged, StoppedMessage);
            }

            return new ScanOutcome(run?.State ?? ScanState.Running, run?.PhotosSeen ?? 0, rowsChanged, null);
        }
        catch (ImmichException exception)
        {
            var message = exception is ImmichRejectedException ? ImmichConnection.CantReachMessage : TimeoutMessage;
            await FailAsync(db, run, message);
            LogFailed(logger, exception.Message);
            return new ScanOutcome(ScanState.Failed, run?.PhotosSeen ?? 0, rowsChanged, message);
        }
#pragma warning disable CA1031 // A background scan must record any failure, or it would vanish without a trace.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // Detection rolled back, so the trips are as they were. The next scan reads again from the cursor.
            var detecting = run?.State == ScanState.Detecting;
            var message = detecting
                ? DetectionFailedMessage
                : "The scan stopped because of a problem in Fibotrail. Your progress is saved. The log has the details.";
            await FailAsync(db, run, message);
            LogCrashed(logger, exception);
            return new ScanOutcome(ScanState.Failed, run?.PhotosSeen ?? 0, rowsChanged, message);
        }

        return new ScanOutcome(ScanState.Completed, run?.PhotosSeen ?? 0, rowsChanged, null);
    }

    /// <summary>Stores every page that is left, then marks the run "detecting".</summary>
    private async Task ReadPagesAsync(FibotrailDbContext db, ScanRun run, Action<ScanRun>? onPage, Action<int> addRowsChanged, CancellationToken cancellationToken)
    {
        if (run.Cursor is null && run.ChangedSince is null && run.Total is null)
        {
            // The search doesn't say how large the library is. The statistics do.
            run.Total = await immich.GetTotalAsync(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        while (true)
        {
            var page = await immich.SearchAsync(run.Cursor, run.ChangedSince, ImmichClient.MaxPageSize, cancellationToken);
            await StoreAsync(db, page.Assets, cancellationToken);
            addRowsChanged(db.ChangeTracker.Entries<Photo>().Count(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted));
            run.PhotosSeen += page.Assets.Count;
            run.Cursor = page.NextCursor;
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
            db.Attach(run);
            onPage?.Invoke(run);
            if (page.NextCursor is null)
            {
                break;
            }
        }

        run.State = ScanState.Detecting;
        run.LatestUpdatedAt = await db.Photos.MaxAsync(photo => (DateTime?)photo.ImmichUpdatedAt, cancellationToken) ?? run.ChangedSince;
        await db.SaveChangesAsync(cancellationToken);
        LogCompleted(logger, run.PhotosSeen);
    }

    /// <summary>Returns the run to continue, or a new one. A full pass always starts a new run.</summary>
    private async Task<ScanRun> OpenRunAsync(FibotrailDbContext db, bool fullPass, CancellationToken cancellationToken)
    {
        var latest = await db.ScanRuns.OrderByDescending(run => run.Id).FirstOrDefaultAsync(cancellationToken);
        if (latest is { State: ScanState.Detecting } && !fullPass)
        {
            // Every page is stored. Only detection is left.
            return latest;
        }

        if (latest is not null && latest.State != ScanState.Completed && !fullPass)
        {
            // A scan that stopped for any reason resumes from its saved cursor.
            latest.State = ScanState.Running;
            latest.FinishedAt = null;
            latest.Message = null;
            await db.SaveChangesAsync(cancellationToken);
            return latest;
        }

        var run = new ScanRun
        {
            State = ScanState.Running,
            StartedAt = time.GetUtcNow().UtcDateTime,
            // After the first complete scan, a scan reads only what changed since. A full pass reads everything.
            ChangedSince = fullPass ? null : latest?.LatestUpdatedAt,
        };
        db.ScanRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return run;
    }

    /// <summary>Applies one page to the photo table. Rows are keyed by asset ID.</summary>
    private static async Task StoreAsync(FibotrailDbContext db, IReadOnlyList<ImmichAsset> assets, CancellationToken cancellationToken)
    {
        var ids = assets.Select(asset => asset.Id.ToString()).ToList();
        var existing = await db.Photos.Where(photo => ids.Contains(photo.Id)).ToDictionaryAsync(photo => photo.Id, cancellationToken);

        foreach (var asset in assets)
        {
            var id = asset.Id.ToString();
            existing.TryGetValue(id, out var photo);

            // An asset in the trash isn't stored, and an asset with no capture time can't be placed in time.
            if (asset.IsTrashed || asset.LocalTime is null)
            {
                if (photo is not null)
                {
                    db.Photos.Remove(photo);
                    existing.Remove(id);
                }

                continue;
            }

            if (photo is null)
            {
                photo = new Photo { Id = id };
                db.Photos.Add(photo);
                existing[id] = photo;
            }

            // A position of exactly 0, 0 is a missing position. The photo can still join a trip by date.
            var hasPosition = asset.Latitude is not null && asset.Longitude is not null
                && !(asset.Latitude == 0 && asset.Longitude == 0);
            photo.LocalTime = asset.LocalTime.Value;
            photo.Latitude = hasPosition ? asset.Latitude : null;
            photo.Longitude = hasPosition ? asset.Longitude : null;
            photo.City = hasPosition ? asset.City : null;
            photo.State = hasPosition ? asset.State : null;
            photo.Country = hasPosition ? asset.Country : null;
            photo.ImmichUpdatedAt = asset.UpdatedAt;
            photo.IsVideo = asset.IsVideo;
            photo.DurationMs = asset.IsVideo ? asset.DurationMs : null;
            photo.Thumbhash = asset.Thumbhash;
        }
    }

    private async Task FailAsync(FibotrailDbContext db, ScanRun? run, string message)
    {
        if (run is null)
        {
            return;
        }

        // What the failed page changed is dropped. The cursor still points at that page.
        db.ChangeTracker.Clear();
        db.Attach(run);
        run.State = ScanState.Failed;
        run.FinishedAt = time.GetUtcNow().UtcDateTime;
        run.Message = message;
        db.Entry(run).Property(entry => entry.State).IsModified = true;
        db.Entry(run).Property(entry => entry.FinishedAt).IsModified = true;
        db.Entry(run).Property(entry => entry.Message).IsModified = true;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The scan read {Count} photos from Immich. Finding trips is next.")]
    private static partial void LogCompleted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The scan stopped: {Reason}")]
    private static partial void LogFailed(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "The scan stopped because of a problem in Fibotrail.")]
    private static partial void LogCrashed(ILogger logger, Exception exception);
}
