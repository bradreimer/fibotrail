using Fibotrail.Immich;
using Fibotrail.Server.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

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

        api.MapPost("/scan", (ScanService scans, FibotrailDbContext db, CancellationToken cancellationToken) =>
            StartAsync(fullPass: false, scans, db, cancellationToken))
            .WithName("StartScan");

        // The button form of `fibotrail rescan`: it rereads every photo, so it sees a location edited in Immich.
        api.MapPost("/rescan", (ScanService scans, FibotrailDbContext db, CancellationToken cancellationToken) =>
            StartAsync(fullPass: true, scans, db, cancellationToken))
            .WithName("StartRescan");

        api.MapGet("/photos/{id}/thumbnail", (string id, ThumbnailStore store, HttpContext context) =>
            ImageAsync(id, ImmichImageSize.Thumbnail, store, context))
            .WithName("GetPhotoThumbnail");

        api.MapGet("/photos/{id}/preview", (string id, ThumbnailStore store, HttpContext context) =>
            ImageAsync(id, ImmichImageSize.Preview, store, context))
            .WithName("GetPhotoPreview");
    }

    private static async Task<Results<Accepted<ScanStatus>, Conflict<ProblemDetail>>> StartAsync(bool fullPass, ScanService scans, FibotrailDbContext db, CancellationToken cancellationToken)
    {
        // A run that is "running" while this server isn't scanning belongs to `fibotrail rescan`, which
        // runs in its own process. Starting here would resume that run and write it from two places.
        var latest = await db.ScanRuns.AsNoTracking().OrderByDescending(run => run.Id).Select(run => (ScanState?)run.State).FirstOrDefaultAsync(cancellationToken);
        if (latest is ScanState.Running or ScanState.Detecting || !scans.TryStart(fullPass))
        {
            return TypedResults.Conflict(new ProblemDetail("A scan is already running."));
        }

        return TypedResults.Accepted("/api/scan", await ReadStatusAsync(scans, db, cancellationToken));
    }

    private static async Task<ScanStatus> ReadStatusAsync(ScanService scans, FibotrailDbContext db, CancellationToken cancellationToken)
    {
        var photos = await db.Photos.CountAsync(cancellationToken);
        var run = await db.ScanRuns.AsNoTracking().OrderByDescending(candidate => candidate.Id).FirstOrDefaultAsync(cancellationToken);
        if (scans.IsRunning && run?.State is not (ScanState.Running or ScanState.Detecting))
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
            // The banner shows detection as part of the scan.
            ScanState.Running or ScanState.Detecting => ScanStatusState.Running,
            ScanState.Completed => ScanStatusState.Completed,
            _ => ScanStatusState.Failed,
        };
        // The database stores UTC without a mark. The mark is added here, so the browser reads it as UTC.
        DateTime? finishedAt = run.FinishedAt is { } finished ? DateTime.SpecifyKind(finished, DateTimeKind.Utc) : null;
        return new ScanStatus(state, run.PhotosSeen, run.Total, run.Message, finishedAt, photos);
    }

    private static async Task<Results<FileContentHttpResult, StatusCodeHttpResult, NotFound, ProblemHttpResult>> ImageAsync(
        string id,
        ImmichImageSize size,
        ThumbnailStore store,
        HttpContext context)
    {
        try
        {
            var key = await store.FindAsync(id, context.RequestAborted);
            if (key is null)
            {
                return TypedResults.NotFound();
            }

            // The browser keeps the image but asks again each time. While the photo is unchanged, the
            // answer is a 304 with no body. After a scan reads a change, such as a rotation, the ETag
            // differs and the new image is sent. "private" keeps it out of shared caches.
            var etag = new EntityTagHeaderValue($"\"{key.Version}\"");
            if (context.Request.GetTypedHeaders().IfNoneMatch.Any(match => match.Tag == etag.Tag))
            {
                SetCacheHeaders(context, etag);
                return TypedResults.StatusCode(StatusCodes.Status304NotModified);
            }

            var image = await store.GetAsync(key, size, context.RequestAborted);
            if (image is null)
            {
                return TypedResults.NotFound();
            }

            SetCacheHeaders(context, etag);
            return TypedResults.File(image.Bytes, image.ContentType);
        }
        catch (ImmichException)
        {
            return TypedResults.Problem(ImmichConnection.CantReachMessage, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static void SetCacheHeaders(HttpContext context, EntityTagHeaderValue etag)
    {
        context.Response.Headers.CacheControl = "private, no-cache";
        context.Response.Headers.ETag = etag.ToString();
    }
}

/// <summary>A problem, in plain words. The web app shows <see cref="Detail"/> as it is.</summary>
public sealed record ProblemDetail(string Detail);
