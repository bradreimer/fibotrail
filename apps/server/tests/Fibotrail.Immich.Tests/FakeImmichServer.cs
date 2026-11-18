using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fibotrail.Immich.Tests;

/// <summary>One asset in the stand-in Immich library. Every value is made up.</summary>
public sealed record FakeAsset(
    Guid Id,
    string LocalDateTime,
    string UpdatedAt = "2026-04-02T18:05:00.000Z",
    double? Latitude = null,
    double? Longitude = null,
    string? City = null,
    string? State = null,
    string? Country = null,
    bool IsVideo = false,
    bool IsTrashed = false,
    string Thumbhash = "EXAMPLEthumbhash0001==")
{
    /// <summary>An asset whose ID is built from a number, so a test can name it.</summary>
    public static FakeAsset Number(int number, string localDateTime) =>
        new(IdFor(number), localDateTime);

    public static Guid IdFor(int number) => Guid.Parse($"00000000-0000-4000-8000-{number:D12}");
}

/// <summary>One request that reached the stand-in, as Immich's access log would show it.</summary>
public sealed record FakeRequest(string Method, string PathAndQuery, string? ApiKey, string? Body);

/// <summary>
/// A stand-in for an Immich 3.4.1 server, for tests. It answers the four requests that Fibotrail
/// makes, in the shapes recorded in docs/knowledge/immich-api-findings.md, and it keeps a list of
/// every request it gets. It's passed to <see cref="ImmichClient"/> as the transport, so no test
/// uses the network.
/// </summary>
public sealed class FakeImmichServer : HttpMessageHandler
{
    public const string Key = "test-key-not-real";

    private static readonly JsonObject Template =
        JsonNode.Parse(Recorded("first-page"))!["assets"]!["items"]![0]!.AsObject();

    private readonly ConcurrentQueue<FakeRequest> _requests = new();
    private int _failuresLeft;

    /// <summary>The library. A search returns it newest first, as Immich does.</summary>
    public List<FakeAsset> Assets { get; } = [];

    public IReadOnlyList<FakeRequest> Requests => [.. _requests];

    /// <summary>The key that the stand-in accepts.</summary>
    public string ApiKey { get; set; } = Key;

    public string VersionJson { get; set; } = Recorded("version");

    /// <summary>When set, every request waits until it's canceled, like an Immich that hangs.</summary>
    public bool Hangs { get; set; }

    /// <summary>When set, a request fails as if nothing listened at the address.</summary>
    public bool Unreachable { get; set; }

    /// <summary>Runs before each search is answered. A test uses it to change the library mid-scan, or to stop.</summary>
    public Func<int, Task>? BeforeSearch { get; set; }

    /// <summary>Makes the next requests fail with status 500.</summary>
    public void FailNext(int count) => _failuresLeft = count;

