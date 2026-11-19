using System.Globalization;
using System.Net;
using System.Text.Json;
using Fibotrail.Detection;
using Fibotrail.Detection.Parks;
using Fibotrail.Server.Cli;
using Fibotrail.Server.Collections;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Microsoft.Extensions.DependencyInjection;

namespace Fibotrail.Server.Tests;

/// <summary>Collections, on a made-up park around the test Orlando trip. See docs/specs/collections.md.</summary>
public class CollectionsTests
{
    private static readonly string[] FarOnly = ["far"];

    private static ParkPlace Park(string id, double south, double west, double size = 0.02) =>
        new(id, id.ToUpperInvariant(), "Test Resort", 3, 50,
            [[[new GeoPoint(south, west), new GeoPoint(south, west + size), new GeoPoint(south + size, west + size), new GeoPoint(south + size, west), new GeoPoint(south, west)]]]);

    // The test trip is at 28.4, -81.55.
    private static CollectionCatalog Catalog() => new(
    [
        new CollectionDefinition("test-parks", "Test parks", "Made up", 3, 50,
        [
            new CollectionPlace(Park("near", 28.39, -81.56), Planned: false),
            new CollectionPlace(Park("far", 10, 10), Planned: false),
            new CollectionPlace(Park("coming", 28.39, -81.56), Planned: true),
        ]),
    ]);

