using Fibotrail.Immich.Tests;
using Fibotrail.Server.Cli;
using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fibotrail.Server.Tests;

public class RescanCommandTests
{
    private const double OrlandoLatitude = 28.4;
    private const double OrlandoLongitude = -81.55;

    private static async Task<(int ExitCode, string Output)> RunAsync(TestApp app, params string[] arguments)
    {
        var console = new ScriptedConsole();
        var exitCode = await app.WithServicesAsync(services => RescanCommand.RunAsync(
            arguments,
            console,
            services.GetRequiredService<FibotrailDbContext>(),
            app.Scans,
            app.Settings));
        return (exitCode, string.Join('\n', console.Lines));
    }

    /// <summary>
    /// A library with 60 days at home, and 3 days in March 2027 whose photos still say Vancouver, as if the
    /// camera had no position and Immich guessed home. Home is confirmed, so detection has run and the inbox is empty.
    /// </summary>
    private static async Task<(TestApp App, Browser Browser)> StartWithUnplacedTripAsync()
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 3, 30, TripLibrary.HomeLatitude, TripLibrary.HomeLongitude, "Vancouver", "British Columbia", "Canada"));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        Assert.Empty(await TripLibrary.TripsAsync(app));
        return (app, browser);
    }

    /// <summary>Moves the March 2027 photos to Orlando in Immich, without changing their update time, the case a normal scan can't see.</summary>
    private static void EditLocationsInImmich(TestApp app)
    {
        for (var index = 0; index < app.Immich.Assets.Count; index++)
        {
            if (app.Immich.Assets[index].LocalDateTime.StartsWith("2027-03", StringComparison.Ordinal))
            {
                app.Immich.Assets[index] = app.Immich.Assets[index] with
                {
                    Latitude = OrlandoLatitude,
                    Longitude = OrlandoLongitude,
                    City = "Orlando",
                    State = "Florida",
                    Country = "United States of America",
                };
            }
        }
    }

    [Fact]
    public async Task A_rescan_finds_photos_whose_location_was_edited_and_suggests_the_new_trip()
    {
        var (app, _) = await StartWithUnplacedTripAsync();
        await using (app)
        {
            EditLocationsInImmich(app);

            // A normal scan reads only what Immich says changed, so it misses these.
            await app.ScanAsync();
            Assert.Empty(await TripLibrary.TripsAsync(app));

            var (exitCode, output) = await RunAsync(app);

            Assert.Equal(0, exitCode);
            Assert.Contains("30 changed.", output, StringComparison.Ordinal);
            Assert.Contains("1 new suggestion is in your inbox.", output, StringComparison.Ordinal);
            var trips = await TripLibrary.TripsAsync(app);
            Assert.Equal("Orlando", Assert.Single(trips).MainPlace);
            var photos = await app.WithDbAsync(db => db.Photos.AsNoTracking().Where(photo => photo.City == "Orlando").CountAsync());
            Assert.Equal(30, photos);
        }
    }

    [Fact]
    public async Task A_rescan_asks_immich_for_every_photo_with_no_changed_since_filter()
    {
        var (app, _) = await StartWithUnplacedTripAsync();
        await using (app)
        {
            await app.ScanAsync();
            var requestsBefore = app.Immich.Requests.Count;

            await RunAsync(app);

            var searches = app.Immich.Requests.Skip(requestsBefore).Where(request => request.Method == "POST").ToList();
            Assert.NotEmpty(searches);
            Assert.All(searches, request => Assert.DoesNotContain("updatedAt", request.Body!, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task A_rescan_of_an_unchanged_library_changes_nothing()
    {
        var (app, _) = await StartWithUnplacedTripAsync();
        await using (app)
        {
            var before = await app.WithDbAsync(db => db.Photos.AsNoTracking().CountAsync());

            var (exitCode, output) = await RunAsync(app);

            Assert.Equal(0, exitCode);
            Assert.Contains($"Read {before} photos. 0 changed.", output, StringComparison.Ordinal);
            Assert.Contains("No new suggestions.", output, StringComparison.Ordinal);
            Assert.Equal(before, await app.WithDbAsync(db => db.Photos.AsNoTracking().CountAsync()));
        }
    }

    [Fact]
    public async Task A_trip_marked_not_a_trip_stays_hidden_after_a_rescan()
    {
        var (app, browser) = await StartWithUnplacedTripAsync();
        await using (app)
        {
            EditLocationsInImmich(app);
            await RunAsync(app);
            var inbox = await browser.GetJsonAsync("/api/inbox");
            var id = inbox.GetProperty("suggestions")[0].GetProperty("id").GetInt32();
            await browser.SendJsonAsync(HttpMethod.Post, $"/api/trips/{id}/decision", new { action = "ignore" });

            var (exitCode, output) = await RunAsync(app);

            Assert.Equal(0, exitCode);
            Assert.Contains("No new suggestions.", output, StringComparison.Ordinal);
            var status = await app.WithDbAsync(db => db.Trips.AsNoTracking().Select(trip => trip.Status).SingleAsync());
            Assert.Equal(TripStatus.Ignored, status);
        }
    }

    [Fact]
    public async Task A_rescan_refuses_while_a_scan_is_running_and_doesnt_call_immich()
    {
        var (app, _) = await StartWithUnplacedTripAsync();
        await using (app)
        {
            await app.WithDbAsync(async db =>
            {
                db.ScanRuns.Add(new ScanRun { State = ScanState.Running, StartedAt = DateTime.UtcNow });
                return await db.SaveChangesAsync();
            });
            var requestsBefore = app.Immich.Requests.Count;

            var (exitCode, output) = await RunAsync(app);

            Assert.Equal(1, exitCode);
            Assert.Contains("A scan is running.", output, StringComparison.Ordinal);
            Assert.Equal(requestsBefore, app.Immich.Requests.Count);
        }
    }

    [Fact]
    public async Task A_rescan_with_a_key_immich_rejects_says_it_cant_reach_immich_and_exits_with_1()
    {
        var (app, _) = await StartWithUnplacedTripAsync();
        await using (app)
        {
            app.Immich.ApiKey = "a-different-key";

            var (exitCode, output) = await RunAsync(app);

            Assert.Equal(1, exitCode);
            Assert.Contains(Fibotrail.Server.Immich.ImmichConnection.CantReachMessage, output, StringComparison.Ordinal);
            Assert.DoesNotContain(FakeImmichServer.Key, output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_failed_rescan_resumes_from_its_cursor_on_the_next_normal_scan()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(Enumerable.Range(1, 2500).Select(number => FakeAsset.Number(number, "2026-03-01T10:00:00.000Z")));
        await app.ScanAsync();
        var searchesBefore = app.Immich.SearchCount;
        app.Immich.BeforeSearch = search =>
        {
            if (search == searchesBefore + 2)
            {
                app.Immich.FailNext(3);
                throw new HttpRequestException("Connection reset");
            }

            return Task.CompletedTask;
        };

        var (exitCode, _) = await RunAsync(app);
        Assert.Equal(1, exitCode);

        app.Immich.BeforeSearch = null;
        await app.ScanAsync();
        var run = await app.WithDbAsync(db => db.ScanRuns.AsNoTracking().OrderByDescending(candidate => candidate.Id).FirstAsync());
        Assert.Equal(ScanState.Completed, run.State);
        Assert.Null(run.ChangedSince);
        Assert.Equal(2500, run.PhotosSeen);
    }

    [Fact]
    public async Task Scan_now_is_refused_while_a_rescan_run_is_running_and_allowed_after()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        await browser.GetJsonAsync("/api/session");
        await app.ScanAsync();
        await app.WithDbAsync(async db =>
        {
            db.ScanRuns.Add(new ScanRun { State = ScanState.Running, StartedAt = DateTime.UtcNow });
            return await db.SaveChangesAsync();
        });
        var requestsBefore = app.Immich.Requests.Count;

        var refused = await browser.SendJsonAsync(HttpMethod.Post, "/api/scan");

        Assert.Equal(System.Net.HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(requestsBefore, app.Immich.Requests.Count);

        await app.WithDbAsync(async db =>
        {
            var run = await db.ScanRuns.OrderByDescending(candidate => candidate.Id).FirstAsync();
            run.State = ScanState.Completed;
            return await db.SaveChangesAsync();
        });
        var allowed = await browser.SendJsonAsync(HttpMethod.Post, "/api/scan");
        await app.Scans.Current;

        Assert.Equal(System.Net.HttpStatusCode.Accepted, allowed.StatusCode);
    }

    [Fact]
    public async Task The_command_takes_no_arguments()
    {
        await using var app = await TestApp.StartAsync();

        var (exitCode, output) = await RunAsync(app, "--from", "2020-01-01");

        Assert.Equal(2, exitCode);
        Assert.Contains("takes no arguments", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_rescan_route_finds_photos_whose_location_was_edited_and_a_scan_now_does_not()
    {
        var (app, browser) = await StartWithUnplacedTripAsync();
        await using (app)
        {
            EditLocationsInImmich(app);

            var scan = await browser.SendJsonAsync(HttpMethod.Post, "/api/scan");
            await app.Scans.Current;
            Assert.Equal(System.Net.HttpStatusCode.Accepted, scan.StatusCode);
            Assert.Empty(await TripLibrary.TripsAsync(app));

            var rescan = await browser.SendJsonAsync(HttpMethod.Post, "/api/rescan");
            await app.Scans.Current;

            Assert.Equal(System.Net.HttpStatusCode.Accepted, rescan.StatusCode);
            Assert.Equal("Orlando", Assert.Single(await TripLibrary.TripsAsync(app)).MainPlace);
            var run = await app.WithDbAsync(db => db.ScanRuns.AsNoTracking().OrderByDescending(r => r.Id).FirstAsync());
            Assert.Equal(ScanState.Completed, run.State);
            Assert.Null(run.ChangedSince);
        }
    }

    [Fact]
    public async Task The_rescan_route_is_refused_while_a_scan_runs()
    {
        var (app, browser) = await StartWithUnplacedTripAsync();
        await using (app)
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            app.Immich.BeforeSearch = _ => release.Task;

            var first = await browser.SendJsonAsync(HttpMethod.Post, "/api/rescan");
            var second = await browser.SendJsonAsync(HttpMethod.Post, "/api/rescan");
            release.SetResult();
            await app.Scans.Current;

            Assert.Equal(System.Net.HttpStatusCode.Accepted, first.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.Conflict, second.StatusCode);
        }
    }
}
