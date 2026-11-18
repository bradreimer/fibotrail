using System.Net;
using System.Text.Json;

namespace Fibotrail.Immich.Tests;

public class ImmichClientTests
{
    private static readonly Uri BaseUrl = new("http://immich.invalid:2283");

    private static ImmichOptions Options(TimeSpan? timeout = null) =>
        new(BaseUrl, FakeImmichServer.Key, timeout ?? TimeSpan.FromSeconds(5))
        {
            RetryDelays = [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero],
        };

    [Fact]
    public async Task The_recorded_first_page_is_read_field_by_field()
    {
        using var client = new ImmichClient(Options(), new FixedResponseHandler(FakeImmichServer.Recorded("first-page")));

        var page = await client.SearchAsync(null, null, 1000, CancellationToken.None);

        Assert.Equal("eyJvZmZzZXQiOjR9", page.NextCursor);
        Assert.Equal(4, page.Assets.Count);

        var image = page.Assets[0];
        Assert.Equal(Guid.Parse("11111111-1111-4111-8111-111111111111"), image.Id);
        Assert.Equal(new DateTime(2026, 3, 17, 23, 30, 0), image.LocalTime);
        Assert.Equal(DateTimeKind.Unspecified, image.LocalTime!.Value.Kind);
        Assert.Equal(10.5, image.Latitude);
        Assert.Equal(20.25, image.Longitude);
        Assert.Equal(("Port Example", "Example Province", "Exampleland"), (image.City, image.State, image.Country));
        Assert.Equal(new DateTime(2026, 4, 2, 18, 5, 0, 123, DateTimeKind.Utc), image.UpdatedAt);
        Assert.False(image.IsVideo);
        Assert.Null(image.DurationMs);
        Assert.False(image.IsTrashed);

        var noPosition = page.Assets[1];
        Assert.Null(noPosition.Latitude);
        Assert.Null(noPosition.Longitude);
        Assert.Null(noPosition.City);
        Assert.Equal(new DateTime(2026, 3, 17, 14, 0, 0), noPosition.LocalTime);

        var video = page.Assets[2];
        Assert.True(video.IsVideo);
        Assert.Equal(8836, video.DurationMs);
        // The local time is 12:15, not the 16:15 of fileCreatedAt.
        Assert.Equal(new DateTime(2026, 3, 16, 12, 15, 0), video.LocalTime);
    }

    [Fact]
    public async Task The_recorded_last_page_has_a_trashed_asset_and_no_next_cursor()
    {
        using var client = new ImmichClient(Options(), new FixedResponseHandler(FakeImmichServer.Recorded("last-page")));

        var page = await client.SearchAsync("eyJvZmZzZXQiOjR9", null, 1000, CancellationToken.None);

        Assert.Null(page.NextCursor);
        var asset = Assert.Single(page.Assets);
        Assert.True(asset.IsTrashed);
        Assert.Equal(-5.125, asset.Latitude);
        Assert.Null(asset.Country);
    }

