using System.Net;
using System.Text.Json;
using Fibotrail.Games.Stamps;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Tests;

/// <summary>The Stamps game's routes. See docs/specs/stamps-game.md#the-server.</summary>
public class StampsGameTests
{
    /// <summary>A Friday: a 7 by 7 day, but 6 added trips make it 6 by 6.</summary>
    private const string Today = "2026-11-20";

    /// <summary>A cell past the last one of a 6 by 6 board.</summary>
    private static readonly int[] OffTheBoard = [36];

    /// <summary>Six trips, with the first <paramref name="added"/> of them added.</summary>
    private static async Task<(TestApp App, Browser Browser)> StartAsync(int added = 6)
    {
        var app = await TestApp.StartAsync();
        app.Time.Advance(TimeSpan.FromDays(5));
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 4, 20));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 5, 1, 9, 0, 0), 3, 20, 51.05, -114.07, "Calgary", "Alberta", "Canada"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 7, 3, 9, 0, 0), 4, 20, 50.5, -121.5, "Lakeside", "British Columbia", "Canada"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 9, 1, 9, 0, 0), 5, 20, 48.85, 2.35, "Paris", "Île-de-France", "France"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 11, 10, 9, 0, 0), 2, 20, 47.6, -117.4, "Spokane", "Washington", "United States of America"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2028, 2, 10, 9, 0, 0), 3, 20, 41.9, 12.5, "Rome", "Lazio", "Italy"));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        foreach (var trip in (await TripLibrary.TripsAsync(app)).Take(added))
        {
            Assert.Equal(HttpStatusCode.NoContent, (await browser.SendJsonAsync(HttpMethod.Post, $"/api/trips/{trip.Id}/decision", new { action = "add" })).StatusCode);
        }

        return (app, browser);
    }

    /// <summary>The board's solution, as stamp cells, worked out from the regions the puzzle sends.</summary>
    private static List<int> Solve(JsonElement puzzle)
    {
        var rows = puzzle.GetProperty("regions").EnumerateArray().Select(row => row.GetString()!).ToList();
        var size = rows.Count;
        var regions = rows.SelectMany(row => row.Select(letter => letter - 'A')).ToList();
        var columns = Assert.Single(StampsBoard.Solutions(size, regions, 2));
        return [.. Enumerable.Range(0, size).Select(row => (row * size) + columns[row])];
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(Browser browser, string path, object body)
    {
        var response = await browser.SendJsonAsync(HttpMethod.Post, path, body);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, JsonDocument.Parse(text).RootElement);
    }

    [Fact]
    public async Task Stamps_needs_five_added_trips_with_photos()
    {
        var (app, browser) = await StartAsync(added: 4);
        await using var _ = app;

        var puzzle = await browser.GetJsonAsync($"/api/games/stamps/puzzle?date={Today}");

        Assert.Equal(1, puzzle.GetProperty("tripsNeeded").GetInt32());
        Assert.Equal(0, puzzle.GetProperty("size").GetInt32());
        Assert.Empty(puzzle.GetProperty("regions").EnumerateArray());
    }

    [Fact]
    public async Task The_board_has_regions_and_photos_and_no_trip_names_or_solution()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;

        var puzzle = await browser.GetJsonAsync($"/api/games/stamps/puzzle?date={Today}");
        var again = await browser.GetJsonAsync($"/api/games/stamps/puzzle?date={Today}");

        Assert.Equal((0, 6, "Easy", "Fri"), (puzzle.GetProperty("tripsNeeded").GetInt32(), puzzle.GetProperty("size").GetInt32(), puzzle.GetProperty("label").GetString(), puzzle.GetProperty("weekday").GetString()));
        Assert.Equal("6 × 6 today, because you have 6 trips.", puzzle.GetProperty("sizeNote").GetString());
        Assert.Equal(6, puzzle.GetProperty("regions").GetArrayLength());
        Assert.Equal(36, puzzle.GetProperty("photos").GetArrayLength());
        Assert.False(puzzle.TryGetProperty("solution", out var _));
        var trips = await TripLibrary.TripsAsync(app);
        Assert.All(trips.Select(trip => trip.Name), name => Assert.DoesNotContain(name, puzzle.GetRawText(), StringComparison.Ordinal));
        Assert.Equal(puzzle.GetRawText(), again.GetRawText());
    }

    [Fact]
    public async Task A_hint_finds_a_step_or_a_mistake_and_stores_nothing()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;
        var solution = Solve(await browser.GetJsonAsync($"/api/games/stamps/puzzle?date={Today}"));
        var wrong = Enumerable.Range(0, 36).First(cell => !solution.Contains(cell));

        var (status, hint) = await PostAsync(browser, "/api/games/stamps/hint", new { date = Today, stamps = Array.Empty<int>(), crosses = Array.Empty<int>() });
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(hint.GetProperty("stamp").GetBoolean(), solution.Contains(hint.GetProperty("cell").GetInt32()));
        Assert.False(hint.GetProperty("wrong").GetBoolean());

        var (_, mistake) = await PostAsync(browser, "/api/games/stamps/hint", new { date = Today, stamps = new[] { wrong }, crosses = Array.Empty<int>() });
        Assert.Equal((wrong, true), (mistake.GetProperty("cell").GetInt32(), mistake.GetProperty("wrong").GetBoolean()));

        var (_, done) = await PostAsync(browser, "/api/games/stamps/hint", new { date = Today, stamps = solution, crosses = Array.Empty<int>() });
        Assert.Equal(JsonValueKind.Null, done.GetProperty("cell").ValueKind);

        var (offBoard, _) = await PostAsync(browser, "/api/games/stamps/hint", new { date = Today, stamps = OffTheBoard, crosses = Array.Empty<int>() });
        Assert.Equal(HttpStatusCode.BadRequest, offBoard);
        Assert.Equal(0, await app.WithDbAsync(db => db.GameResults.CountAsync()));
    }

    [Fact]
    public async Task Solving_stores_the_stamps_and_time_once_and_shows_the_passport()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;
        var solution = Solve(await browser.GetJsonAsync($"/api/games/stamps/puzzle?date={Today}"));

        var (status, result) = await PostAsync(browser, "/api/games/stamps/results", new { date = Today, stamps = solution, seconds = 161, hints = 1 });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(result.GetProperty("solved").GetBoolean());
        Assert.Equal((6, 161, 1), (result.GetProperty("size").GetInt32(), result.GetProperty("seconds").GetInt32(), result.GetProperty("hints").GetInt32()));
        Assert.Equal("Fibotrail Stamps #2 Fri 6×6 2:41 💡1", result.GetProperty("share").GetString());
        var passport = result.GetProperty("passport").EnumerateArray().ToList();
        Assert.Equal(6, passport.Count);
        Assert.Equal(solution.Order(), passport.Select(stamp => stamp.GetProperty("cell").GetInt32()).Order());
        var starts = passport.Select(stamp => stamp.GetProperty("startDate").GetString()).ToList();
        Assert.Equal(starts.Order(StringComparer.Ordinal), starts);
        var trips = await TripLibrary.TripsAsync(app);
        Assert.Equal(trips.Select(trip => trip.Name).Order(StringComparer.Ordinal), passport.Select(stamp => stamp.GetProperty("tripName").GetString()).Order(StringComparer.Ordinal));
        Assert.StartsWith("https://photos.example.com/photos/", passport[0].GetProperty("immichUrl").GetString(), StringComparison.Ordinal);
        var times = result.GetProperty("stats").GetProperty("times").EnumerateArray().Single();
        Assert.Equal(("6 × 6", 161, 161), (times.GetProperty("label").GetString(), times.GetProperty("best").GetInt32(), times.GetProperty("average").GetInt32()));

        // A second send keeps the first.
        var (_, second) = await PostAsync(browser, "/api/games/stamps/results", new { date = Today, stamps = solution, seconds = 5, hints = 0 });
        Assert.Equal(161, second.GetProperty("seconds").GetInt32());
        var stored = await app.WithDbAsync(db => db.GameResults.SingleAsync());
        Assert.Equal(("stamps", 1, 1, 161), (stored.Game, stored.Tries, stored.MaxTries, stored.Seconds));
        Assert.Equal(StampsPuzzle.Encode(6, solution, 1), stored.Marks);

        var again = await browser.GetJsonAsync($"/api/games/stamps/puzzle?date={Today}");
        Assert.Equal(161, again.GetProperty("result").GetProperty("seconds").GetInt32());
        var games = await browser.GetJsonAsync($"/api/games?date={Today}");
        var card = games.EnumerateArray().Single(game => game.GetProperty("id").GetString() == "stamps");
        Assert.True(card.GetProperty("solvedToday").GetBoolean());
        Assert.Equal(161, card.GetProperty("stats").GetProperty("times")[0].GetProperty("best").GetInt32());
        Assert.Equal(result.GetProperty("share").GetString(), card.GetProperty("share").GetString());
        Assert.Equal(JsonValueKind.Null, games[0].GetProperty("stats").GetProperty("times").ValueKind);
    }

    [Fact]
    public async Task Stamps_that_break_a_rule_or_a_time_out_of_range_are_refused()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;
        var solution = Solve(await browser.GetJsonAsync($"/api/games/stamps/puzzle?date={Today}"));

        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(browser, "/api/games/stamps/results", new { date = Today, stamps = solution[..^1], seconds = 60, hints = 0 })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(browser, "/api/games/stamps/results", new { date = Today, stamps = solution.Select(cell => cell == solution[0] ? solution[0] + 1 : cell), seconds = 60, hints = 0 })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(browser, "/api/games/stamps/results", new { date = Today, stamps = solution, seconds = 0, hints = 0 })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(browser, "/api/games/stamps/results", new { date = Today, stamps = solution, seconds = 60, hints = -1 })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(browser, "/api/games/stamps/results", new { date = "2026-11-25", stamps = solution, seconds = 60, hints = 0 })).Status);
        Assert.Equal(0, await app.WithDbAsync(db => db.GameResults.CountAsync()));
    }
}
