using Fibotrail.Immich;
using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Immich;

/// <summary>Work that runs after a scan completes, such as trip detection.</summary>
public interface IScanCompletedHandler
{
    Task OnScanCompletedAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Reads the Immich library into the photo table, one page at a time, in the background. One scan
/// runs at a time. The cursor is saved after each page, so a scan that stops resumes where it was.
/// See docs/specs/immich-scanning.md.
/// </summary>
public sealed partial class ScanService(
    IServiceScopeFactory scopes,
    IImmichClient immich,
    TimeProvider time,
    IHostApplicationLifetime lifetime,
    ILogger<ScanService> logger)
{
    public const string TimeoutMessage = "Immich didn't answer in time. Your progress is saved.";

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

    /// <summary>Starts a scan in the background.</summary>
    /// <returns>False when a scan is already running.</returns>
    public bool TryStart()
    {
        lock (_gate)
        {
            if (!_current.IsCompleted)
            {
                return false;
            }

            _current = Task.Run(() => RunAsync(lifetime.ApplicationStopping));
            return true;
        }
    }

    /// <summary>Continues a scan that the last shutdown interrupted.</summary>
    /// <returns>True when there was one.</returns>
    public async Task<bool> ResumeInterruptedAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FibotrailDbContext>();
        var latest = await db.ScanRuns.OrderByDescending(run => run.Id).FirstOrDefaultAsync(cancellationToken);
        return latest?.State == ScanState.Running && TryStart();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FibotrailDbContext>();
        ScanRun? run = null;
        try
        {
            run = await OpenRunAsync(db, cancellationToken);
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
                run.PhotosSeen += page.Assets.Count;
                run.Cursor = page.NextCursor;
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
                db.Attach(run);
                if (page.NextCursor is null)
                {
                    break;
                }
            }

            run.State = ScanState.Completed;
            run.FinishedAt = time.GetUtcNow().UtcDateTime;
            run.LatestUpdatedAt = await db.Photos.MaxAsync(photo => (DateTime?)photo.ImmichUpdatedAt, cancellationToken) ?? run.ChangedSince;
            await db.SaveChangesAsync(cancellationToken);
            LogCompleted(logger, run.PhotosSeen);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The server is stopping. The run stays "running", and the next start resumes it.
            return;
        }
        catch (ImmichException exception)
        {
            await FailAsync(db, run, exception is ImmichRejectedException ? ImmichConnection.CantReachMessage : TimeoutMessage);
            LogFailed(logger, exception.Message);
            return;
        }
#pragma warning disable CA1031 // A background scan must record any failure, or it would vanish without a trace.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            await FailAsync(db, run, "The scan stopped because of a problem in Fibotrail. Your progress is saved. The log has the details.");
            LogCrashed(logger, exception);
            return;
        }

        foreach (var handler in scope.ServiceProvider.GetServices<IScanCompletedHandler>())
        {
            await handler.OnScanCompletedAsync(cancellationToken);
        }
    }

    /// <summary>Returns the run to continue, or a new one.</summary>
    private async Task<ScanRun> OpenRunAsync(FibotrailDbContext db, CancellationToken cancellationToken)
    {
        var latest = await db.ScanRuns.OrderByDescending(run => run.Id).FirstOrDefaultAsync(cancellationToken);
        if (latest is not null && latest.State != ScanState.Completed)
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
            // After the first complete scan, a scan reads only what changed since.
            ChangedSince = latest?.LatestUpdatedAt,
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

    [LoggerMessage(Level = LogLevel.Information, Message = "The scan completed. It read {Count} photos from Immich.")]
    private static partial void LogCompleted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The scan stopped: {Reason}")]
    private static partial void LogFailed(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "The scan stopped because of a problem in Fibotrail.")]
    private static partial void LogCrashed(ILogger logger, Exception exception);
}
