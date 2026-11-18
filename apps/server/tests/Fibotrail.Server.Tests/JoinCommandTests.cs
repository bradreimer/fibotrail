using Fibotrail.Immich.Tests;
using Fibotrail.Server.Cli;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fibotrail.Server.Tests;

public class JoinCommandTests
{
    private static async Task<(int ExitCode, string Output)> RunAsync(TestApp app, params string[] arguments)
    {
        var console = new ScriptedConsole();
        var exitCode = await app.WithServicesAsync(services => JoinCommand.RunAsync(
            arguments,
            console,
            services.GetRequiredService<FibotrailDbContext>(),
            services.GetRequiredService<DetectionService>(),
            app.Settings,
            services.GetRequiredService<TimeProvider>()));
        return (exitCode, string.Join('\n', console.Lines));
    }

    /// <summary>Two stays in Orlando, 6 days apart with no photos between them, so detection finds two trips. Home is confirmed.</summary>
    private static async Task<(TestApp App, Browser Browser)> StartWithTwoPartsAsync()
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 3, 30));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 23, 9, 0, 0), 3, 30));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        Assert.Equal(2, (await TripLibrary.TripsAsync(app)).Count);
        return (app, browser);
    }

    private static async Task AddTripAsync(Browser browser, int id) =>
        await browser.SendJsonAsync(HttpMethod.Post, $"/api/trips/{id}/decision", new { action = "add" });

    [Fact]
    public async Task Joining_two_trips_makes_one_trip_and_says_so()
    {
        var (app, _) = await StartWithTwoPartsAsync();
        await using var _ = app;

        var (exitCode, output) = await RunAsync(app, "2027-03-16", "2027-03-24");

        Assert.Equal(0, exitCode);
        var trip = Assert.Single(await TripLibrary.TripsAsync(app));
        Assert.Equal(new DateOnly(2027, 3, 15), trip.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 25), trip.EndDate);
        Assert.Equal(60, trip.PhotoCount);
        Assert.Contains("Joined into", output, StringComparison.Ordinal);
        Assert.Contains("11 days, 60 photos", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_order_of_the_two_days_does_not_matter()
    {
        var (app, _) = await StartWithTwoPartsAsync();
        await using var _ = app;

        var (exitCode, _) = await RunAsync(app, "2027-03-24", "2027-03-16");

        Assert.Equal(0, exitCode);
        Assert.Single(await TripLibrary.TripsAsync(app));
    }

    [Fact]
    public async Task An_added_trip_stays_added_with_its_name_after_the_join()
    {
        var (app, browser) = await StartWithTwoPartsAsync();
        await using var _ = app;
        var first = (await TripLibrary.TripsAsync(app)).First();
        await browser.SendJsonAsync(HttpMethod.Post, $"/api/trips/{first.Id}/decision", new { action = "add", name = "Spring break" });

        await RunAsync(app, "2027-03-16", "2027-03-24");

        var trip = Assert.Single(await TripLibrary.TripsAsync(app));
        Assert.Equal(TripStatus.Added, trip.Status);
        Assert.Equal("Spring break", trip.Name);
    }

    [Fact]
    public async Task When_only_the_later_trip_was_added_the_joined_trip_is_added()
    {
        var (app, browser) = await StartWithTwoPartsAsync();
        await using var _ = app;
        await AddTripAsync(browser, (await TripLibrary.TripsAsync(app)).Last().Id);

        var (_, output) = await RunAsync(app, "2027-03-16", "2027-03-24");

        Assert.Equal(TripStatus.Added, Assert.Single(await TripLibrary.TripsAsync(app)).Status);
        Assert.DoesNotContain("set aside", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_join_survives_a_new_detection_run_and_adds_no_suggestion()
    {
        var (app, browser) = await StartWithTwoPartsAsync();
        await using var _ = app;
        await AddTripAsync(browser, (await TripLibrary.TripsAsync(app)).First().Id);
        await RunAsync(app, "2027-03-16", "2027-03-24");

        var result = await app.WithServicesAsync(services => services.GetRequiredService<DetectionService>().RunAsync(CancellationToken.None));

        Assert.Equal(1, result.Trips);
        Assert.Equal(0, result.NewSuggestions);
        Assert.Equal(TripStatus.Added, Assert.Single(await TripLibrary.TripsAsync(app)).Status);
    }

    [Fact]
    public async Task Removing_the_join_splits_the_trips_again()
    {
        var (app, _) = await StartWithTwoPartsAsync();
        await using var _ = app;
        await RunAsync(app, "2027-03-16", "2027-03-24");
        var id = (await app.WithDbAsync(db => db.TripJoins.AsNoTracking().SingleAsync())).Id;

        var (exitCode, _) = await RunAsync(app, "--remove", id.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(0, exitCode);
        Assert.Equal(2, (await TripLibrary.TripsAsync(app)).Count);
        Assert.Equal(0, await app.WithDbAsync(db => db.TripJoins.CountAsync()));
    }

    [Fact]
    public async Task The_list_shows_each_join()
    {
        var (app, _) = await StartWithTwoPartsAsync();
        await using var _ = app;
        Assert.Contains("No joins yet.", (await RunAsync(app, "--list")).Output, StringComparison.Ordinal);

        await RunAsync(app, "2027-03-16", "2027-03-24");

        Assert.Contains("2027-03-15 to 2027-03-25", (await RunAsync(app, "--list")).Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_trip_from_photos_with_no_position_joins_a_trip_with_a_position()
    {
        // The case from issue 144: three days at a place, then days of photos that carry no position.
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 3, 30));
        app.Immich.Assets.AddRange(TripLibrary.Camera(new DateTime(2027, 3, 18, 0, 30, 0), 4, 200));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        var before = await TripLibrary.TripsAsync(app);
        Assert.Equal(2, before.Count);
        await AddTripAsync(browser, before.First().Id);

        var (exitCode, output) = await RunAsync(app, "2027-03-16", "2027-03-19");

        Assert.Equal(0, exitCode);
        Assert.Contains("Joined into", output, StringComparison.Ordinal);
        var trip = Assert.Single(await TripLibrary.TripsAsync(app));
        Assert.Equal(new DateOnly(2027, 3, 15), trip.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 21), trip.EndDate);
        Assert.Equal(230, trip.PhotoCount);
        Assert.Equal("Orlando", trip.MainPlace);
        Assert.Equal(TripStatus.Added, trip.Status);
        Assert.Equal(1, await app.WithDbAsync(db => db.TripJoins.CountAsync()));
    }

    [Fact]
    public async Task Two_days_in_one_trip_are_refused()
    {
        var (app, _) = await StartWithTwoPartsAsync();
        await using var _ = app;

        var (exitCode, output) = await RunAsync(app, "2027-03-15", "2027-03-17");

        Assert.Equal(1, exitCode);
        Assert.Contains("same trip", output, StringComparison.Ordinal);
        Assert.Equal(0, await app.WithDbAsync(db => db.TripJoins.CountAsync()));
    }

    [Fact]
    public async Task A_day_that_no_trip_covers_is_refused()
    {
        var (app, _) = await StartWithTwoPartsAsync();
        await using var _ = app;

        var (exitCode, output) = await RunAsync(app, "2027-03-16", "2027-03-20");

        Assert.Equal(1, exitCode);
        Assert.Contains("No trip covers 2027-03-20", output, StringComparison.Ordinal);
        Assert.Equal(0, await app.WithDbAsync(db => db.TripJoins.CountAsync()));
    }

    [Theory]
    [InlineData]
    [InlineData("2027-03-16")]
    [InlineData("March", "April")]
    [InlineData("2027-3-16", "2027-03-24")]
    [InlineData("--remove", "abc")]
    [InlineData("--list", "extra")]
    public async Task Anything_else_prints_the_usage_and_exits_with_2(params string[] arguments)
    {
        var (app, _) = await StartWithTwoPartsAsync();
        await using var _ = app;

        var (exitCode, output) = await RunAsync(app, arguments);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Removing_a_join_that_does_not_exist_exits_with_1()
    {
        var (app, _) = await StartWithTwoPartsAsync();
        await using var _ = app;

        var (exitCode, output) = await RunAsync(app, "--remove", "7");

        Assert.Equal(1, exitCode);
        Assert.Contains("There is no join 7", output, StringComparison.Ordinal);
    }
}
