using System.Text.Json;
using Fibotrail.Immich.Tests;
using Fibotrail.Server.Cli;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fibotrail.Server.Tests;

public class StopsCommandTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<(int ExitCode, string Output)> RunAsync(TestApp app, params string[] arguments)
    {
        var console = new ScriptedConsole();
        var exitCode = await app.WithServicesAsync(services => StopsCommand.RunAsync(
            arguments,
            console,
            services.GetRequiredService<FibotrailDbContext>(),
            app.Settings));
        return (exitCode, string.Join('\n', console.Lines));
    }

    /// <summary>
    /// Three trips found from photos: Orlando (added, with a 3-photo layover first, then Orlando, then Miami),
    /// Lakeside (added, one stop), and Paris (left in the inbox, so the command skips it).
    /// </summary>
    private static async Task<TestApp> StartWithTripsAsync()
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 1, 3, 51.47, -0.45, "Hounslow", "England", "United Kingdom"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 16, 9, 0, 0), 4, 60));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 20, 9, 0, 0), 2, 20, 25.76, -80.19, "Miami", "Florida", "United States of America"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 7, 3, 9, 0, 0), 4, 60, 49.25, -120.35, "Lakeside", "British Columbia", "Canada"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 9, 1, 9, 0, 0), 6, 40, 48.85, 2.35, "Paris", "Île-de-France", "France"));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);

        var inbox = await browser.GetJsonAsync("/api/inbox");
        foreach (var card in inbox.GetProperty("suggestions").EnumerateArray())
        {
            if (card.GetProperty("place").GetString() != "Paris")
            {
                await browser.SendJsonAsync(HttpMethod.Post, $"/api/trips/{card.GetProperty("id").GetInt32()}/decision", new { action = "add" });
            }
        }

        return app;
    }

    private static async Task SetStopsAsync(TestApp app, string place, string json) =>
        await app.WithDbAsync(async db =>
        {
            var trip = await db.Trips.SingleAsync(trip => trip.MainPlace == place);
            trip.StopsJson = json;
            return await db.SaveChangesAsync();
        });

    private static string Stops(params StoredStop[] stops) => JsonSerializer.Serialize(stops, Json);

    private static StoredStop Stop(string? place, int photos, TimeSpan span) =>
        new(10.123456, 20.654321, place, new DateTime(2027, 3, 15, 9, 0, 0), new DateTime(2027, 3, 15, 9, 0, 0) + span, photos);

    [Fact]
    public async Task The_report_lists_each_added_trip_with_its_stops_and_the_totals()
    {
        await using var app = await StartWithTripsAsync();

        var (exitCode, output) = await RunAsync(app);

        Assert.Equal(0, exitCode);
        Assert.Contains("fewer than 5 photos, or whose photos span less than 3 hours.", output, StringComparison.Ordinal);
        Assert.Contains("Orlando, March 2027, 2027-03-15 to 2027-03-21", output, StringComparison.Ordinal);
        Assert.Contains("1. Hounslow (first), 3 photos, 8 h 00 min, FLAGGED: first or last with few photos", output, StringComparison.Ordinal);
        Assert.Contains("2. Orlando (middle), 60 photos, 3 d 8 h, not flagged", output, StringComparison.Ordinal);
        Assert.Contains("3. Miami (last), 20 photos, 1 d 8 h, not flagged", output, StringComparison.Ordinal);
        Assert.Contains("1. Lakeside (only stop), 60 photos, 3 d 8 h, not flagged", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Paris", output, StringComparison.Ordinal);
        Assert.Contains("Trips: 2\nStops: 4\nFlagged stops: 1\n", output, StringComparison.Ordinal);
        Assert.Contains("  First or last with fewer than 5 photos: 1\n  Photos span less than 3 hours: 0\n  Both: 0", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_rule_is_measured_at_its_edges_and_each_condition_is_counted()
    {
        await using var app = await StartWithTripsAsync();
        await SetStopsAsync(app, "Orlando", Stops(
            Stop("Few", 4, TimeSpan.FromHours(10)),
            Stop("Exactly three hours", 40, TimeSpan.FromHours(3)),
            Stop("Just under three hours", 40, TimeSpan.FromMinutes(179)),
            Stop("Few and short", 2, TimeSpan.FromMinutes(20)),
            Stop("Exactly five photos", 5, TimeSpan.FromHours(10))));

        var (exitCode, output) = await RunAsync(app);

        Assert.Equal(0, exitCode);
        Assert.Contains("1. Few (first), 4 photos, 10 h 00 min, FLAGGED: first or last with few photos\n", output + "\n", StringComparison.Ordinal);
        Assert.Contains("2. Exactly three hours (middle), 40 photos, 3 h 00 min, not flagged", output, StringComparison.Ordinal);
        Assert.Contains("3. Just under three hours (middle), 40 photos, 2 h 59 min, FLAGGED: short span", output, StringComparison.Ordinal);
        Assert.Contains("4. Few and short (middle), 2 photos, 0 h 20 min, FLAGGED: short span", output, StringComparison.Ordinal);
        Assert.Contains("5. Exactly five photos (last), 5 photos, 10 h 00 min, not flagged", output, StringComparison.Ordinal);
        Assert.Contains("Stops: 6\nFlagged stops: 3\n  First or last with fewer than 5 photos: 1\n  Photos span less than 3 hours: 2\n  Both: 0", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stop_that_holds_both_conditions_is_counted_under_each_and_under_both()
    {
        await using var app = await StartWithTripsAsync();
        await SetStopsAsync(app, "Orlando", Stops(Stop("Hounslow", 1, TimeSpan.Zero), Stop("Orlando", 60, TimeSpan.FromDays(3))));

        var (_, output) = await RunAsync(app);

        Assert.Contains("1. Hounslow (first), 1 photo, 0 h 00 min, FLAGGED: first or last with few photos and short span", output, StringComparison.Ordinal);
        Assert.Contains("Flagged stops: 1\n  First or last with fewer than 5 photos: 1\n  Photos span less than 3 hours: 1\n  Both: 1", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_trip_with_no_stops_or_unreadable_stops_is_reported_and_the_rest_still_counts()
    {
        await using var app = await StartWithTripsAsync();
        await SetStopsAsync(app, "Orlando", "[]");
        await SetStopsAsync(app, "Lakeside", "not json");

        var (exitCode, output) = await RunAsync(app);

        Assert.Equal(0, exitCode);
        Assert.Contains("  No stops.", output, StringComparison.Ordinal);
        Assert.Contains("  Its stored stops couldn't be read.", output, StringComparison.Ordinal);
        Assert.Contains("Trips: 2\nStops: 0\nFlagged stops: 0", output, StringComparison.Ordinal);
        Assert.Contains("Trips whose stops couldn't be read: 1", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stop_with_no_place_name_says_so()
    {
        await using var app = await StartWithTripsAsync();
        await SetStopsAsync(app, "Orlando", Stops(Stop(null, 30, TimeSpan.FromDays(1))));

        var (_, output) = await RunAsync(app);

        Assert.Contains("1. (no place name) (only stop), 30 photos, 1 d 0 h, not flagged", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_output_has_no_coordinates_photo_ids_or_immich_key()
    {
        await using var app = await StartWithTripsAsync();
        await SetStopsAsync(app, "Orlando", Stops(Stop("Hounslow", 3, TimeSpan.FromHours(1)), Stop("Orlando", 60, TimeSpan.FromDays(3))));
        var photoIds = await app.WithDbAsync(db => db.TripPhotos.Select(photo => photo.PhotoId).ToListAsync());
        Assert.NotEmpty(photoIds);

        var (_, output) = await RunAsync(app);

        Assert.DoesNotContain("10.123456", output, StringComparison.Ordinal);
        Assert.DoesNotContain("20.654321", output, StringComparison.Ordinal);
        Assert.DoesNotContain("28.4", output, StringComparison.Ordinal);
        Assert.DoesNotContain("-81.55", output, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeImmichServer.Key, output, StringComparison.Ordinal);
        Assert.All(photoIds, id => Assert.DoesNotContain(id, output, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_command_changes_nothing_in_the_database()
    {
        await using var app = await StartWithTripsAsync();
        async Task<string> SnapshotAsync() => await app.WithDbAsync(async db => JsonSerializer.Serialize(new
        {
            Trips = await db.Trips.AsNoTracking().OrderBy(trip => trip.Id).ToListAsync(),
            TripPhotos = await db.TripPhotos.AsNoTracking().OrderBy(photo => photo.TripId).ThenBy(photo => photo.PhotoId).ToListAsync(),
            Decisions = await db.Decisions.AsNoTracking().OrderBy(decision => decision.Id).ToListAsync(),
            Photos = await db.Photos.CountAsync(),
        }));
        var before = await SnapshotAsync();

        var (exitCode, _) = await RunAsync(app);

        Assert.Equal(0, exitCode);
        Assert.Equal(before, await SnapshotAsync());
    }

    [Fact]
    public async Task With_no_added_trips_the_command_says_to_add_one()
    {
        await using var app = await TestApp.StartAsync();

        var (exitCode, output) = await RunAsync(app);

        Assert.Equal(1, exitCode);
        Assert.Contains("No trips are added yet.", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_no_database_the_command_says_so_and_does_not_create_one()
    {
        await using var app = await TestApp.StartAsync();
        var emptyFolder = Directory.CreateTempSubdirectory("fibotrail-stops-").FullName;
        var settings = new ServerSettings(emptyFolder);

        var console = new ScriptedConsole();
        var exitCode = await app.WithServicesAsync(services => StopsCommand.RunAsync([], console, services.GetRequiredService<FibotrailDbContext>(), settings));

        Assert.Equal(1, exitCode);
        Assert.Contains("No database at", string.Join('\n', console.Lines), StringComparison.Ordinal);
        Assert.False(File.Exists(settings.DatabasePath));
        Directory.Delete(emptyFolder, recursive: true);
    }

    [Fact]
    public async Task The_command_takes_no_arguments()
    {
        await using var app = await TestApp.StartAsync();

        var (exitCode, output) = await RunAsync(app, "extra");

        Assert.Equal(2, exitCode);
        Assert.Contains("takes no arguments", output, StringComparison.Ordinal);
    }
}
