namespace Fibotrail.Immich;

/// <summary>A request was stopped because it isn't an allowed read of the configured Immich server.</summary>
public sealed class ImmichRequestNotAllowedException(string message) : InvalidOperationException(message);

/// <summary>
/// The last step before a request leaves Fibotrail. It stops any request that isn't on the allowed
/// list or isn't for the configured Immich server, and it adds the API key to the rest. The key is
/// set here and nowhere else, so it can't reach another host.
/// </summary>
public sealed class ImmichGuardHandler(Uri baseUrl, string apiKey, HttpMessageHandler inner) : DelegatingHandler(inner)
{
    public const string ApiKeyHeader = "x-api-key";

    private readonly string _apiRoot = new Uri(baseUrl, "api").AbsolutePath;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var target = request.RequestUri
            ?? throw new ImmichRequestNotAllowedException("A request to Immich had no address.");

        if (!string.Equals(target.GetLeftPart(UriPartial.Authority), baseUrl.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            throw new ImmichRequestNotAllowedException($"A request to {target.Host} was stopped. Fibotrail sends requests only to IMMICH_URL.");
        }

        var path = target.AbsolutePath;
        var apiPath = path.StartsWith(_apiRoot + "/", StringComparison.Ordinal) ? path[_apiRoot.Length..] : null;
        if (apiPath is null || !AllowedRequests.IsAllowed(request.Method, apiPath))
        {
            throw new ImmichRequestNotAllowedException($"{request.Method} {path} was stopped. It isn't on the list of Immich requests that Fibotrail may make.");
        }

        request.Headers.Remove(ApiKeyHeader);
        request.Headers.Add(ApiKeyHeader, apiKey);
        return base.SendAsync(request, cancellationToken);
    }
}
