using System.Net;
using Fibotrail.Immich;
using Fibotrail.Immich.Tests;
using Fibotrail.Server.Immich;

namespace Fibotrail.Server.Tests;

public class ImmichConnectionTests
{
    [Fact]
    public async Task A_working_connection_shows_the_immich_version()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();

        var status = await browser.GetJsonAsync("/api/immich");

        Assert.True(status.GetProperty("connected").GetBoolean());
        Assert.Equal("3.4.1", status.GetProperty("version").GetString());
        // The check asks for the version and for one asset, so that the key is tried.
        Assert.Equal(["/api/server/version", "/api/search/metadata"], app.Immich.Requests.Select(request => request.PathAndQuery));
        Assert.Contains("\"size\":1", app.Immich.Requests[1].Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_wrong_address_shows_the_cant_reach_message_with_the_next_step()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        app.Immich.Unreachable = true;

        var status = await browser.GetJsonAsync("/api/immich");

        Assert.False(status.GetProperty("connected").GetBoolean());
        Assert.Equal("Can't reach Immich. Check IMMICH_URL and IMMICH_API_KEY in your .env file.", status.GetProperty("message").GetString());
        // A check isn't retried, so Settings doesn't wait.
        Assert.Single(app.Immich.Requests);
    }

    [Fact]
    public async Task A_wrong_key_shows_the_same_message_even_though_the_version_request_worked()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        app.Immich.ApiKey = "another-key";

        var status = await browser.GetJsonAsync("/api/immich");

        Assert.False(status.GetProperty("connected").GetBoolean());
        Assert.Equal("3.4.1", status.GetProperty("version").GetString());
        Assert.StartsWith("Can't reach Immich.", status.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_untested_immich_version_shows_a_warning_and_scanning_still_runs()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        app.Immich.VersionJson = """{ "major": 4, "minor": 0, "patch": 2, "prerelease": null, "featureLevel": 3 }""";
        app.Immich.Assets.Add(FakeAsset.Number(1, "2026-03-01T10:00:00.000Z"));

        var status = await browser.GetJsonAsync("/api/immich");
        await app.ScanAsync();

        Assert.True(status.GetProperty("connected").GetBoolean());
        Assert.Contains("4.0.2", status.GetProperty("warning").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, (await browser.GetJsonAsync("/api/scan")).GetProperty("photoCount").GetInt32());
    }

    [Theory]
    [InlineData(3, 4, 1, true)]
    [InlineData(3, 4, 9, true)]
    [InlineData(3, 3, 0, false)]
    [InlineData(3, 5, 0, false)]
    [InlineData(4, 4, 0, false)]
    public void The_tested_range_is_immich_3_4(int major, int minor, int patch, bool tested)
    {
        Assert.Equal(tested, ImmichConnection.IsTested(new ImmichVersion(major, minor, patch)));
    }
}

public class ThumbnailTests
{
    private static async Task<(TestApp App, Browser Browser)> StartWithOnePhotoAsync()
    {
        var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        app.Immich.Assets.Add(FakeAsset.Number(1, "2026-03-01T10:00:00.000Z"));
        await app.ScanAsync();
        return (app, browser);
    }

    private static int ImageRequests(TestApp app) =>
        app.Immich.Requests.Count(request => request.PathAndQuery.Contains("/thumbnail", StringComparison.Ordinal));

