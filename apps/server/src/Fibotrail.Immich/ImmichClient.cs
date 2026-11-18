using Fibotrail.Immich.Generated;
using Fibotrail.Immich.Generated.Models;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace Fibotrail.Immich;

/// <summary>The reads that Fibotrail makes from Immich.</summary>
public interface IImmichClient
{
    /// <summary>The server version. Immich answers this without a key, so it shows only that the address is right.</summary>
    /// <param name="retry">False for a quick check that reports the first failure.</param>
    Task<ImmichVersion> GetVersionAsync(CancellationToken cancellationToken, bool retry = true);

    /// <summary>The number of assets in the timeline.</summary>
    Task<int> GetTotalAsync(CancellationToken cancellationToken);

    /// <summary>One page of timeline assets, newest first.</summary>
    /// <param name="cursor">The cursor of the page before, or null for the first page.</param>
    /// <param name="updatedAfter">When set, only assets that Immich changed after this time.</param>
    /// <param name="size">The page size, from 1 to 1000.</param>
    /// <param name="retry">False for a quick check that reports the first failure.</param>
    Task<ImmichPage> SearchAsync(string? cursor, DateTime? updatedAfter, int size, CancellationToken cancellationToken, bool retry = true);

    /// <summary>The bytes of a still image of an asset.</summary>
    Task<byte[]> GetImageAsync(Guid assetId, ImmichImageSize size, CancellationToken cancellationToken);
}

/// <summary>
/// Wraps the generated client. Every request has a timeout, is retried when Immich fails or is
/// slow, and passes through <see cref="ImmichGuardHandler"/>.
/// </summary>
public sealed class ImmichClient : IImmichClient, IDisposable
{
    public const int MaxPageSize = 1000;

    private readonly ImmichOptions _options;
    private readonly TimeProvider _time;
    private readonly HttpClient _http;
    private readonly ImmichApiClient _api;

