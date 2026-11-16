using System.Net;
using System.Text.Json;
using Fibotrail.Detection;
using Fibotrail.Detection.Parks;
using Fibotrail.Server.Collections;

namespace Fibotrail.Server.Tests;

/// <summary>Collections and the Stats Theme parks view, on a made-up park around the test Orlando trip. See docs/specs/collections.md.</summary>
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
        var places = detail.GetProperty("places").EnumerateArray().ToDictionary(place => place.GetProperty("id").GetString()!);
        Assert.InRange(places["near"].GetProperty("days").GetInt32(), 6, 7);
        Assert.Equal(1, places["near"].GetProperty("trips").GetInt32());
        Assert.Equal(trip, places["near"].GetProperty("tripList")[0].GetProperty("tripId").GetInt32());
        Assert.Equal(0, places["far"].GetProperty("days").GetInt32());
        Assert.Equal(("planned", 0), (places["coming"].GetProperty("status").GetString(), places["coming"].GetProperty("days").GetInt32()));
        Assert.Contains("OpenStreetMap", detail.GetProperty("attribution").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Collections_and_the_theme_parks_view_give_the_same_days_for_every_park()
    {
        var (app, browser, trip) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, trip, "add");

        var detail = await browser.GetJsonAsync("/api/collections/test-parks");
        var view = await browser.GetJsonAsync("/api/stats/theme-parks");

        var fromCollections = detail.GetProperty("places").EnumerateArray()
            .Where(place => place.GetProperty("status").GetString() == "open")
            .ToDictionary(place => place.GetProperty("id").GetString()!, place => place.GetProperty("days").GetInt32());
        var fromView = view.EnumerateArray().SelectMany(group => group.GetProperty("parks").EnumerateArray())
            .ToDictionary(park => park.GetProperty("id").GetString()!, park => park.GetProperty("days").GetInt32());
        Assert.Equal(fromCollections, fromView);
        Assert.True(fromView["near"] > 0);
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
}
