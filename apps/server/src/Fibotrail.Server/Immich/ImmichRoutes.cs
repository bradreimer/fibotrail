using Fibotrail.Immich;
using Fibotrail.Server.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Immich;

/// <summary>Where a scan stands, as the banner and Settings show it.</summary>
/// <param name="State">"Never" before the first scan.</param>
/// <param name="Done">Photos read so far in this scan.</param>
/// <param name="Total">Photos in the library, or null for a scan that reads changes only.</param>
/// <param name="Message">Why the scan failed, in plain words.</param>
/// <param name="FinishedAt">When the scan completed or failed, in UTC.</param>
/// <param name="PhotoCount">Photos in Fibotrail's database.</param>
public sealed record ScanStatus(ScanStatusState State, int Done, int? Total, string? Message, DateTime? FinishedAt, int PhotoCount);

public enum ScanStatusState
{
    Never,
    Running,
    Completed,
    Failed,
}

/// <summary>The API routes for the Immich connection, scanning, and photo images.</summary>
public static class ImmichRoutes
{
    public static void MapImmichRoutes(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapGet("/immich", async (ImmichConnection connection, CancellationToken cancellationToken) =>
            TypedResults.Ok(await connection.CheckAsync(cancellationToken)))
            .WithName("GetImmichStatus");

        api.MapGet("/scan", async (ScanService scans, FibotrailDbContext db, CancellationToken cancellationToken) =>
            TypedResults.Ok(await ReadStatusAsync(scans, db, cancellationToken)))
            .WithName("GetScanStatus");

        api.MapPost("/scan", async Task<Results<Accepted<ScanStatus>, Conflict<ProblemDetail>>> (ScanService scans, FibotrailDbContext db, CancellationToken cancellationToken) =>
        {
            // A run that is "running" while this server isn't scanning belongs to `fibotrail rescan`, which
            // runs in its own process. Starting here would resume that run and write it from two places.
            var latest = await db.ScanRuns.AsNoTracking().OrderByDescending(run => run.Id).Select(run => (ScanState?)run.State).FirstOrDefaultAsync(cancellationToken);
            if (latest == ScanState.Running || !scans.TryStart())
            {
                return TypedResults.Conflict(new ProblemDetail("A scan is already running."));
            }

            return TypedResults.Accepted("/api/scan", await ReadStatusAsync(scans, db, cancellationToken));
        }).WithName("StartScan");

        api.MapGet("/photos/{id}/thumbnail", (string id, ThumbnailStore store, HttpContext context) =>
            ImageAsync(id, ImmichImageSize.Thumbnail, store, context))
            .WithName("GetPhotoThumbnail");

        api.MapGet("/photos/{id}/preview", (string id, ThumbnailStore store, HttpContext context) =>
            ImageAsync(id, ImmichImageSize.Preview, store, context))
            .WithName("GetPhotoPreview");
    }

    private static async Task<ScanStatus> ReadStatusAsync(ScanService scans, FibotrailDbContext db, CancellationToken cancellationToken)
    {
        var photos = await db.Photos.CountAsync(cancellationToken);
        var run = await db.ScanRuns.AsNoTracking().OrderByDescending(candidate => candidate.Id).FirstOrDefaultAsync(cancellationToken);
        if (scans.IsRunning && run?.State != ScanState.Running)
        {
            // The scan has started, and it hasn't written its first row yet.
            return new ScanStatus(ScanStatusState.Running, 0, null, null, null, photos);
        }

        if (run is null)
        {
            return new ScanStatus(ScanStatusState.Never, 0, null, null, null, photos);
        }

        var state = run.State switch
        {
            ScanState.Running => ScanStatusState.Running,
            ScanState.Completed => ScanStatusState.Completed,
            _ => ScanStatusState.Failed,
        };
        // The database stores UTC without a mark. The mark is added here, so the browser reads it as UTC.
        DateTime? finishedAt = run.FinishedAt is { } finished ? DateTime.SpecifyKind(finished, DateTimeKind.Utc) : null;
        return new ScanStatus(state, run.PhotosSeen, run.Total, run.Message, finishedAt, photos);
    }

    private static async Task<Results<FileContentHttpResult, NotFound, ProblemHttpResult>> ImageAsync(
        string id,
        ImmichImageSize size,
        ThumbnailStore store,
        HttpContext context)
    {
        try
        {
            var image = await store.GetAsync(id, size, context.RequestAborted);
            if (image is null)
            {
                return TypedResults.NotFound();
            }

            // A photo's image doesn't change, so the browser may keep it. "private" keeps it out of shared caches.
            context.Response.Headers.CacheControl = "private, max-age=604800";
            return TypedResults.File(image.Bytes, image.ContentType);
        }
        catch (ImmichException)
        {
            return TypedResults.Problem(ImmichConnection.CantReachMessage, statusCode: StatusCodes.Status502BadGateway);
        }
    }
}

/// <summary>A problem, in plain words. The web app shows <see cref="Detail"/> as it is.</summary>
public sealed record ProblemDetail(string Detail);