    [Fact]
    public async Task The_recorded_empty_page_has_no_assets()
    {
        using var client = new ImmichClient(Options(), new FixedResponseHandler(FakeImmichServer.Recorded("empty-page")));

        var page = await client.SearchAsync(null, new DateTime(2026, 9, 7, 21, 52, 37, 969, DateTimeKind.Utc), 1000, CancellationToken.None);

        Assert.Empty(page.Assets);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task A_first_scan_request_asks_for_exif_a_full_page_and_the_timeline_only()
    {
        var handler = new FixedResponseHandler(FakeImmichServer.Recorded("empty-page"));
        using var client = new ImmichClient(Options(), handler);

        await client.SearchAsync(null, null, 1000, CancellationToken.None);

        var (method, path, body) = Assert.Single(handler.Requests);
        Assert.Equal(("POST", "/api/search/metadata"), (method, path));
        using var sent = JsonDocument.Parse(body!);
        Assert.True(sent.RootElement.GetProperty("withExif").GetBoolean());
        Assert.Equal(1000, sent.RootElement.GetProperty("size").GetInt32());
        Assert.Equal("timeline", sent.RootElement.GetProperty("filter").GetProperty("visibility").GetProperty("eq").GetString());
        Assert.False(sent.RootElement.GetProperty("filter").TryGetProperty("updatedAt", out _));
        Assert.False(sent.RootElement.TryGetProperty("cursor", out _));
        // The older parameters can't be mixed with filter and cursor: Immich answers 400.
        Assert.False(sent.RootElement.TryGetProperty("page", out _));
        Assert.False(sent.RootElement.TryGetProperty("updatedAfter", out _));
    }

    [Fact]
    public async Task A_later_scan_request_adds_the_changed_since_filter_and_the_cursor()
    {
        var handler = new FixedResponseHandler(FakeImmichServer.Recorded("empty-page"));
        using var client = new ImmichClient(Options(), handler);

        await client.SearchAsync("eyJvZmZzZXQiOjEwMDB9", new DateTime(2026, 9, 7, 21, 52, 37, 969, DateTimeKind.Utc), 1000, CancellationToken.None);

        using var sent = JsonDocument.Parse(handler.Requests[0].Body!);
        Assert.Equal("eyJvZmZzZXQiOjEwMDB9", sent.RootElement.GetProperty("cursor").GetString());
        var changedSince = sent.RootElement.GetProperty("filter").GetProperty("updatedAt").GetProperty("gt").GetDateTimeOffset();
        Assert.Equal(new DateTimeOffset(2026, 9, 7, 21, 52, 37, 969, TimeSpan.Zero), changedSince);
    }

    [Fact]
    public async Task The_version_and_the_library_total_are_read()
    {
        using var immich = new FakeImmichServer();
        immich.Assets.AddRange([FakeAsset.Number(1, "2026-03-01T10:00:00.000Z"), FakeAsset.Number(2, "2026-03-02T10:00:00.000Z")]);
        using var client = new ImmichClient(Options(), immich);

        var version = await client.GetVersionAsync(CancellationToken.None);
        var total = await client.GetTotalAsync(CancellationToken.None);

        Assert.Equal("3.4.1", version.ToString());
        Assert.Equal(2, total);
    }

    [Fact]
    public async Task The_two_image_sizes_are_fetched_as_bytes()
    {
        using var immich = new FakeImmichServer();
        immich.Assets.Add(FakeAsset.Number(1, "2026-03-01T10:00:00.000Z"));
        using var client = new ImmichClient(Options(), immich);

        var thumbnail = await client.GetImageAsync(FakeAsset.IdFor(1), ImmichImageSize.Thumbnail, CancellationToken.None);
        var preview = await client.GetImageAsync(FakeAsset.IdFor(1), ImmichImageSize.Preview, CancellationToken.None);

        Assert.Equal(FakeImmichServer.WebpBytes, thumbnail);
        Assert.Equal(FakeImmichServer.JpegBytes, preview);
        Assert.Contains("size=thumbnail", immich.Requests[0].PathAndQuery, StringComparison.Ordinal);
        Assert.Contains("size=preview", immich.Requests[1].PathAndQuery, StringComparison.Ordinal);
        // An edit made in Immich, such as a rotation, is what Fibotrail shows.
        Assert.All(immich.Requests, request => Assert.Contains("edited=true", request.PathAndQuery, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_search_reads_the_thumbhash_of_each_asset()
    {
        using var immich = new FakeImmichServer();
        immich.Assets.Add(FakeAsset.Number(1, "2026-03-01T10:00:00.000Z") with { Thumbhash = "rotatedThumbhash==" });
        using var client = new ImmichClient(Options(), immich);

        var page = await client.SearchAsync(null, null, 10, CancellationToken.None);

        Assert.Equal("rotatedThumbhash==", Assert.Single(page.Assets).Thumbhash);
    }

    [Fact]
    public async Task An_asset_that_immich_doesnt_have_is_not_found_and_isnt_retried()
    {
        using var immich = new FakeImmichServer();
        using var client = new ImmichClient(Options(), immich);

        await Assert.ThrowsAsync<ImmichNotFoundException>(() => client.GetImageAsync(FakeAsset.IdFor(9), ImmichImageSize.Thumbnail, CancellationToken.None));

        Assert.Single(immich.Requests);
    }

    [Fact]
    public async Task A_wrong_key_is_refused_and_isnt_retried()
    {
        using var immich = new FakeImmichServer { ApiKey = "another-key" };
        using var client = new ImmichClient(Options(), immich);

        var refused = await Assert.ThrowsAsync<ImmichRejectedException>(() => client.SearchAsync(null, null, 1, CancellationToken.None));

        Assert.Equal(401, refused.StatusCode);
        Assert.Single(immich.Requests);
    }

    [Fact]
    public async Task A_request_that_fails_is_retried_three_times_and_then_succeeds()
    {
        using var immich = new FakeImmichServer();
        immich.FailNext(3);
        using var client = new ImmichClient(Options(), immich);

        var page = await client.SearchAsync(null, null, 1000, CancellationToken.None);

        Assert.Empty(page.Assets);
        Assert.Equal(4, immich.Requests.Count);
    }

    [Fact]
    public async Task After_three_retries_the_request_fails_with_a_plain_message()
    {
        using var immich = new FakeImmichServer();
        immich.FailNext(10);
        using var client = new ImmichClient(Options(), immich);

        var failure = await Assert.ThrowsAsync<ImmichUnavailableException>(() => client.SearchAsync(null, null, 1000, CancellationToken.None));

        Assert.Equal(4, immich.Requests.Count);
        Assert.Contains("4 times in a row", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_request_that_takes_longer_than_the_timeout_fails_and_is_retried()
    {
        using var immich = new FakeImmichServer { Hangs = true };
        using var client = new ImmichClient(Options(TimeSpan.FromMilliseconds(40)), immich);

        var failure = await Assert.ThrowsAsync<ImmichUnavailableException>(() => client.GetVersionAsync(CancellationToken.None));

        Assert.Equal(4, immich.Requests.Count);
        Assert.Contains("didn't answer within", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_waits_between_tries_are_1_5_and_15_seconds_by_default()
    {
        var defaults = new ImmichOptions(BaseUrl, FakeImmichServer.Key, TimeSpan.FromSeconds(30));

        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)], defaults.RetryDelays);

        // With the default waits and a clock that doesn't move, the second try never starts.
        using var immich = new FakeImmichServer();
        immich.FailNext(1);
        using var client = new ImmichClient(defaults, immich, new StoppedClock());
        var pending = client.GetVersionAsync(CancellationToken.None);
        await Task.Delay(50);

        Assert.False(pending.IsCompleted);
        Assert.Single(immich.Requests);
    }

    [Fact]
    public async Task Canceling_stops_at_once_with_no_retry()
    {
        using var immich = new FakeImmichServer { Hangs = true };
        using var client = new ImmichClient(Options(TimeSpan.FromSeconds(30)), immich);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(40));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetVersionAsync(cancel.Token));

        Assert.Single(immich.Requests);
    }

    [Fact]
    public async Task The_key_is_sent_in_the_api_key_header_and_never_in_the_address()
    {
        using var immich = new FakeImmichServer();
        using var client = new ImmichClient(Options(), immich);

        await client.SearchAsync(null, null, 1, CancellationToken.None);

        var request = Assert.Single(immich.Requests);
        Assert.Equal(FakeImmichServer.Key, request.ApiKey);
        Assert.DoesNotContain(FakeImmichServer.Key, request.PathAndQuery, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeImmichServer.Key, request.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public void The_options_never_print_the_key()
    {
        Assert.DoesNotContain(FakeImmichServer.Key, Options().ToString(), StringComparison.Ordinal);
    }

    /// <summary>A clock whose timers never fire.</summary>
    private sealed class StoppedClock : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new NoTimer();

        private sealed class NoTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
