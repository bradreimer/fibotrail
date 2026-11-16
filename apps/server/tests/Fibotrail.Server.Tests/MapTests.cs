using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Tests;

/// <summary>The life map's data: which countries the added trips visited. See docs/specs/life-map.md and ADR-011.</summary>
public class MapTests
{
    private static readonly DateTime March15 = new(2027, 3, 15, 9, 0, 0);
    private static readonly DateTime March14 = new(2027, 3, 14, 20, 0, 0);

    /// <summary>Orlando (March), Paris (June), and a Canadian lake (July), none added yet.</summary>
    private static async Task<(TestApp App, Browser Browser, int Orlando, int Paris, int Lake)> StartAsync()
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15, 6, 60));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 6, 5, 9, 0, 0), 5, 50, 48.86, 2.35, "Paris", "Île-de-France", "France"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 7, 3, 9, 0, 0), 4, 60, 50.5, -121.5, "Lakeside", "British Columbia", "Canada"));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        var trips = await TripLibrary.TripsAsync(app);
        return (app, browser, trips[0].Id, trips[1].Id, trips[2].Id);
    }

    private static async Task DecideAsync(Browser browser, int id, string action) =>
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendJsonAsync(HttpMethod.Post, $"/api/trips/{id}/decision", new { action })).StatusCode);

    private static async Task<JsonElement> MapAsync(Browser browser) => await browser.GetJsonAsync("/api/map");

    private static List<string?> Names(JsonElement map) =>
        [.. map.GetProperty("countries").EnumerateArray().Select(country => country.GetProperty("name").GetString())];

    [Fact]
    public async Task With_no_added_trip_there_is_no_country_route_or_total()
    {
        var (app, browser, _, _, _) = await StartAsync();
        await using var _ = app;

        var map = await MapAsync(browser);

        Assert.Equal(0, map.GetProperty("countries").GetArrayLength());
        Assert.Equal(0, map.GetProperty("routes").GetArrayLength());
        var totals = map.GetProperty("totals");
        Assert.Equal((0, 0, 0, 0), (totals.GetProperty("countries").GetInt32(), totals.GetProperty("trips").GetInt32(), totals.GetProperty("daysAway").GetInt32(), (int)totals.GetProperty("distanceKm").GetDouble()));
    }

    [Fact]
    public async Task Added_trips_fill_their_countries_with_their_trips_and_the_date_of_the_first_visit()
    {
        var (app, browser, orlando, paris, _) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, orlando, "add");
        await DecideAsync(browser, paris, "add");

        var map = await MapAsync(browser);

        // Earliest first visit first: the United States in March, then France in June.
        Assert.Equal(["United States of America", "France"], Names(map));
        var usa = map.GetProperty("countries")[0];
        Assert.Equal("840", usa.GetProperty("id").GetString());
        Assert.Equal("2027-03-15", usa.GetProperty("firstVisit").GetString());
        var trip = Assert.Single(usa.GetProperty("trips").EnumerateArray());
        Assert.Equal((orlando, "2027-03-15"), (trip.GetProperty("id").GetInt32(), trip.GetProperty("startDate").GetString()));
    }

    [Fact]
    public async Task A_suggested_later_or_ignored_trip_never_fills_a_country()
    {
        var (app, browser, orlando, paris, lake) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, orlando, "add");
        await DecideAsync(browser, paris, "later");
        await DecideAsync(browser, lake, "ignore");

        var map = await MapAsync(browser);

        Assert.Equal(["United States of America"], Names(map));
        Assert.Equal(1, map.GetProperty("totals").GetProperty("trips").GetInt32());
        Assert.Equal(1, map.GetProperty("routes").GetArrayLength());
    }

    [Fact]
    public async Task The_totals_are_the_sums_of_the_added_trips_as_their_pages_show_them()
    {
        var (app, browser, orlando, paris, _) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, orlando, "add");
        await DecideAsync(browser, paris, "add");
        var trips = await TripLibrary.TripsAsync(app);
        var added = trips.Where(trip => trip.Id == orlando || trip.Id == paris).ToList();

        var totals = (await MapAsync(browser)).GetProperty("totals");

        Assert.Equal(2, totals.GetProperty("countries").GetInt32());
        Assert.Equal(2, totals.GetProperty("trips").GetInt32());
        Assert.Equal(added.Sum(trip => trip.Days), totals.GetProperty("daysAway").GetInt32());
        Assert.Equal(Math.Round(added.Sum(trip => trip.DistanceKm), 1), totals.GetProperty("distanceKm").GetDouble());
    }

    [Fact]
    public async Task Each_added_trip_has_a_route_of_its_stops_in_order()
    {
        var (app, browser, orlando, _, _) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, orlando, "add");

        var route = Assert.Single((await MapAsync(browser)).GetProperty("routes").EnumerateArray());

        Assert.Equal(orlando, route.GetProperty("tripId").GetInt32());
        var point = Assert.Single(route.GetProperty("points").EnumerateArray());
        Assert.Equal(28.4, Math.Round(point.GetProperty("latitude").GetDouble(), 1));
    }

    [Fact]
    public async Task A_country_where_a_trip_has_only_photos_on_the_way_does_not_fill_in()
    {
        var app = await TestApp.StartAsync();
        await using var _ = app;
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        // A London layover on the first evening, then Orlando.
        app.Immich.Assets.AddRange(TripLibrary.Trip(March14, 1, 6, 51.47, -0.45, "Hounslow", "England", "United Kingdom"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15, 6, 60));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        var tripId = (await TripLibrary.TripsAsync(app))[0].Id;
        await DecideAsync(browser, tripId, "add");
        Assert.Equal(["United Kingdom", "United States of America"], Names(await MapAsync(browser)).Order(StringComparer.Ordinal));

        var layover = await app.WithDbAsync(db => db.Photos.AsNoTracking().Where(photo => photo.City == "Hounslow").Select(photo => photo.Id).ToListAsync());
        foreach (var id in layover)
        {
            Assert.Equal(HttpStatusCode.OK, (await browser.SendJsonAsync(HttpMethod.Put, $"/api/photos/{id}/on-the-way", new { onTheWay = true })).StatusCode);
        }

        Assert.Equal(["United States of America"], Names(await MapAsync(browser)));
    }

    [Fact]
    public async Task The_route_needs_a_session()
    {
        var (app, _, _, _, _) = await StartAsync();
        await using var _ = app;

        var response = await app.NewBrowser().GetAsync("/api/map", asPage: false);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
