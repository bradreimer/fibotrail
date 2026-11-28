using Fibotrail.Server.Immich;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Fibotrail.Server.Trails;

/// <summary>
/// The API routes for the GPS trails of a trip. A file is sent as the raw request body, with its
/// name in the <c>name</c> query parameter. See docs/specs/gps-trails.md.
/// </summary>
public static class TrailRoutes
{
    public static void MapTrailRoutes(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapGet("/trips/{id:int}/trails", async Task<Results<Ok<IReadOnlyList<TrailInfo>>, NotFound>> (int id, TrailStore trails, CancellationToken cancellationToken) =>
            await trails.ListAsync(id, cancellationToken) is { } list ? TypedResults.Ok(list) : TypedResults.NotFound())
            .WithName("GetTrails");

        api.MapPost("/trips/{id:int}/trails/preview", async Task<Results<Ok<TrailPreview>, NotFound, BadRequest<ProblemDetail>, JsonHttpResult<ProblemDetail>>> (
            int id, string? name, HttpRequest request, TrailStore trails, CancellationToken cancellationToken) =>
        {
            var file = await ReadFileAsync(request, cancellationToken);
            if (file is null)
            {
                return TooLarge();
            }

            var result = await trails.PreviewAsync(id, name, file, cancellationToken);
            return result.Outcome switch
            {
                TrailOutcome.Done => TypedResults.Ok(result.Value!),
                TrailOutcome.NotFound => TypedResults.NotFound(),
                _ => TypedResults.BadRequest(new ProblemDetail(result.Problem!)),
            };
        }).WithName("PreviewTrail").Produces<ProblemDetail>(StatusCodes.Status413PayloadTooLarge);

        api.MapPost("/trips/{id:int}/trails", async Task<Results<Ok<TrailInfo>, NotFound, BadRequest<ProblemDetail>, JsonHttpResult<ProblemDetail>>> (
            int id, string? name, HttpRequest request, TrailStore trails, CancellationToken cancellationToken) =>
        {
            var file = await ReadFileAsync(request, cancellationToken);
            if (file is null)
            {
                return TooLarge();
            }

            var result = await trails.AddAsync(id, name, file, cancellationToken);
            return result.Outcome switch
            {
                TrailOutcome.Done => TypedResults.Ok(result.Value!),
                TrailOutcome.NotFound => TypedResults.NotFound(),
                _ => TypedResults.BadRequest(new ProblemDetail(result.Problem!)),
            };
        }).WithName("AddTrail").Produces<ProblemDetail>(StatusCodes.Status413PayloadTooLarge);

        api.MapDelete("/trips/{id:int}/trails/{trailId:int}", async Task<Results<NoContent, NotFound>> (int id, int trailId, TrailStore trails, CancellationToken cancellationToken) =>
            await trails.RemoveAsync(id, trailId, cancellationToken) == TrailOutcome.Done ? TypedResults.NoContent() : TypedResults.NotFound())
            .WithName("RemoveTrail");
    }

    /// <summary>Reads the body, up to the limit. Returns null when the file is larger than the limit.</summary>
    private static async Task<byte[]?> ReadFileAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > TrailStore.MaxBytes)
        {
            return null;
        }

        // The server's default limit is 30 MB. This route reads one byte past its own limit, to tell a file that's too large.
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = TrailStore.MaxBytes + 1;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > TrailStore.MaxBytes)
            {
                return null;
            }
        }

        return buffer.ToArray();
    }

    private static JsonHttpResult<ProblemDetail> TooLarge() =>
        TypedResults.Json(new ProblemDetail(TrailStore.TooLarge), statusCode: StatusCodes.Status413PayloadTooLarge);
}