    private static async Task<(TestApp App, Browser Browser, int Trip)> StartAsync()
    {
        var app = await TestApp.StartAsync(collections: Catalog());
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 6, 60));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        var trips = await TripLibrary.TripsAsync(app);
        return (app, browser, trips[0].Id);
    }

    private static async Task DecideAsync(Browser browser, int id, string action) =>
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendJsonAsync(HttpMethod.Post, $"/api/trips/{id}/decision", new { action })).StatusCode);

    [Fact]
    public async Task An_added_trip_gives_a_park_its_days_and_trip_and_a_planned_place_stays_empty()
    {
        var (app, browser, trip) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, trip, "add");

        var list = await browser.GetJsonAsync("/api/collections");
        var detail = await browser.GetJsonAsync("/api/collections/test-parks");

        var summary = Assert.Single(list.EnumerateArray());
        Assert.Equal((1, 2), (summary.GetProperty("visited").GetInt32(), summary.GetProperty("total").GetInt32()));
        Assert.Equal(1, summary.GetProperty("trips").GetInt32());
        var resort = Assert.Single(detail.GetProperty("resorts").EnumerateArray());
        Assert.Equal(("Test Resort", 1, 2, 1), (resort.GetProperty("name").GetString(), resort.GetProperty("visited").GetInt32(), resort.GetProperty("total").GetInt32(), resort.GetProperty("trips").GetInt32()));
        Assert.Equal(summary.GetProperty("trips").GetInt32(), detail.GetProperty("summary").GetProperty("trips").GetInt32());
        var resortTrip = Assert.Single(resort.GetProperty("tripList").EnumerateArray());
        Assert.Equal(trip, resortTrip.GetProperty("tripId").GetInt32());
        Assert.Equal(["NEAR"], resortTrip.GetProperty("parks").EnumerateArray().Select(name => name.GetString()));
        var places = detail.GetProperty("places").EnumerateArray().ToDictionary(place => place.GetProperty("id").GetString()!);
        Assert.InRange(places["near"].GetProperty("days").GetInt32(), 6, 7);
        Assert.Equal(1, places["near"].GetProperty("trips").GetInt32());
        Assert.Equal(trip, places["near"].GetProperty("tripList")[0].GetProperty("tripId").GetInt32());
        Assert.Equal(0, places["far"].GetProperty("days").GetInt32());
        Assert.Equal(("planned", 0), (places["coming"].GetProperty("status").GetString(), places["coming"].GetProperty("days").GetInt32()));
        Assert.Contains("OpenStreetMap", detail.GetProperty("attribution").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_visited_place_has_its_last_visit_and_an_unvisited_or_planned_place_has_none()
    {
        var (app, browser, trip) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, trip, "add");

        var detail = await browser.GetJsonAsync("/api/collections/test-parks");

        var places = detail.GetProperty("places").EnumerateArray().ToDictionary(place => place.GetProperty("id").GetString()!);
        var first = DateOnly.Parse(places["near"].GetProperty("firstVisit").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var last = DateOnly.Parse(places["near"].GetProperty("lastVisit").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(new DateOnly(2027, 3, 15), first);
        Assert.Equal(places["near"].GetProperty("days").GetInt32() - 1, last.DayNumber - first.DayNumber);
        Assert.Equal(JsonValueKind.Null, places["far"].GetProperty("lastVisit").ValueKind);
        Assert.Equal(JsonValueKind.Null, places["coming"].GetProperty("lastVisit").ValueKind);
    }

    [Fact]
    public async Task A_trip_in_the_library_has_its_park_resort_and_collection_as_keywords()
    {
        var (app, browser, trip) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, trip, "add");

        var card = Assert.Single((await browser.GetJsonAsync("/api/trips")).EnumerateArray());
        var keywords = card.GetProperty("keywords").EnumerateArray().Select(word => word.GetString()).ToList();

        Assert.Contains("NEAR", keywords);
        Assert.Contains("Test Resort", keywords);
        Assert.Contains("Test parks", keywords);
        Assert.Contains("United States of America", keywords);
        Assert.DoesNotContain("FAR", keywords);
        Assert.DoesNotContain("COMING", keywords);
    }

    [Fact]
    public async Task A_suggested_or_ignored_trip_adds_no_park_day()
    {
        var (app, browser, trip) = await StartAsync();
        await using var _ = app;

        Assert.Equal(0, (await browser.GetJsonAsync("/api/collections"))[0].GetProperty("totalDays").GetInt32());
        await DecideAsync(browser, trip, "ignore");
        Assert.Equal(0, (await browser.GetJsonAsync("/api/collections"))[0].GetProperty("totalDays").GetInt32());
    }

    [Fact]
    public async Task A_photo_on_the_way_adds_no_park_day()
    {
        var (app, browser, trip) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, trip, "add");
        var photos = await browser.GetJsonAsync($"/api/trips/{trip}");
        var ids = photos.GetProperty("days").EnumerateArray().SelectMany(day => day.GetProperty("photos").EnumerateArray()).Select(photo => photo.GetProperty("id").GetString()!).ToList();
        foreach (var id in ids)
        {
            await browser.SendJsonAsync(HttpMethod.Put, $"/api/photos/{id}/on-the-way", new { onTheWay = true });
        }

        var summary = (await browser.GetJsonAsync("/api/collections"))[0];

        Assert.Equal(0, summary.GetProperty("totalDays").GetInt32());
    }

    [Fact]
    public async Task An_unknown_collection_is_not_found()
    {
        var (app, browser, _) = await StartAsync();
        await using var _ = app;

        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/api/collections/nope")).StatusCode);
    }

    /// <summary>Runs <c>fibotrail trip parks</c> against the test app, with its collections.</summary>
    private static async Task<(int ExitCode, string Output)> ParksAsync(TestApp app, params string[] arguments)
    {
        var console = new ScriptedConsole();
        var exitCode = await app.WithServicesAsync(services => TripCommand.RunAsync(
            ["parks", .. arguments],
            console,
            services.GetRequiredService<FibotrailDbContext>(),
            services.GetRequiredService<DetectionService>(),
            app.Settings,
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<CollectionCatalog>()));
        return (exitCode, string.Join('\n', console.Lines));
    }

    private static string Id(int trip) => trip.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public async Task A_park_you_mark_counts_the_trip_and_shows_on_the_trip_page()
    {
        var (app, browser, trip) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, trip, "add");

        var (exitCode, output) = await ParksAsync(app, Id(trip), "far");

        Assert.Equal(0, exitCode);
        Assert.Contains("near: NEAR, from photos", output, StringComparison.Ordinal);
        Assert.Contains("far: FAR, marked by you", output, StringComparison.Ordinal);
        var parks = (await browser.GetJsonAsync($"/api/trips/{trip}")).GetProperty("parks").EnumerateArray()
            .ToDictionary(park => park.GetProperty("id").GetString()!, park => park.GetProperty("source").GetString());
        Assert.Equal(new Dictionary<string, string?> { ["near"] = "photos", ["far"] = "you" }, parks);
        var detail = await browser.GetJsonAsync("/api/collections/test-parks");
        var far = detail.GetProperty("places").EnumerateArray().Single(place => place.GetProperty("id").GetString() == "far");
        Assert.Equal((0, 1), (far.GetProperty("days").GetInt32(), far.GetProperty("trips").GetInt32()));
        Assert.True(far.GetProperty("tripList")[0].GetProperty("marked").GetBoolean());
        Assert.Equal(2, (await browser.GetJsonAsync("/api/collections"))[0].GetProperty("visited").GetInt32());
    }

    [Fact]
    public async Task Showing_the_parks_of_a_trip_lists_each_with_its_source()
    {
        var (app, _, trip) = await StartAsync();
        await using var _app = app;

        var (exitCode, output) = await ParksAsync(app, Id(trip));

        Assert.Equal(0, exitCode);
        Assert.Contains("near: NEAR, from photos", output, StringComparison.Ordinal);
        Assert.DoesNotContain("far", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clearing_the_marked_parks_takes_the_trip_out_of_the_park_and_keeps_parks_from_photos()
    {
        var (app, browser, trip) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, trip, "add");
        await ParksAsync(app, Id(trip), "far", "near");

        var (exitCode, output) = await ParksAsync(app, Id(trip), "--clear");

        Assert.Equal(0, exitCode);
        Assert.Contains("near: NEAR, from photos", output, StringComparison.Ordinal);
        Assert.Equal(1, (await browser.GetJsonAsync("/api/collections"))[0].GetProperty("visited").GetInt32());
        Assert.Equal(1, (await browser.GetJsonAsync("/api/collections"))[0].GetProperty("trips").GetInt32());
    }

    [Fact]
    public async Task Marking_a_park_that_photos_place_adds_no_second_trip()
    {
        var (app, browser, trip) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, trip, "add");

        await ParksAsync(app, Id(trip), "near");

        var near = (await browser.GetJsonAsync("/api/collections/test-parks")).GetProperty("places").EnumerateArray()
            .Single(place => place.GetProperty("id").GetString() == "near");
        Assert.Equal(1, near.GetProperty("trips").GetInt32());
    }

    [Theory]
    [InlineData("coming")]
    [InlineData("nowhere")]
    public async Task A_planned_or_unknown_park_is_refused_and_nothing_is_saved(string park)
    {
        var (app, browser, trip) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, trip, "add");
        await ParksAsync(app, Id(trip), "far");

        var (exitCode, output) = await ParksAsync(app, Id(trip), "near", park);

        Assert.Equal(1, exitCode);
        Assert.Contains($"\"{park}\" isn't an open park", output, StringComparison.Ordinal);
        Assert.Contains("fibotrail trip parks --list", output, StringComparison.Ordinal);
        var sources = (await browser.GetJsonAsync($"/api/trips/{trip}")).GetProperty("parks").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()).ToList();
        Assert.Equal(["near", "far"], sources);
    }

    [Fact]
    public async Task An_unknown_trip_is_refused()
    {
        var (app, _, _) = await StartAsync();
        await using var _app = app;

        Assert.Equal(1, (await ParksAsync(app, "9999", "far")).ExitCode);
        Assert.Equal(1, (await ParksAsync(app, "9999")).ExitCode);
    }

    [Fact]
    public async Task The_park_list_holds_the_open_parks_only()
    {
        var (app, _, _) = await StartAsync();
        await using var _app = app;

        var (exitCode, output) = await ParksAsync(app, "--list");

        Assert.Equal(0, exitCode);
        Assert.Equal(["near: NEAR, Test Resort, Test parks", "far: FAR, Test Resort, Test parks"], output.Split('\n'));
    }

    [Theory]
    [InlineData("--clear", "far")]
    [InlineData("--all")]
    public async Task Other_options_print_the_usage(params string[] extra)
    {
        var (app, _, trip) = await StartAsync();
        await using var _app = app;

        var (exitCode, output) = await ParksAsync(app, [Id(trip), .. extra]);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_route_marks_a_park()
    {
        var (app, browser, trip) = await StartAsync();
        await using var _ = app;

        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/api/parks")).StatusCode);
        var put = await browser.SendJsonAsync(HttpMethod.Put, $"/api/trips/{trip}/parks", new { parks = FarOnly });
        Assert.True(put.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, put.StatusCode.ToString());
    }
}
