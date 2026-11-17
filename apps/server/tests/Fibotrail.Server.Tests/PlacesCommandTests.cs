using System.Text.Json;
using Fibotrail.Server.Cli;
using Fibotrail.Server.Collections;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fibotrail.Server.Tests;

public class PlacesCommandTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<(int ExitCode, string Output)> RunAsync(TestApp app, params string[] arguments)
    {
        var console = new ScriptedConsole();
        var exitCode = await app.WithServicesAsync(services => PlacesCommand.RunAsync(
            arguments,
            console,
            services.GetRequiredService<FibotrailDbContext>(),
            app.Settings,
            CollectionCatalog.Empty,
            services.GetRequiredService<Fibotrail.Places.CountryFinder>()));
        return (exitCode, string.Join('\n', console.Lines));
    }

    private static async Task<TestApp> StartWithOneAddedTripAsync()
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 16, 9, 0, 0), 4, 60));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        var inbox = await browser.GetJsonAsync("/api/inbox");
        foreach (var card in inbox.GetProperty("suggestions").EnumerateArray())
        {
            await browser.SendJsonAsync(HttpMethod.Post, $"/api/trips/{card.GetProperty("id").GetInt32()}/decision", new { action = "add" });
        }

        return app;
    }

    // Seattle and Tacoma are about 41 km apart. Shibuya and Shinjuku are about 3 km apart.
    private static async Task SetStopsAsync(TestApp app, params StoredStop[] stops) =>
        await app.WithDbAsync(async db =>
        {
            var trip = await db.Trips.SingleAsync(trip => trip.Status == TripStatus.Added);
            trip.StopsJson = JsonSerializer.Serialize(stops, Json);
            return await db.SaveChangesAsync();
        });

    private static StoredStop Stop(string place, double latitude, double longitude, int photos) =>
        new(latitude, longitude, place, new DateTime(2027, 3, 16, 9, 0, 0), new DateTime(2027, 3, 17, 9, 0, 0), photos);

    [Fact]
    public async Task The_report_counts_cities_at_each_radius_and_lists_the_merges()
    {
        await using var app = await StartWithOneAddedTripAsync();
        await SetStopsAsync(app, Stop("Seattle", 47.61, -122.33, 50), Stop("Tacoma", 47.25, -122.44, 20), Stop("Shibuya", 35.66, 139.70, 80), Stop("Shinjuku", 35.69, 139.70, 30));

        var (exitCode, output) = await RunAsync(app);

        Assert.Equal(0, exitCode);
        Assert.Contains("Trips: 1\nStops: 4\n", output, StringComparison.Ordinal);
        Assert.Contains("10 km: 3 cities, 0 parks,", output, StringComparison.Ordinal);
        Assert.Contains("25 km: 3 cities, 0 parks,", output, StringComparison.Ordinal);
        Assert.Contains("50 km: 2 cities, 0 parks,", output, StringComparison.Ordinal);
        Assert.Contains("  Shibuya: Shibuya, Shinjuku", output, StringComparison.Ordinal);
        Assert.Contains("Cities that merge going from 25 km to 50 km:\n  Seattle: Seattle, Tacoma", output, StringComparison.Ordinal);
        Assert.Contains("Cities that merge going from 10 km to 25 km:\n  None.", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_output_has_no_coordinates()
    {
        await using var app = await StartWithOneAddedTripAsync();
        await SetStopsAsync(app, Stop("Seattle", 47.61, -122.33, 50));

        var (_, output) = await RunAsync(app);

        Assert.DoesNotContain("47.61", output, StringComparison.Ordinal);
        Assert.DoesNotContain("-122.33", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_command_changes_nothing_in_the_database()
    {
        await using var app = await StartWithOneAddedTripAsync();
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
    public async Task With_no_added_trips_the_command_says_so_and_exits_cleanly()
    {
        await using var app = await TestApp.StartAsync();

        var (exitCode, output) = await RunAsync(app);

        Assert.Equal(0, exitCode);
        Assert.Contains("No trips are added yet.", output, StringComparison.Ordinal);
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