    /// <param name="options">The address, the key, and the timeout.</param>
    /// <param name="transport">What sends the requests. A test passes a stand-in for Immich.</param>
    /// <param name="time">The clock that the timeout and the waits between tries follow.</param>
    public ImmichClient(ImmichOptions options, HttpMessageHandler? transport = null, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _time = time ?? TimeProvider.System;

        // The client is built by hand, and not by the HTTP client factory, so that no logging
        // handler sees the requests. The timeout is applied for each try, below.
#pragma warning disable CA2000 // The HttpClient owns the handlers and disposes them.
        _http = new HttpClient(new ImmichGuardHandler(options.BaseUrl, options.ApiKey, transport ?? new SocketsHttpHandler { AllowAutoRedirect = false }))
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
#pragma warning restore CA2000
        var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: _http)
        {
            BaseUrl = new Uri(options.BaseUrl, "api").AbsoluteUri,
        };
        _api = new ImmichApiClient(adapter);
    }

    public Task<ImmichVersion> GetVersionAsync(CancellationToken cancellationToken, bool retry = true) => SendAsync(async token =>
    {
        var version = await _api.Server.Version.GetAsync(cancellationToken: token).ConfigureAwait(false);
        return new ImmichVersion(version?.Major ?? 0, version?.Minor ?? 0, version?.Patch ?? 0);
    }, cancellationToken, retry);

    public Task<int> GetTotalAsync(CancellationToken cancellationToken) => SendAsync(async token =>
    {
        var statistics = await _api.Assets.Statistics.GetAsync(
            request => request.QueryParameters.Visibility = AssetVisibility.Timeline, token).ConfigureAwait(false);
        return statistics?.Total ?? 0;
    }, cancellationToken);

    public Task<ImmichPage> SearchAsync(string? cursor, DateTime? updatedAfter, int size, CancellationToken cancellationToken, bool retry = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, MaxPageSize);

        // The timeline filter leaves out the hidden video of a live photo, so it counts once.
        var body = new MetadataSearchDto
        {
            WithExif = true,
            Size = size,
            Cursor = cursor,
            Filter = new SearchFilter
            {
                Visibility = new EnumFilterAssetVisibility { Eq = AssetVisibility.Timeline },
                UpdatedAt = updatedAfter is null
                    ? null
                    : new DateFilter { Gt = new DateTimeOffset(DateTime.SpecifyKind(updatedAfter.Value, DateTimeKind.Utc)) },
            },
        };

        return SendAsync(async token =>
        {
            var response = await _api.Search.Metadata.PostAsync(body, cancellationToken: token).ConfigureAwait(false);
            var assets = response?.Assets?.Items?.Where(item => item.Id is not null).Select(ToAsset).ToList() ?? [];
            return new ImmichPage(assets, string.IsNullOrEmpty(response?.Assets?.NextCursor) ? null : response.Assets.NextCursor);
        }, cancellationToken, retry);
    }

    public Task<byte[]> GetImageAsync(Guid assetId, ImmichImageSize size, CancellationToken cancellationToken) => SendAsync(async token =>
    {
        var stream = await _api.Assets[assetId].Thumbnail.GetAsync(
            request =>
            {
                request.QueryParameters.Size = size == ImmichImageSize.Preview ? AssetMediaSize.Preview : AssetMediaSize.Thumbnail;
                // An edit made in Immich, such as a rotation, shows. Immich sends the original when there is no edit.
                request.QueryParameters.Edited = true;
            },
            token).ConfigureAwait(false) ?? throw new ImmichNotFoundException();
        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, token).ConfigureAwait(false);
            return buffer.ToArray();
        }
    }, cancellationToken);

    public void Dispose() => _http.Dispose();

    private static ImmichAsset ToAsset(AssetResponseDto item)
    {
        var exif = item.ExifInfo;
        return new ImmichAsset(
            item.Id!.Value,
            // Immich labels the local wall-clock time as UTC. The label is dropped here.
            item.LocalDateTime is { } local ? DateTime.SpecifyKind(local.UtcDateTime, DateTimeKind.Unspecified) : null,
            exif?.Latitude,
            exif?.Longitude,
            NullIfEmpty(exif?.City),
            NullIfEmpty(exif?.State),
            NullIfEmpty(exif?.Country),
            item.UpdatedAt?.UtcDateTime ?? DateTime.UnixEpoch,
            item.Type == AssetTypeEnum.VIDEO,
            item.Duration,
            item.IsTrashed == true,
            NullIfEmpty(item.Thumbhash));
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Runs one request with the timeout, and tries again after each wait in the options.</summary>
    private async Task<T> SendAsync<T>(Func<CancellationToken, Task<T>> request, CancellationToken cancellationToken, bool retry = true)
    {
        var retries = retry ? _options.RetryDelays.Count : 0;
        for (var attempt = 0; ; attempt++)
        {
            Exception failure;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.Timeout);
            try
            {
                return await request(timeout.Token).ConfigureAwait(false);
            }
            catch (ImmichRequestNotAllowedException)
            {
                throw;
            }
            catch (ApiException exception) when (exception.ResponseStatusCode is 401 or 403)
            {
                throw new ImmichRejectedException(exception.ResponseStatusCode, exception);
            }
            catch (ApiException exception) when (exception.ResponseStatusCode is 400 or 404)
            {
                // Immich answers 400 for an asset ID it doesn't have.
                throw new ImmichNotFoundException(exception);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is ApiException or HttpRequestException or OperationCanceledException or IOException)
            {
                failure = exception;
            }

            if (attempt >= retries)
            {
                throw new ImmichUnavailableException(
                    failure is OperationCanceledException
                        ? $"Immich didn't answer within {_options.Timeout.TotalSeconds:0} seconds, {attempt + 1} times in a row."
                        : $"Immich didn't answer, {attempt + 1} times in a row.",
                    failure);
            }

            await Task.Delay(_options.RetryDelays[attempt], _time, cancellationToken).ConfigureAwait(false);
        }
    }
}