    /// <summary>A recorded response from the Responses folder.</summary>
    public static string Recorded(string name)
    {
        var assembly = typeof(FakeImmichServer).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(candidate => candidate.EndsWith($".{name}.json", StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        return reader.ReadToEnd();
    }

    public int SearchCount => _requests.Count(request => request.PathAndQuery.StartsWith("/api/search/metadata", StringComparison.Ordinal));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var key = request.Headers.TryGetValues(ImmichGuardHandler.ApiKeyHeader, out var values) ? values.Single() : null;
        _requests.Enqueue(new FakeRequest(request.Method.Method, request.RequestUri!.PathAndQuery, key, body));

        if (Hangs)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        if (Unreachable)
        {
            throw new HttpRequestException("Connection refused");
        }

        if (Interlocked.Decrement(ref _failuresLeft) >= 0)
        {
            return Json(HttpStatusCode.InternalServerError, """{ "message": "Internal server error" }""");
        }

        var path = request.RequestUri.AbsolutePath;
        if (request.Method == HttpMethod.Get && path == "/api/server/version")
        {
            // Immich answers the version without a key.
            return Json(HttpStatusCode.OK, VersionJson);
        }

        if (key is null)
        {
            return Json(HttpStatusCode.Unauthorized, """{ "message": "Authentication required" }""");
        }

        if (key != ApiKey)
        {
            return Json(HttpStatusCode.Unauthorized, Recorded("error-invalid-key"));
        }

        if (request.Method == HttpMethod.Post && path == "/api/search/metadata")
        {
            if (BeforeSearch is not null)
            {
                await BeforeSearch(SearchCount);
            }

            return Json(HttpStatusCode.OK, Search(JsonNode.Parse(body!)!.AsObject()));
        }

        if (request.Method == HttpMethod.Get && path == "/api/assets/statistics")
        {
            var live = Assets.Where(asset => !asset.IsTrashed).ToList();
            return Json(HttpStatusCode.OK, $$"""{ "images": {{live.Count(asset => !asset.IsVideo)}}, "videos": {{live.Count(asset => asset.IsVideo)}}, "total": {{live.Count}} }""");
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/api/assets/", StringComparison.Ordinal) && path.EndsWith("/thumbnail", StringComparison.Ordinal))
        {
            var id = Guid.Parse(path.Split('/')[3]);
            if (Assets.All(asset => asset.Id != id))
            {
                return Json(HttpStatusCode.BadRequest, """{ "message": "Not found or no asset.view access" }""");
            }

            var preview = request.RequestUri.Query.Contains("size=preview", StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(preview ? JpegBytes : WebpBytes)
                {
                    Headers = { ContentType = new(preview ? "image/jpeg" : "image/webp") },
                },
            };
        }

        // A real Immich would run a request that isn't a read. Reaching this line means the guard failed.
        return Json(HttpStatusCode.NotFound, """{ "message": "Not found" }""");
    }

    /// <summary>The first bytes of a WebP file, then filler.</summary>
    public static readonly byte[] WebpBytes = [.. "RIFF"u8, 4, 0, 0, 0, .. "WEBP"u8, 1, 2, 3, 4];

    /// <summary>The first bytes of a JPEG file, then filler.</summary>
    public static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 5, 6, 7, 8];

    private string Search(JsonObject request)
    {
        var size = request["size"]?.GetValue<int>() ?? 250;
        var offset = 0;
        if (request["cursor"]?.GetValue<string>() is { } cursor)
        {
            // The cursor is an offset in Base64, as the spike found.
            offset = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)))!["offset"]!.GetValue<int>();
        }

        var matches = Assets.OrderByDescending(asset => asset.LocalDateTime, StringComparer.Ordinal).AsEnumerable();
        if (request["filter"]?["updatedAt"]?["gt"]?.GetValue<string>() is { } changedAfter)
        {
            var after = DateTimeOffset.Parse(changedAfter, System.Globalization.CultureInfo.InvariantCulture);
            matches = matches.Where(asset => DateTimeOffset.Parse(asset.UpdatedAt, System.Globalization.CultureInfo.InvariantCulture) > after);
        }

        var all = matches.ToList();
        var page = all.Skip(offset).Take(size).ToList();
        var next = offset + size < all.Count
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes($$"""{"offset":{{offset + size}}}"""))
            : null;

        var response = JsonNode.Parse(Recorded("empty-page"))!.AsObject();
        var assets = response["assets"]!.AsObject();
        assets["total"] = page.Count;
        assets["count"] = page.Count;
        assets["nextCursor"] = next;
        assets["items"] = new JsonArray([.. page.Select(ToJson)]);
        return response.ToJsonString();
    }

    private static JsonNode ToJson(FakeAsset asset)
    {
        var item = Template.DeepClone().AsObject();
        item["id"] = asset.Id.ToString();
        item["localDateTime"] = asset.LocalDateTime;
        item["fileCreatedAt"] = asset.LocalDateTime;
        item["updatedAt"] = asset.UpdatedAt;
        item["type"] = asset.IsVideo ? "VIDEO" : "IMAGE";
        item["duration"] = asset.IsVideo ? 8836 : null;
        item["isTrashed"] = asset.IsTrashed;
        item["thumbhash"] = asset.Thumbhash;
        var exif = item["exifInfo"]!.AsObject();
        exif["latitude"] = asset.Latitude;
        exif["longitude"] = asset.Longitude;
        exif["city"] = asset.City;
        exif["state"] = asset.State;
        exif["country"] = asset.Country;
        return item;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

/// <summary>Answers every request with one fixed body. For tests of how a recorded response is read.</summary>
public sealed class FixedResponseHandler(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    public List<(string Method, string Path, string? Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request.Method.Method, request.RequestUri!.PathAndQuery, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
        return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
