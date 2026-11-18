using System.Net;

namespace Fibotrail.Server.Tests;

/// <summary>The Insights screen's totals. See docs/specs/insights.md and ADR-019.</summary>
public class InsightsTests
{
    private static async Task<(TestApp App, Browser Browser, int Orlando, int Paris, int Lake)> StartAsync()
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 6, 60));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 6, 5, 9, 0, 0), 5, 50, 48.86, 2.35, "Paris", "Île-de-France", "France"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 7, 3, 9, 0, 0), 4, 60, 50.5, -121.5, "Lakeside", "British Columbia", "Canada"));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        var trips = await TripLibrary.TripsAsync(app);
        return (app, browser, trips[0].Id, trips[1].Id, trips[2].Id);
    }

    private static async Task DecideAsync(Browser browser, int id, string action) =>
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendJsonAsync(HttpMethod.Post, $"/api/trips/{id}/decision", new { action })).StatusCode);

    [Fact]
    public async Task With_no_added_trip_the_summary_is_zero()
    {
        var (app, browser, _, _, _) = await StartAsync();
        await using var _ = app;

        var summary = await browser.GetJsonAsync("/api/insights/summary");

        Assert.Equal((0, 0, 0, 0), (summary.GetProperty("trips").GetInt32(), summary.GetProperty("countries").GetInt32(), summary.GetProperty("continents").GetInt32(), summary.GetProperty("daysAway").GetInt32()));
        Assert.True(summary.GetProperty("outlineCountries").GetInt32() > 200);
    }

    [Fact]
    public async Task Added_trips_count()
    {
        var (app, browser, orlando, paris, _) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, orlando, "add");
        await DecideAsync(browser, paris, "add");

        var summary = await browser.GetJsonAsync("/api/insights/summary");

        Assert.Equal((2, 2, 2), (summary.GetProperty("trips").GetInt32(), summary.GetProperty("countries").GetInt32(), summary.GetProperty("continents").GetInt32()));
        Assert.Equal(2, summary.GetProperty("cities").GetInt32());
    }

    [Fact]
    public async Task A_suggested_later_or_ignored_trip_changes_no_number()
    {
        var (app, browser, orlando, paris, lake) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, orlando, "add");
        await DecideAsync(browser, paris, "later");
        await DecideAsync(browser, lake, "ignore");

        var summary = await browser.GetJsonAsync("/api/insights/summary");

        Assert.Equal((1, 1), (summary.GetProperty("trips").GetInt32(), summary.GetProperty("countries").GetInt32()));
    }

    [Fact]
    public async Task The_stats_routes_are_gone()
    {
        var (app, browser, orlando, _, _) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, orlando, "add");

        foreach (var path in new[] { "/api/stats/summary", "/api/stats/continents/north-america", "/api/stats/countries/840", "/api/stats/records", "/api/stats/trip-spiral", "/api/stats/countries/840/season-wheel" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync(path)).StatusCode);
        }
    }

    [Fact]
    public async Task With_no_collection_files_the_collections_are_empty()
    {
        var (app, browser, _, _, _) = await StartAsync();
        await using var _ = app;

        Assert.Equal(0, (await browser.GetJsonAsync("/api/collections")).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/api/collections/disney-parks")).StatusCode);
    }

    [Fact]
    public async Task The_insights_route_needs_a_session()
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.NewBrowser().GetAsync("/api/insights/summary");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
