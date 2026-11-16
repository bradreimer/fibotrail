using System.Net;
using System.Text.Json;

namespace Fibotrail.Server.Tests;

/// <summary>The Stats screen's data. See docs/specs/stats-screen.md and ADR-014.</summary>
public class StatsTests
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
    public async Task With_no_added_trip_the_summary_is_zero_and_every_continent_is_listed()
    {
        var (app, browser, _, _, _) = await StartAsync();
        await using var _ = app;

        var summary = await browser.GetJsonAsync("/api/stats/summary");

        Assert.Equal((0, 0, 0, 0), (summary.GetProperty("trips").GetInt32(), summary.GetProperty("countries").GetInt32(), summary.GetProperty("continents").GetInt32(), summary.GetProperty("daysAway").GetInt32()));
        Assert.Equal(7, summary.GetProperty("continentCards").GetArrayLength());
        Assert.True(summary.GetProperty("outlineCountries").GetInt32() > 200);
    }

    [Fact]
    public async Task Added_trips_count_and_the_continent_cards_add_up_to_the_countries_tile()
    {
        var (app, browser, orlando, paris, _) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, orlando, "add");
        await DecideAsync(browser, paris, "add");

        var summary = await browser.GetJsonAsync("/api/stats/summary");

        Assert.Equal((2, 2, 2), (summary.GetProperty("trips").GetInt32(), summary.GetProperty("countries").GetInt32(), summary.GetProperty("continents").GetInt32()));
        Assert.Equal(2, summary.GetProperty("continentCards").EnumerateArray().Sum(card => card.GetProperty("visited").GetInt32()));
        // Continents with visits come first.
        Assert.Equal(["Europe", "North America"], summary.GetProperty("continentCards").EnumerateArray().Take(2).Select(card => card.GetProperty("name").GetString()).Order());
    }

    [Fact]
    public async Task A_suggested_later_or_ignored_trip_changes_no_number()
    {
        var (app, browser, orlando, paris, lake) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, orlando, "add");
        await DecideAsync(browser, paris, "later");
        await DecideAsync(browser, lake, "ignore");

        var summary = await browser.GetJsonAsync("/api/stats/summary");

        Assert.Equal((1, 1), (summary.GetProperty("trips").GetInt32(), summary.GetProperty("countries").GetInt32()));
    }

    [Fact]
    public async Task A_continent_lists_its_visited_countries_with_trips_and_days()
    {
        var (app, browser, orlando, paris, _) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, orlando, "add");
        await DecideAsync(browser, paris, "add");

        var europe = await browser.GetJsonAsync("/api/stats/continents/europe");

        Assert.Equal("Europe", europe.GetProperty("name").GetString());
        var france = Assert.Single(europe.GetProperty("countries").EnumerateArray());
        Assert.Equal(("France", 1), (france.GetProperty("name").GetString(), france.GetProperty("trips").GetInt32()));
        Assert.True(france.GetProperty("days").GetInt32() >= 1);
    }

    [Fact]
    public async Task An_unknown_continent_or_an_unvisited_country_is_not_found()
    {
        var (app, browser, _, _, _) = await StartAsync();
        await using var _ = app;

        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/api/stats/continents/atlantis")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/api/stats/countries/752")).StatusCode);
    }

    [Fact]
    public async Task A_country_page_has_its_continent_first_visit_numbers_and_trips_newest_first()
    {
        var (app, browser, orlando, _, _) = await StartAsync();
        await using var _ = app;
        await DecideAsync(browser, orlando, "add");

        var usa = await browser.GetJsonAsync("/api/stats/countries/840");

        Assert.Equal(("North America", "2027-03-15", 1), (usa.GetProperty("continentName").GetString(), usa.GetProperty("firstVisit").GetString(), usa.GetProperty("trips").GetInt32()));
        Assert.Equal(orlando, Assert.Single(usa.GetProperty("tripList").EnumerateArray()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task The_view_routes_answer_with_no_added_trip()
    {
        var (app, browser, _, _, _) = await StartAsync();
        await using var _ = app;

        Assert.Equal(0, (await browser.GetJsonAsync("/api/stats/trips-per-year")).GetArrayLength());
        Assert.Equal(5, (await browser.GetJsonAsync("/api/stats/trip-length")).GetArrayLength());
        Assert.Equal(JsonValueKind.Null, (await browser.GetJsonAsync("/api/stats/records")).GetProperty("longestTrip").ValueKind);
    }

    [Fact]
    public async Task The_stats_routes_need_a_session()
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.NewBrowser().GetAsync("/api/stats/summary");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
