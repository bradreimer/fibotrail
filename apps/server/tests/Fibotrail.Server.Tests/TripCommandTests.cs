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
    [InlineData("rename", "1")]
    [InlineData("rename", "abc", "New name")]
    [InlineData("rename", "1", "New name", "extra")]
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

    [Fact]
    public async Task Renaming_a_trip_changes_its_name_and_a_new_detection_run_keeps_it()
    {
        await using var app = await StartAsync();
        var trip = Assert.Single(await TripsAsync(app));

        var (exitCode, output) = await RunAsync(app, "rename", trip.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), "  Ontario and Quebec ");

        Assert.Equal(0, exitCode);
        Assert.Contains($"from \"{trip.Name}\" to \"Ontario and Quebec\"", output, StringComparison.Ordinal);
        Assert.Equal("Ontario and Quebec", Assert.Single(await TripsAsync(app)).Name);

        await app.WithServicesAsync(services => services.GetRequiredService<DetectionService>().RunAsync(CancellationToken.None));

        Assert.Equal("Ontario and Quebec", Assert.Single(await TripsAsync(app)).Name);
    }

    [Fact]
    public async Task Renaming_a_trip_that_does_not_exist_exits_with_1_and_changes_nothing()
    {
        await using var app = await StartAsync();
        var before = Assert.Single(await TripsAsync(app)).Name;

        var (exitCode, output) = await RunAsync(app, "rename", "999", "Anything");

        Assert.Equal(1, exitCode);
        Assert.Contains("There is no trip 999", output, StringComparison.Ordinal);
        Assert.Equal(before, Assert.Single(await TripsAsync(app)).Name);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public async Task Renaming_to_an_empty_name_exits_with_1_and_changes_nothing(string name)
    {
        await using var app = await StartAsync();
        var trip = Assert.Single(await TripsAsync(app));

        var (exitCode, output) = await RunAsync(app, "rename", trip.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), name);

        Assert.Equal(1, exitCode);
        Assert.Contains("1 to 100 characters", output, StringComparison.Ordinal);
        Assert.Equal(trip.Name, Assert.Single(await TripsAsync(app)).Name);
    }

    [Fact]
    public async Task Renaming_to_a_name_over_100_characters_exits_with_1()
    {
        await using var app = await StartAsync();
        var trip = Assert.Single(await TripsAsync(app));

        var (exitCode, _) = await RunAsync(app, "rename", trip.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), new string('x', 101));

        Assert.Equal(1, exitCode);
        Assert.Equal(trip.Name, Assert.Single(await TripsAsync(app)).Name);
    }

    private const string StrayId = "00000000-0000-4000-8000-0000000fffff";

    /// <summary>The Orlando trip of March 2027, and a photo with no position taken a month after it.</summary>
    private static async Task<TestApp> StartWithStrayPhotoAsync()
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 3, 30));
        app.Immich.Assets.Add(TripLibrary.Asset(new DateTime(2027, 4, 20, 12, 0, 0), null, null, null, null, null) with { Id = Guid.Parse(StrayId) });
        await TripLibrary.ScanAndConfirmHomeAsync(app);
        return app;
    }

    private static string IdText(Trip trip) => trip.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(StrayId)]
    [InlineData("https://immich.example.home/photos/" + StrayId)]
    [InlineData("https://immich.example.home/photos/00000000-0000-4000-8000-0000000FFFFF?foo=bar")]
    public async Task Pinning_a_photo_puts_it_in_the_trip_and_a_new_detection_run_keeps_it(string photo)
    {
        await using var app = await StartWithStrayPhotoAsync();
        var trip = Assert.Single(await TripsAsync(app));
        Assert.DoesNotContain(trip.Photos, link => link.PhotoId == StrayId);

        var (exitCode, output) = await RunAsync(app, "pin", IdText(trip), photo);

        Assert.Equal(0, exitCode);
        Assert.Contains("Pinned photo " + StrayId + " to 2027-03-17", output, StringComparison.Ordinal);
        Assert.Contains($"The photo is in trip {trip.Id}", output, StringComparison.Ordinal);
        var after = Assert.Single(await TripsAsync(app));
        Assert.Equal(trip.PhotoCount + 1, after.PhotoCount);
        Assert.Contains(after.Photos, link => link.PhotoId == StrayId);

        await app.WithServicesAsync(services => services.GetRequiredService<DetectionService>().RunAsync(CancellationToken.None));

        Assert.Contains(Assert.Single(await TripsAsync(app)).Photos, link => link.PhotoId == StrayId);
    }

    [Fact]
    public async Task Unpinning_a_photo_sends_it_back_to_where_its_own_time_puts_it()
    {
        await using var app = await StartWithStrayPhotoAsync();
        var trip = Assert.Single(await TripsAsync(app));
        await RunAsync(app, "pin", IdText(trip), StrayId);

        var (exitCode, output) = await RunAsync(app, "unpin", StrayId);

        Assert.Equal(0, exitCode);
        Assert.Contains("Unpinned photo " + StrayId, output, StringComparison.Ordinal);
        Assert.Contains("in no trip now", output, StringComparison.Ordinal);
        Assert.Equal(trip.PhotoCount, Assert.Single(await TripsAsync(app)).PhotoCount);
        Assert.Equal(0, await app.WithDbAsync(db => db.PinnedPhotos.CountAsync()));
    }

    [Fact]
    public async Task Pinning_a_photo_again_moves_the_pin_and_leaves_one_row()
    {
        await using var app = await StartWithStrayPhotoAsync();
        var trip = Assert.Single(await TripsAsync(app));

        await RunAsync(app, "pin", IdText(trip), StrayId);
        var (exitCode, _) = await RunAsync(app, "pin", IdText(trip), StrayId);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, await app.WithDbAsync(db => db.PinnedPhotos.CountAsync()));
    }

    [Theory]
    [InlineData("999", StrayId, "There is no trip 999")]
    [InlineData("TRIP", "00000000-0000-4000-8000-00000000abcd", "Fibotrail has no photo")]
    [InlineData("TRIP", "not-a-photo", "Immich asset ID")]
    public async Task Pinning_that_cannot_be_done_exits_with_1_and_changes_nothing(string trip, string photo, string message)
    {
        await using var app = await StartWithStrayPhotoAsync();
        var existing = Assert.Single(await TripsAsync(app));

        var (exitCode, output) = await RunAsync(app, "pin", trip == "TRIP" ? IdText(existing) : trip, photo);

        Assert.Equal(1, exitCode);
        Assert.Contains(message, output, StringComparison.Ordinal);
        Assert.Equal(0, await app.WithDbAsync(db => db.PinnedPhotos.CountAsync()));
        Assert.Equal(existing.PhotoCount, Assert.Single(await TripsAsync(app)).PhotoCount);
    }

    [Theory]
    [InlineData(StrayId)]
    [InlineData("not-a-photo")]
    public async Task Unpinning_a_photo_that_is_not_pinned_exits_with_1(string photo)
    {
        await using var app = await StartWithStrayPhotoAsync();

        var (exitCode, _) = await RunAsync(app, "unpin", photo);

        Assert.Equal(1, exitCode);
    }

    [Theory]
    [InlineData("pin")]
    [InlineData("pin", "1")]
    [InlineData("pin", "abc", StrayId)]
    [InlineData("pin", "1", StrayId, "extra")]
    [InlineData("unpin")]
    [InlineData("unpin", StrayId, "extra")]
    public async Task Pin_and_unpin_with_the_wrong_arguments_exit_with_2(params string[] arguments)
    {
        await using var app = await StartWithStrayPhotoAsync();

        var (exitCode, output) = await RunAsync(app, arguments);

        Assert.Equal(2, exitCode);
        Assert.Contains("fibotrail trip pin", output, StringComparison.Ordinal);
        Assert.Equal(0, await app.WithDbAsync(db => db.PinnedPhotos.CountAsync()));
    }
}
