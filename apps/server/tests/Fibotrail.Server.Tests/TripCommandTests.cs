using Fibotrail.Immich.Tests;
using Fibotrail.Server.Cli;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fibotrail.Server.Tests;

public class TripCommandTests
{
    private static readonly string[] SanFranciscoWeek =
    [
        "add", "--start", "2005-07-31", "--end", "2005-08-06", "--name", "San Francisco, August 2005",
        "--place", "San Francisco", "--country", "United States", "--lat", "37.7749", "--lon", "-122.4194",
    ];

    private static async Task<(int ExitCode, string Output)> RunAsync(TestApp app, params string[] arguments)
    {
        var console = new ScriptedConsole();
        var exitCode = await app.WithServicesAsync(services => TripCommand.RunAsync(
            arguments,
            console,
            services.GetRequiredService<FibotrailDbContext>(),
            services.GetRequiredService<DetectionService>(),
            app.Settings,
            services.GetRequiredService<TimeProvider>()));
        return (exitCode, string.Join('\n', console.Lines));
    }

    /// <summary>A home, an Orlando trip in 2027, and a week of 2005 photos with no position. Home is confirmed.</summary>
    private static async Task<TestApp> StartAsync()
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 3, 30));
        app.Immich.Assets.AddRange(TripLibrary.Camera(new DateTime(2005, 7, 31, 19, 0, 0), 6, 60));
        await TripLibrary.ScanAndConfirmHomeAsync(app);
        return app;
    }

    private static Task<List<Trip>> TripsAsync(TestApp app) => TripLibrary.TripsAsync(app);

    [Fact]
    public async Task Adding_a_trip_makes_an_added_trip_with_every_photo_in_its_days()
    {
        await using var app = await StartAsync();
        Assert.Single(await TripsAsync(app));

        var (exitCode, output) = await RunAsync(app, SanFranciscoWeek);

        Assert.Equal(0, exitCode);
        var trip = (await TripsAsync(app)).Single(candidate => candidate.StartDate.Year == 2005);
        Assert.Equal(TripStatus.Added, trip.Status);
        Assert.Equal("San Francisco, August 2005", trip.Name);
        Assert.Equal(new DateOnly(2005, 8, 6), trip.EndDate);
        Assert.Equal(60, trip.PhotoCount);
        Assert.Equal("San Francisco", trip.MainPlace);
        var stop = Assert.Single(DetectionService.ReadStops(trip.StopsJson));
        Assert.Equal(37.7749, stop.Latitude, 4);
        Assert.Equal(-122.4194, stop.Longitude, 4);
        Assert.Contains("Added \"San Francisco, August 2005\"", output, StringComparison.Ordinal);
        Assert.Contains("7 days, 60 photos", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_trip_survives_a_new_detection_run_and_adds_no_suggestion()
    {
        await using var app = await StartAsync();
        await RunAsync(app, SanFranciscoWeek);

        var result = await app.WithServicesAsync(services => services.GetRequiredService<DetectionService>().RunAsync(CancellationToken.None));

        Assert.Equal(2, result.Trips);
        Assert.Equal(0, result.NewSuggestions);
        Assert.Equal(TripStatus.Added, (await TripsAsync(app)).Single(candidate => candidate.StartDate.Year == 2005).Status);
    }

    [Fact]
    public async Task Without_a_name_the_trip_gets_the_place_and_month()
    {
        await using var app = await StartAsync();

        await RunAsync(app, "add", "--start", "2005-07-31", "--end", "2005-08-06", "--place", "San Francisco");

        Assert.Equal("San Francisco, July 2005", (await TripsAsync(app)).Single(candidate => candidate.StartDate.Year == 2005).Name);
    }

    [Fact]
    public async Task A_manual_trip_replaces_a_suggestion_that_shares_its_days()
    {
        await using var app = await StartAsync();

        var (exitCode, _) = await RunAsync(app, "add", "--start", "2027-03-14", "--end", "2027-03-18", "--name", "Orlando by hand");

        Assert.Equal(0, exitCode);
        var trip = Assert.Single(await TripsAsync(app), candidate => candidate.StartDate.Year == 2027);
        Assert.Equal("Orlando by hand", trip.Name);
        Assert.Equal(TripStatus.Added, trip.Status);
    }

    [Fact]
    public async Task Removing_the_trip_deletes_it_and_its_decision()
    {
        await using var app = await StartAsync();
        await RunAsync(app, SanFranciscoWeek);
        var id = (await app.WithDbAsync(db => db.ManualTrips.AsNoTracking().SingleAsync())).Id;

        var (exitCode, _) = await RunAsync(app, "remove", id.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain(await TripsAsync(app), candidate => candidate.StartDate.Year == 2005);
        Assert.Equal(0, await app.WithDbAsync(db => db.ManualTrips.CountAsync()));
        Assert.Equal(0, await app.WithDbAsync(db => db.Decisions.CountAsync(decision => decision.StartDate.Year == 2005)));
    }

    [Fact]
    public async Task The_list_shows_each_trip_added_by_hand()
    {
        await using var app = await StartAsync();
        Assert.Contains("No trips added by hand yet.", (await RunAsync(app, "list")).Output, StringComparison.Ordinal);

        await RunAsync(app, SanFranciscoWeek);

        var output = (await RunAsync(app, "list")).Output;
        Assert.Contains("2005-07-31 to 2005-08-06", output, StringComparison.Ordinal);
        Assert.Contains("San Francisco, August 2005", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Days_with_no_photo_are_refused_and_change_nothing()
    {
        await using var app = await StartAsync();

        var (exitCode, output) = await RunAsync(app, "add", "--start", "2010-01-01", "--end", "2010-01-05");

        Assert.Equal(1, exitCode);
        Assert.Contains("No photo was taken", output, StringComparison.Ordinal);
        Assert.Equal(0, await app.WithDbAsync(db => db.ManualTrips.CountAsync()));
    }

    [Fact]
    public async Task Days_that_an_added_trip_covers_are_refused_and_change_nothing()
    {
        await using var app = await StartAsync();
        await RunAsync(app, SanFranciscoWeek);

        var (exitCode, output) = await RunAsync(app, "add", "--start", "2005-08-05", "--end", "2005-08-08");

        Assert.Equal(1, exitCode);
        Assert.Contains("already a trip in your library", output, StringComparison.Ordinal);
        Assert.Equal(1, await app.WithDbAsync(db => db.ManualTrips.CountAsync()));
    }

    [Theory]
    [InlineData]
    [InlineData("add")]
    [InlineData("add", "--start", "2005-07-31")]
    [InlineData("add", "--start", "2005-7-31", "--end", "2005-08-06")]
    [InlineData("add", "--start", "2005-08-06", "--end", "2005-07-31")]
    [InlineData("add", "--start", "2005-07-31", "--end", "2005-08-06", "--lat", "37.7")]
    [InlineData("add", "--start", "2005-07-31", "--end", "2005-08-06", "--lat", "137.7", "--lon", "10")]
    [InlineData("add", "--start", "2005-07-31", "--end", "2005-08-06", "--city", "Reno")]
    [InlineData("add", "--start", "2005-07-31", "--end", "2005-08-06", "--name")]
    [InlineData("remove", "abc")]
    [InlineData("list", "extra")]
    [InlineData("rename")]
    public async Task Anything_else_exits_with_2_and_changes_nothing(params string[] arguments)
    {
        await using var app = await StartAsync();

        var (exitCode, output) = await RunAsync(app, arguments);

        Assert.Equal(2, exitCode);
        Assert.NotEmpty(output);
        Assert.Equal(0, await app.WithDbAsync(db => db.ManualTrips.CountAsync()));
    }

    [Fact]
    public async Task Removing_a_trip_that_does_not_exist_exits_with_1()
    {
        await using var app = await StartAsync();

        var (exitCode, output) = await RunAsync(app, "remove", "7");

        Assert.Equal(1, exitCode);
        Assert.Contains("There is no trip 7", output, StringComparison.Ordinal);
    }
}
