using System.Net;
using System.Text.Json;
using Fibotrail.Detection;
using Fibotrail.Detection.Parks;
using Fibotrail.Server.Collections;

namespace Fibotrail.Server.Tests;

/// <summary>Collections, on a made-up park around the test Orlando trip. See docs/specs/collections.md.</summary>
public class CollectionsTests
{
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
        var places = detail.GetProperty("places").EnumerateArray().ToDictionary(place => place.GetProperty("id").GetString()!);
        Assert.InRange(places["near"].GetProperty("days").GetInt32(), 6, 7);
        Assert.Equal(1, places["near"].GetProperty("trips").GetInt32());
        Assert.Equal(trip, places["near"].GetProperty("tripList")[0].GetProperty("tripId").GetInt32());
        Assert.Equal(0, places["far"].GetProperty("days").GetInt32());
        Assert.Equal(("planned", 0), (places["coming"].GetProperty("status").GetString(), places["coming"].GetProperty("days").GetInt32()));
        Assert.Contains("OpenStreetMap", detail.GetProperty("attribution").GetString(), StringComparison.Ordinal);
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

    private static Task<HttpResponseMessage> MarkAsync(Browser browser, string path, params string[] parks) =>
        browser.SendJsonAsync(HttpMethod.Put, path, new { parks });

    [Fact]
    public async Task A_park_you_mark_counts_the_trip_and_shows_on_the_trip_page()
    {
        var (app, browser, trip) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, trip, "add");

        var response = await MarkAsync(browser, $"/api/trips/{trip}/parks", "far");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
    public async Task Clearing_the_marked_parks_takes_the_trip_out_of_the_park()
    {
        var (app, browser, trip) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, trip, "add");
        await MarkAsync(browser, $"/api/trips/{trip}/parks", "far");

        await MarkAsync(browser, $"/api/trips/{trip}/parks");

        Assert.Equal(1, (await browser.GetJsonAsync("/api/collections"))[0].GetProperty("visited").GetInt32());
    }

    [Fact]
    public async Task A_planned_or_unknown_park_cannot_be_marked()
    {
        var (app, browser, trip) = await StartAsync();
        await using var _ = app;

        Assert.Equal(HttpStatusCode.BadRequest, (await MarkAsync(browser, $"/api/trips/{trip}/parks", "coming")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await MarkAsync(browser, $"/api/trips/{trip}/parks", "nowhere")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await MarkAsync(browser, "/api/trips/9999/parks", "far")).StatusCode);
    }

    [Fact]
    public async Task The_parks_list_holds_the_open_parks_only()
    {
        var (app, browser, _) = await StartAsync();
        await using var _app = app;

        var ids = (await browser.GetJsonAsync("/api/parks")).EnumerateArray().Select(park => park.GetProperty("id").GetString()).ToList();

        Assert.Equal(["near", "far"], ids);
    }
}
