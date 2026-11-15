using System.Net;
using Fibotrail.Immich.Tests;
using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fibotrail.Server.Tests;

public class ScanTests
{
    /// <summary>A library of numbered photos, one a minute, all changed at the same moment.</summary>
    private static IEnumerable<FakeAsset> Library(int count, string updatedAt = "2026-04-02T18:05:00.000Z") =>
        Enumerable.Range(1, count).Select(number =>
            FakeAsset.Number(number, $"2026-03-{1 + (number / 1440):D2}T{number / 60 % 24:D2}:{number % 60:D2}:00.000Z") with { UpdatedAt = updatedAt });

    private static Task<List<Photo>> PhotosAsync(TestApp app) =>
        app.WithDbAsync(db => db.Photos.AsNoTracking().OrderBy(photo => photo.Id).ToListAsync());

    private static Task<ScanRun> LatestRunAsync(TestApp app) =>
        app.WithDbAsync(db => db.ScanRuns.AsNoTracking().OrderByDescending(run => run.Id).FirstAsync());

    [Fact]
    public async Task A_full_scan_pages_through_the_library_and_stores_every_photo()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(Library(2500));

        await app.ScanAsync();

        Assert.Equal(2500, (await PhotosAsync(app)).Count);
        Assert.Equal(3, app.Immich.SearchCount);
        var run = await LatestRunAsync(app);
        Assert.Equal(ScanState.Completed, run.State);
        Assert.Equal(2500, run.PhotosSeen);
        Assert.Equal(2500, run.Total);
        Assert.Null(run.Cursor);
        Assert.NotNull(run.FinishedAt);
    }

    [Fact]
    public async Task A_scan_stores_only_the_values_the_spec_lists()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.Add(new FakeAsset(
            FakeAsset.IdFor(1), "2026-03-17T23:30:00.000Z", "2026-04-02T18:05:00.123Z", 10.5, 20.25, "Port Example", "Example Province", "Exampleland"));
        app.Immich.Assets.Add(FakeAsset.Number(2, "2026-03-16T12:15:00.000Z") with { IsVideo = true });

        await app.ScanAsync();

        var photos = await PhotosAsync(app);
        var image = photos[0];
        Assert.Equal(FakeAsset.IdFor(1).ToString(), image.Id);
        Assert.Equal(new DateTime(2026, 3, 17, 23, 30, 0), image.LocalTime);
        Assert.Equal((10.5, 20.25), (image.Latitude, image.Longitude));
        Assert.Equal(("Port Example", "Example Province", "Exampleland"), (image.City, image.State, image.Country));
        Assert.Equal(new DateTime(2026, 4, 2, 18, 5, 0, 123), image.ImmichUpdatedAt);
        Assert.False(image.IsVideo);
        Assert.Null(image.DurationMs);

        var video = photos[1];
        Assert.True(video.IsVideo);
        Assert.Equal(8836, video.DurationMs);
        Assert.Null(video.Latitude);
    }

    [Fact]
    public async Task A_trashed_asset_isnt_stored_and_its_row_is_removed()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(Library(3));
        await app.ScanAsync();

        // The asset goes to the trash, which Immich reports with a newer update time.
        app.Immich.Assets[1] = app.Immich.Assets[1] with { IsTrashed = true, UpdatedAt = "2026-05-01T00:00:00.000Z" };
        app.Immich.Assets.Add(FakeAsset.Number(4, "2026-03-04T10:00:00.000Z") with { IsTrashed = true, UpdatedAt = "2026-05-01T00:00:00.000Z" });
        await app.ScanAsync();

        Assert.Equal([FakeAsset.IdFor(1).ToString(), FakeAsset.IdFor(3).ToString()], (await PhotosAsync(app)).Select(photo => photo.Id));
    }

    [Fact]
    public async Task A_position_of_exactly_0_0_is_stored_as_no_position()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.Add(FakeAsset.Number(1, "2026-03-01T10:00:00.000Z") with { Latitude = 0, Longitude = 0, City = "Null Island" });
        app.Immich.Assets.Add(FakeAsset.Number(2, "2026-03-01T11:00:00.000Z") with { Latitude = 0, Longitude = 20.5 });

        await app.ScanAsync();

        var photos = await PhotosAsync(app);
        Assert.Null(photos[0].Latitude);
        Assert.Null(photos[0].Longitude);
        Assert.Null(photos[0].City);
        Assert.Equal(new DateTime(2026, 3, 1, 10, 0, 0), photos[0].LocalTime);
        Assert.Equal((0d, 20.5), (photos[1].Latitude, photos[1].Longitude));
    }

    [Fact]
    public async Task A_second_scan_with_no_changes_reads_one_empty_page_and_stores_nothing()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(Library(1500));
        await app.ScanAsync();
        var searchesBefore = app.Immich.SearchCount;

        await app.ScanAsync();

        Assert.Equal(searchesBefore + 1, app.Immich.SearchCount);
        Assert.Contains("\"updatedAt\":{\"gt\":", app.Immich.Requests[^1].Body!, StringComparison.Ordinal);
        var run = await LatestRunAsync(app);
        Assert.Equal(ScanState.Completed, run.State);
        Assert.Equal(0, run.PhotosSeen);
        Assert.Equal(1500, (await PhotosAsync(app)).Count);
    }

    [Fact]
    public async Task A_second_scan_reads_only_photos_added_or_changed_since_the_first()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(Library(5));
        await app.ScanAsync();

        app.Immich.Assets.Add(FakeAsset.Number(6, "2026-06-01T10:00:00.000Z") with { UpdatedAt = "2026-06-01T12:00:00.000Z" });
        app.Immich.Assets[0] = app.Immich.Assets[0] with { City = "Moved", Latitude = 1, Longitude = 2, UpdatedAt = "2026-06-02T12:00:00.000Z" };
        await app.ScanAsync();

        var run = await LatestRunAsync(app);
        Assert.Equal(2, run.PhotosSeen);
        Assert.Equal(new DateTime(2026, 4, 2, 18, 5, 0), run.ChangedSince);
        Assert.Equal(new DateTime(2026, 6, 2, 12, 0, 0), run.LatestUpdatedAt);
        var photos = await PhotosAsync(app);
        Assert.Equal(6, photos.Count);
        Assert.Equal("Moved", photos[0].City);
    }

    [Fact]
    public async Task An_asset_that_immich_returns_again_replaces_its_row()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(Library(1001));

        // A photo is added while the scan pages, so the offset cursor returns one asset twice.
        app.Immich.BeforeSearch = search =>
        {
            if (search == 2)
            {
                app.Immich.Assets.Add(FakeAsset.Number(5000, "2026-12-31T10:00:00.000Z"));
            }

            return Task.CompletedTask;
        };
        await app.ScanAsync();

        var run = await LatestRunAsync(app);
        Assert.Equal(1002, run.PhotosSeen);
        Assert.Equal(1001, (await PhotosAsync(app)).Count);
    }

    [Fact]
    public async Task A_scan_that_fails_keeps_its_cursor_and_the_next_scan_resumes_from_it()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(Library(2500));
        app.Immich.BeforeSearch = search =>
        {
            if (search == 2)
            {
                // The second page fails, and so do its three retries.
                app.Immich.FailNext(3);
                throw new HttpRequestException("Connection reset");
            }

            return Task.CompletedTask;
        };

        await app.ScanAsync();

        var failed = await LatestRunAsync(app);
        Assert.Equal(ScanState.Failed, failed.State);
        Assert.Equal("Immich didn't answer in time. Your progress is saved.", failed.Message);
        Assert.Equal(1000, failed.PhotosSeen);
        Assert.NotNull(failed.Cursor);
        Assert.Equal(1000, (await PhotosAsync(app)).Count);

        app.Immich.BeforeSearch = null;
        var searchesBefore = app.Immich.SearchCount;
        await app.ScanAsync();

        var resumed = await LatestRunAsync(app);
        Assert.Equal(failed.Id, resumed.Id);
        Assert.Equal(ScanState.Completed, resumed.State);
        Assert.Equal(2500, resumed.PhotosSeen);
        Assert.Equal(2500, (await PhotosAsync(app)).Count);
        // Two pages were left. The first page wasn't read again.
        Assert.Equal(searchesBefore + 2, app.Immich.SearchCount);
        Assert.Contains("\"cursor\":", app.Immich.Requests.Last(request => request.Method == "POST").Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_scan_interrupted_by_a_restart_continues_from_the_saved_cursor()
    {
        var first = await TestApp.StartAsync();
        var immich = first.Immich;
        var stopping = first.App.Lifetime.ApplicationStopping;
        immich.Assets.AddRange(Library(2500));
        var reachedSecondPage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        immich.BeforeSearch = async search =>
        {
            if (search == 2)
            {
                // The container stops while the second page is on its way.
                reachedSecondPage.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, stopping);
            }
        };
        Assert.True(first.Scans.TryStart());
        await reachedSecondPage.Task;
        var interrupted = first.Scans.Current;
        await first.StopAsync();
        await interrupted;

        immich.BeforeSearch = null;
        await using var second = await TestApp.StartAsync(first.Settings.DataDirectory, immich);
        Assert.Equal(ScanState.Running, (await LatestRunAsync(second)).State);
        Assert.Equal(1000, (await PhotosAsync(second)).Count);
        var searchesBefore = immich.SearchCount;

        await ServerApp.StartImmichAsync(second.App);
        await second.Scans.Current;

        var run = await LatestRunAsync(second);
        Assert.Equal(ScanState.Completed, run.State);
        Assert.Equal(2500, (await PhotosAsync(second)).Count);
        // One search checks the connection, and two read the pages that were left.
        Assert.Equal(searchesBefore + 3, immich.SearchCount);
    }

    [Fact]
    public async Task A_request_that_takes_longer_than_the_timeout_fails_the_scan_and_keeps_its_progress()
    {
        await using var app = await TestApp.StartAsync(immichTimeout: TimeSpan.FromMilliseconds(400));
        app.Immich.Assets.AddRange(Library(1500));
        app.Immich.BeforeSearch = search =>
        {
            app.Immich.Hangs = search >= 1;
            return Task.CompletedTask;
        };

        await app.ScanAsync();

        var run = await LatestRunAsync(app);
        Assert.Equal(ScanState.Failed, run.State);
        Assert.Equal(ScanService.TimeoutMessage, run.Message);
        Assert.Equal(1000, run.PhotosSeen);
        // The second page was tried once and retried 3 times.
        Assert.Equal(5, app.Immich.SearchCount);
    }

    [Fact]
    public async Task A_wrong_key_fails_the_scan_with_the_cant_reach_message()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.ApiKey = "another-key";

        await app.ScanAsync();

        var run = await LatestRunAsync(app);
        Assert.Equal(ScanState.Failed, run.State);
        Assert.Equal("Can't reach Immich. Check IMMICH_URL and IMMICH_API_KEY in your .env file.", run.Message);
    }

    [Fact]
    public async Task One_scan_runs_at_a_time()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        await browser.GetJsonAsync("/api/session");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Immich.BeforeSearch = _ => release.Task;

        var first = await browser.SendJsonAsync(HttpMethod.Post, "/api/scan");
        var second = await browser.SendJsonAsync(HttpMethod.Post, "/api/scan");
        var during = await browser.GetJsonAsync("/api/scan");
        release.SetResult();
        await app.Scans.Current;
        var after = await browser.GetJsonAsync("/api/scan");

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("already running", await second.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal("running", during.GetProperty("state").GetString());
        Assert.Equal("completed", after.GetProperty("state").GetString());
    }

    [Fact]
    public async Task The_scan_status_gives_the_banner_its_count_and_total()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        app.Immich.Assets.AddRange(Library(2500));

        var before = await browser.GetJsonAsync("/api/scan");
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Immich.BeforeSearch = async search =>
        {
            if (search == 2)
            {
                paused.SetResult();
                await release.Task;
            }
        };
        Assert.True(app.Scans.TryStart());
        await paused.Task;
        var during = await browser.GetJsonAsync("/api/scan");
        release.SetResult();
        await app.Scans.Current;
        var after = await browser.GetJsonAsync("/api/scan");

        Assert.Equal("never", before.GetProperty("state").GetString());
        Assert.Equal("running", during.GetProperty("state").GetString());
        Assert.Equal(1000, during.GetProperty("done").GetInt32());
        Assert.Equal(2500, during.GetProperty("total").GetInt32());
        Assert.Equal(1000, during.GetProperty("photoCount").GetInt32());
        Assert.Equal("completed", after.GetProperty("state").GetString());
        Assert.Equal(2500, after.GetProperty("photoCount").GetInt32());
        Assert.True(after.TryGetProperty("finishedAt", out var finished) && finished.GetString() is not null);
    }

    [Fact]
    public async Task Starting_a_scan_needs_the_anti_forgery_token()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        browser.ForgetCsrfToken();

        var response = await browser.SendJsonAsync(HttpMethod.Post, "/api/scan");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.False(app.Scans.IsRunning);
        Assert.Equal(0, app.Immich.SearchCount);
    }

    [Fact]
    public async Task A_full_scan_sends_only_requests_on_the_allowed_list()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        app.Immich.Assets.AddRange(Library(1200));

        await ServerApp.StartImmichAsync(app.App);
        await browser.GetJsonAsync("/api/immich");
        await app.ScanAsync();
        await app.ScanAsync();
        await browser.GetAsync($"/api/photos/{FakeAsset.IdFor(1)}/thumbnail", asPage: false);
        await browser.GetAsync($"/api/photos/{FakeAsset.IdFor(1)}/preview", asPage: false);

        Assert.NotEmpty(app.Immich.Requests);
        Assert.All(app.Immich.Requests, request =>
        {
            var path = request.PathAndQuery.Split('?')[0];
            Assert.StartsWith("/api/", path, StringComparison.Ordinal);
            Assert.True(
                Fibotrail.Immich.AllowedRequests.IsAllowed(new HttpMethod(request.Method), path["/api".Length..]),
                $"{request.Method} {path} isn't an allowed read.");
        });
        // The search is the only request that isn't a GET.
        Assert.All(app.Immich.Requests.Where(request => request.Method != "GET"), request =>
            Assert.Equal(("POST", "/api/search/metadata"), (request.Method, request.PathAndQuery)));
        Assert.Equal(["GET", "POST"], app.Immich.Requests.Select(request => request.Method).Distinct().Order());
    }

    [Fact]
    public async Task The_immich_key_appears_in_no_log_line_and_no_api_answer()
    {
        var log = new CollectingLoggerProvider();
        await using var app = await TestApp.StartAsync(logging: log);
        var browser = await app.SignedInBrowserAsync();
        app.Immich.Assets.AddRange(Library(3));

        await ServerApp.StartImmichAsync(app.App);
        await app.ScanAsync();
        app.Immich.FailNext(10);
        await app.ScanAsync();
        var answers = new List<string>();
        foreach (var path in new[] { "/api/immich", "/api/scan", "/api/session", "/openapi/v1.json", "/", "/settings" })
        {
            answers.Add(await (await browser.GetAsync(path, asPage: path.Length < 10 && !path.StartsWith("/api", StringComparison.Ordinal))).Content.ReadAsStringAsync());
        }

        Assert.NotEmpty(log.Lines);
        Assert.Contains(log.Lines, line => line.Contains("Immich", StringComparison.Ordinal));
        Assert.DoesNotContain(log.Lines, line => line.Contains(FakeImmichServer.Key, StringComparison.Ordinal));
        Assert.DoesNotContain(answers, answer => answer.Contains(FakeImmichServer.Key, StringComparison.Ordinal));
    }
}

/// <summary>Keeps every log line, at every level, so a test can search them.</summary>
public sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _lines = new();

    public IReadOnlyList<string> Lines => [.. _lines];

    public ILogger CreateLogger(string categoryName) => new Collector(_lines);

    public void Dispose()
    {
    }

    private sealed class Collector(System.Collections.Concurrent.ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Enqueue($"{formatter(state, exception)} {exception}");
    }
}
