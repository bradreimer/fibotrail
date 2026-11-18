using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fibotrail.Detection;
using Fibotrail.Detection.Parks;
using Fibotrail.Server.Collections;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fibotrail.Server.Tests;

/// <summary>The MCP server on the real app. See ADR-018 and docs/specs/mcp-server.md.</summary>
public class McpTests
{
    private const string Token = "a-test-token-that-is-at-least-32-chars-long";

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

    private static async Task<(TestApp App, Browser Browser, int Trip)> StartWithTripAsync(string? token = Token, ILoggerProvider? logging = null)
    {
        var app = await TestApp.StartAsync(collections: Catalog(), mcpToken: token, logging: logging);
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 6, 60));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        var trips = await TripLibrary.TripsAsync(app);
        return (app, browser, trips[0].Id);
    }

    private static async Task AddAsync(Browser browser, int id) =>
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendJsonAsync(HttpMethod.Post, $"/api/trips/{id}/decision", new { action = "add" })).StatusCode);

    private static Task<HttpResponseMessage> PostAsync(TestApp app, string body, string? token = Token, string? origin = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (origin is not null) request.Headers.Add("Origin", origin);
        return app.App.GetTestClient().SendAsync(request);
    }

    private static async Task<JsonElement> CallToolAsync(TestApp app, string tool, string arguments = "{}")
    {
        var response = await PostAsync(app, $$$"""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"{{{tool}}}","arguments":{{{arguments}}}}}""");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = document.RootElement.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean(), result.ToString());
        return result.GetProperty("structuredContent").Clone();
    }

    private const string ToolsList = """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""";

    [Fact]
    public async Task With_no_token_set_the_route_answers_404()
    {
        await using var app = await TestApp.StartAsync(mcpToken: null);

        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(app, ToolsList)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(app, ToolsList, token: null)).StatusCode);
        Assert.False(app.Settings.McpEnabled);
    }

    [Fact]
    public async Task A_missing_or_wrong_token_answers_401_with_no_detail()
    {
        await using var app = await TestApp.StartAsync(mcpToken: Token);

        foreach (var offered in new string?[] { null, "wrong", Token + "x", Token[..^1], new string('a', 4000) })
        {
            var response = await PostAsync(app, ToolsList, offered);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
            Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_right_token_lists_the_seven_tools()
    {
        await using var app = await TestApp.StartAsync(mcpToken: Token);

        var response = await PostAsync(app, ToolsList);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(7, document.RootElement.GetProperty("result").GetProperty("tools").GetArrayLength());
    }

    [Fact]
    public async Task The_mcp_screen_has_its_own_address_so_a_reload_reaches_the_web_app()
    {
        await using var app = await TestApp.StartAsync(mcpToken: Token);
        var browser = await app.SignedInBrowserAsync();

        var page = await browser.GetAsync("/mcp-setup");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("id=\"root\"", await page.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_notification_gets_202()
    {
        await using var app = await TestApp.StartAsync(mcpToken: Token);

        var response = await PostAsync(app, """{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Only_post_is_allowed()
    {
        await using var app = await TestApp.StartAsync(mcpToken: Token);
        var client = app.App.GetTestClient();

        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Delete, HttpMethod.Put })
        {
            var request = new HttpRequestMessage(method, "/mcp");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_web_session_cant_call_mcp_and_the_token_cant_call_the_api()
    {
        await using var app = await TestApp.StartAsync(mcpToken: Token);
        var browser = await app.SignedInBrowserAsync();

        // The browser has a valid session cookie and no bearer token.
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(ToolsList, Encoding.UTF8, "application/json") };
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.SendAsync(request)).StatusCode);

        // The token alone opens no /api route.
        var api = new HttpRequestMessage(HttpMethod.Get, "/api/stats/summary");
        api.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.App.GetTestClient().SendAsync(api)).StatusCode);
    }

    [Fact]
    public async Task Another_sites_origin_is_refused_and_this_servers_own_is_allowed()
    {
        await using var app = await TestApp.StartAsync(mcpToken: Token);

        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(app, ToolsList, origin: "https://evil.example.com")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(app, ToolsList, origin: "http://localhost")).StatusCode);
    }

    [Fact]
    public async Task The_sixty_first_request_in_a_minute_answers_429_and_a_new_minute_starts_clean()
    {
        await using var app = await TestApp.StartAsync(mcpToken: Token);

        for (var count = 0; count < McpRateLimiterLimit; count++)
        {
            Assert.Equal(HttpStatusCode.OK, (await PostAsync(app, ToolsList)).StatusCode);
        }

        var limited = await PostAsync(app, ToolsList);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);

        app.Time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(app, ToolsList)).StatusCode);
    }

    private const int McpRateLimiterLimit = Mcp.McpRateLimiter.RequestsPerMinute;

    [Fact]
    public async Task A_wrong_token_doesnt_use_up_the_limit()
    {
        await using var app = await TestApp.StartAsync(mcpToken: Token);

        for (var count = 0; count < McpRateLimiterLimit + 5; count++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(app, ToolsList, "wrong")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(app, ToolsList)).StatusCode);
    }

    [Fact]
    public async Task Disney_style_numbers_match_the_collections_screen()
    {
        var (app, browser, trip) = await StartWithTripAsync();
        await using var _ = app;
        await AddAsync(browser, trip);

        var screen = (await browser.GetJsonAsync("/api/collections/test-parks"));
        var mcp = await CallToolAsync(app, "collection_visits", """{"collection":"test-parks"}""");
        var list = await CallToolAsync(app, "list_collections");

        var summary = screen.GetProperty("summary");
        Assert.Equal(summary.GetProperty("trips").GetInt32(), mcp.GetProperty("trips").GetInt32());
        Assert.Equal(summary.GetProperty("totalDays").GetInt32(), mcp.GetProperty("days").GetInt32());
        Assert.Equal(summary.GetProperty("visited").GetInt32(), mcp.GetProperty("parksVisited").GetInt32());
        Assert.Equal(summary.GetProperty("total").GetInt32(), mcp.GetProperty("parksTotal").GetInt32());
        Assert.Equal(1, mcp.GetProperty("trips").GetInt32());
        Assert.Equal(summary.GetProperty("trips").GetInt32(), list.GetProperty("collections")[0].GetProperty("trips").GetInt32());
        var park = Assert.Single(mcp.GetProperty("parks").EnumerateArray());
        var screenPark = screen.GetProperty("places").EnumerateArray().Single(place => place.GetProperty("id").GetString() == "near");
        Assert.Equal(screenPark.GetProperty("days").GetInt32(), park.GetProperty("days").GetInt32());
    }

    [Fact]
    public async Task Countries_and_days_match_the_stats_summary()
    {
        var (app, browser, trip) = await StartWithTripAsync();
        await using var _ = app;
        await AddAsync(browser, trip);

        var summary = await browser.GetJsonAsync("/api/stats/summary");
        var countries = await CallToolAsync(app, "list_countries");
        var days = await CallToolAsync(app, "days_away", """{"by":"year"}""");

        Assert.Equal(summary.GetProperty("countries").GetInt32(), countries.GetProperty("visited").GetInt32());
        Assert.Equal(summary.GetProperty("daysAway").GetInt32(), days.GetProperty("totalDays").GetInt32());
    }

    [Fact]
    public async Task A_suggested_or_ignored_trip_changes_no_number()
    {
        var (app, browser, trip) = await StartWithTripAsync();
        await using var _ = app;

        Assert.Equal(0, (await CallToolAsync(app, "list_trips")).GetProperty("total").GetInt32());
        Assert.Equal(0, (await CallToolAsync(app, "collection_visits", """{"collection":"test-parks"}""")).GetProperty("trips").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendJsonAsync(HttpMethod.Post, $"/api/trips/{trip}/decision", new { action = "ignore" })).StatusCode);

        Assert.Equal(0, (await CallToolAsync(app, "list_trips")).GetProperty("total").GetInt32());
        Assert.Equal(0, (await CallToolAsync(app, "days_away", """{"by":"year"}""")).GetProperty("totalDays").GetInt32());
    }

    [Fact]
    public async Task A_trip_name_and_a_position_never_appear_in_a_result()
    {
        var (app, browser, trip) = await StartWithTripAsync();
        await using var _ = app;
        await AddAsync(browser, trip);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendJsonAsync(HttpMethod.Put, $"/api/trips/{trip}", new { name = "Mom's 70th secret" })).StatusCode);

        var texts = new List<string>();
        foreach (var (tool, arguments) in new[] { ("list_trips", "{}"), ("get_trip", $$"""{"id":{{trip}}}"""), ("collection_visits", """{"collection":"test-parks"}"""), ("list_countries", "{}") })
        {
            texts.Add((await CallToolAsync(app, tool, arguments)).GetRawText());
        }

        var all = string.Join('\n', texts);
        Assert.DoesNotContain("Mom", all, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", all, StringComparison.Ordinal);
        Assert.DoesNotContain("28.4", all, StringComparison.Ordinal);
        Assert.DoesNotContain("-81.5", all, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_token_never_reaches_a_log_or_a_response_and_a_failed_call_logs_only_argument_names()
    {
        var logs = new CollectingLoggerProvider();
        var (app, _, _) = await StartWithTripAsync(logging: logs);
        await using var _ = app;

        var good = await PostAsync(app, """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_collections","arguments":{}}}""");
        var bad = await PostAsync(app, """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"collection_visits","arguments":{"collection":"private-value-xyz"}}}""");
        var wrong = await PostAsync(app, ToolsList, "an-incorrect-token-that-is-long-enough-to-look-real");

        var everything = string.Join('\n', logs.Lines) + await good.Content.ReadAsStringAsync() + await bad.Content.ReadAsStringAsync() + await wrong.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Token, everything, StringComparison.Ordinal);
        Assert.DoesNotContain("an-incorrect-token", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("private-value-xyz", string.Join('\n', logs.Lines), StringComparison.Ordinal);
        Assert.Contains(logs.Lines, line => line.Contains("MCP tool list_collections: ok", StringComparison.Ordinal));
        Assert.Contains(logs.Lines, line => line.Contains("MCP tool collection_visits failed. Argument names: collection", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_status_route_says_whether_it_is_on_and_never_returns_the_token()
    {
        await using var on = await TestApp.StartAsync(mcpToken: Token);
        var onStatus = await (await on.SignedInBrowserAsync()).GetJsonAsync("/api/mcp/status");

        Assert.True(onStatus.GetProperty("enabled").GetBoolean());
        Assert.Equal(7, onStatus.GetProperty("tools").GetArrayLength());
        Assert.DoesNotContain(Token, onStatus.GetRawText(), StringComparison.Ordinal);

        await using var off = await TestApp.StartAsync(mcpToken: null);
        var offStatus = await (await off.SignedInBrowserAsync()).GetJsonAsync("/api/mcp/status");
        Assert.False(offStatus.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task The_status_route_needs_a_session()
    {
        await using var app = await TestApp.StartAsync(mcpToken: Token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.NewBrowser().GetAsync("/api/mcp/status", asPage: false)).StatusCode);
    }

    private static IConfiguration Configuration(params (string Name, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(pair => pair.Name, pair => pair.Value)).Build();

    [Fact]
    public void A_token_shorter_than_32_characters_stops_startup_and_the_message_hides_the_value()
    {
        var short31 = new string('s', 31);

        var (settings, problems) = ServerSettings.Load(
            Configuration(("IMMICH_URL", "http://immich_server:2283"), ("IMMICH_API_KEY", "abc"), ("MCP_TOKEN", short31)),
            requireImmich: true);

        Assert.Null(settings);
        var problem = Assert.Single(problems);
        Assert.Contains("MCP_TOKEN", problem, StringComparison.Ordinal);
        Assert.Contains("32", problem, StringComparison.Ordinal);
        Assert.DoesNotContain(short31, problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_token_turns_the_server_off(string? value)
    {
        var (settings, problems) = ServerSettings.Load(
            Configuration(("IMMICH_URL", "http://immich_server:2283"), ("IMMICH_API_KEY", "abc"), ("MCP_TOKEN", value)),
            requireImmich: true);

        Assert.Empty(problems);
        Assert.False(settings!.McpEnabled);
        Assert.Null(settings.McpToken);
    }

    [Fact]
    public void A_32_character_token_turns_the_server_on_and_the_settings_text_hides_it()
    {
        var token = new string('t', 32);

        var (settings, problems) = ServerSettings.Load(
            Configuration(("IMMICH_URL", "http://immich_server:2283"), ("IMMICH_API_KEY", "abc"), ("MCP_TOKEN", token)),
            requireImmich: true);

        Assert.Empty(problems);
        Assert.True(settings!.McpEnabled);
        Assert.DoesNotContain(token, settings.ToString(), StringComparison.Ordinal);
    }
}