    [Fact]
    public async Task The_two_sizes_are_served_with_their_content_types()
    {
        var (app, browser) = await StartWithOnePhotoAsync();
        await using var _ = app;

        var thumbnail = await browser.GetAsync($"/api/photos/{FakeAsset.IdFor(1)}/thumbnail", asPage: false);
        var preview = await browser.GetAsync($"/api/photos/{FakeAsset.IdFor(1)}/preview", asPage: false);

        Assert.Equal(HttpStatusCode.OK, thumbnail.StatusCode);
        Assert.Equal("image/webp", thumbnail.Content.Headers.ContentType!.MediaType);
        Assert.Equal(FakeImmichServer.WebpBytes, await thumbnail.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/jpeg", preview.Content.Headers.ContentType!.MediaType);
        Assert.Equal(FakeImmichServer.JpegBytes, await preview.Content.ReadAsByteArrayAsync());
        Assert.Contains("private", thumbnail.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.True(thumbnail.Headers.CacheControl.NoCache);
        Assert.NotNull(thumbnail.Headers.ETag);
    }

    [Fact]
    public async Task An_unchanged_photo_answers_not_modified_without_asking_immich()
    {
        var (app, browser) = await StartWithOnePhotoAsync();
        await using var _ = app;
        var path = $"/api/photos/{FakeAsset.IdFor(1)}/thumbnail";
        var first = await browser.GetAsync(path, asPage: false);

        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var again = await browser.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, again.StatusCode);
        Assert.Equal(first.Headers.ETag, again.Headers.ETag);
        Assert.Equal(1, ImageRequests(app));
    }

    [Fact]
    public async Task After_a_photo_is_rotated_in_immich_a_rescan_makes_the_new_image_show()
    {
        var (app, browser) = await StartWithOnePhotoAsync();
        await using var _ = app;
        var path = $"/api/photos/{FakeAsset.IdFor(1)}/thumbnail";
        var before = await browser.GetAsync(path, asPage: false);

        // Immich makes a new thumbnail, and a new thumbhash, but keeps the same updatedAt.
        app.Immich.Assets[0] = app.Immich.Assets[0] with { Thumbhash = "rotatedThumbhash==" };
        await app.Scans.RunFullPassAsync(onPage: null, CancellationToken.None);

        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.IfNoneMatch.Add(before.Headers.ETag!);
        var after = await browser.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.NotEqual(before.Headers.ETag, after.Headers.ETag);
        Assert.Equal(2, ImageRequests(app));
        // The old version is gone from the cache.
        Assert.Single(Directory.EnumerateFiles(app.Settings.ThumbnailDirectory, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void The_image_version_follows_the_thumbhash_and_the_update_time()
    {
        var updated = new DateTime(2026, 4, 2, 18, 5, 0, DateTimeKind.Utc);

        var version = ThumbnailStore.VersionOf("EXAMPLEthumbhash0001==", updated);

        Assert.Matches("^[0-9a-f]{16}$", version);
        Assert.Equal(version, ThumbnailStore.VersionOf("EXAMPLEthumbhash0001==", updated));
        Assert.NotEqual(version, ThumbnailStore.VersionOf("rotatedThumbhash==", updated));
        Assert.NotEqual(version, ThumbnailStore.VersionOf("EXAMPLEthumbhash0001==", updated.AddSeconds(1)));
        Assert.NotEqual(version, ThumbnailStore.VersionOf(null, updated));
    }

    [Theory]
    [InlineData("00000000-0000-4000-8000-000000000999")]
    [InlineData("not-an-id")]
    [InlineData("..%2F..%2Ffibotrail.db")]
    public async Task An_id_that_isnt_in_the_photo_table_is_not_found_and_immich_isnt_asked(string id)
    {
        var (app, browser) = await StartWithOnePhotoAsync();
        await using var _ = app;
        // Immich has this asset, but Fibotrail never scanned it.
        app.Immich.Assets.Add(FakeAsset.Number(999, "2026-03-02T10:00:00.000Z"));

        var response = await browser.GetAsync($"/api/photos/{id}/thumbnail", asPage: false);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, ImageRequests(app));
    }

    [Fact]
    public async Task An_image_needs_a_session()
    {
        var (app, _) = await StartWithOnePhotoAsync();
        await using var disposable = app;

        var response = await app.NewBrowser().GetAsync($"/api/photos/{FakeAsset.IdFor(1)}/thumbnail", asPage: false);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, ImageRequests(app));
    }

    [Fact]
    public async Task A_fetched_image_is_cached_in_the_data_folder_and_the_cache_refills_when_deleted()
    {
        var (app, browser) = await StartWithOnePhotoAsync();
        await using var _ = app;
        var path = $"/api/photos/{FakeAsset.IdFor(1)}/thumbnail";

        await browser.GetAsync(path, asPage: false);
        await browser.GetAsync(path, asPage: false);

        Assert.Equal(1, ImageRequests(app));
        Assert.StartsWith(app.Settings.DataDirectory, app.Settings.ThumbnailDirectory, StringComparison.Ordinal);
        Assert.Single(Directory.EnumerateFiles(app.Settings.ThumbnailDirectory, "*", SearchOption.AllDirectories));

        Directory.Delete(app.Settings.ThumbnailDirectory, recursive: true);
        var again = await browser.GetAsync(path, asPage: false);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(2, ImageRequests(app));
    }

    [Fact]
    public async Task A_photo_that_was_deleted_from_immich_is_not_found()
    {
        var (app, browser) = await StartWithOnePhotoAsync();
        await using var _ = app;
        app.Immich.Assets.Clear();

        var response = await browser.GetAsync($"/api/photos/{FakeAsset.IdFor(1)}/thumbnail", asPage: false);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task When_immich_is_down_an_uncached_image_answers_with_a_plain_message()
    {
        var (app, browser) = await StartWithOnePhotoAsync();
        await using var _ = app;
        app.Immich.Unreachable = true;

        var response = await browser.GetAsync($"/api/photos/{FakeAsset.IdFor(1)}/thumbnail", asPage: false);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("Can't reach Immich.", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
