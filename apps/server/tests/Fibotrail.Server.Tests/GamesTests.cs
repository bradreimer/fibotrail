using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Tests;

/// <summary>The Games tab and the Timeline game. See docs/specs/games.md and docs/specs/timeline-game.md.</summary>
public class GamesTests
{
    private const string Today = "2026-11-20";

    /// <summary>Six trips, with the first <paramref name="added"/> of them added.</summary>
    private static async Task<(TestApp App, Browser Browser)> StartAsync(int added = 5)
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

    private static async Task<List<string>> AnswerAsync(TestApp app, JsonElement puzzle)
    {
        var shown = puzzle.GetProperty("cards").EnumerateArray().Select(card => card.GetProperty("photoId").GetString()!).ToList();
        var taken = await app.WithDbAsync(db => db.Photos.AsNoTracking().Where(photo => shown.Contains(photo.Id)).ToDictionaryAsync(photo => photo.Id, photo => photo.LocalTime));
        return [.. shown.OrderBy(id => taken[id])];
    }

    [Fact]
    public async Task The_games_list_has_Timeline_with_empty_stats()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;

        var games = await browser.GetJsonAsync($"/api/games?date={Today}");

        var timeline = Assert.Single(games.EnumerateArray());
        Assert.Equal("timeline", timeline.GetProperty("id").GetString());
        Assert.False(timeline.GetProperty("playedToday").GetBoolean());
        Assert.Equal(0, timeline.GetProperty("stats").GetProperty("played").GetInt32());
    }

    [Fact]
    public async Task A_date_far_from_today_is_refused()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;

        Assert.Equal(HttpStatusCode.BadRequest, (await browser.GetAsync("/api/games/timeline/puzzle?date=2026-11-25", asPage: false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.GetAsync("/api/games?date=2020-01-01", asPage: false)).StatusCode);
    }

    [Fact]
    public async Task With_four_added_trips_one_more_is_needed()
    {
        var (app, browser) = await StartAsync(added: 4);
        await using var _ = app;

        var puzzle = await browser.GetJsonAsync($"/api/games/timeline/puzzle?date={Today}");

        Assert.Equal(1, puzzle.GetProperty("tripsNeeded").GetInt32());
        Assert.Empty(puzzle.GetProperty("cards").EnumerateArray());
    }

    [Fact]
    public async Task The_puzzle_has_five_cards_from_added_trips_and_no_dates()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;

        var puzzle = await browser.GetJsonAsync($"/api/games/timeline/puzzle?date={Today}");
        var again = await browser.GetJsonAsync($"/api/games/timeline/puzzle?date={Today}");

        var cards = puzzle.GetProperty("cards").EnumerateArray().ToList();
        Assert.Equal(5, cards.Count);
        Assert.Equal(2, puzzle.GetProperty("number").GetInt32());
        Assert.DoesNotContain("Rome", puzzle.GetRawText(), StringComparison.Ordinal);
        Assert.False(cards[0].TryGetProperty("date", out JsonElement _));
        Assert.Equal(["photoId"], cards[0].EnumerateObject().Select(property => property.Name));
        var names = (await TripLibrary.TripsAsync(app)).Select(trip => trip.Name);
        Assert.All(names, name => Assert.DoesNotContain(name, puzzle.GetRawText(), StringComparison.Ordinal));
        Assert.Equal(puzzle.GetRawText(), again.GetRawText());
        Assert.Equal(JsonValueKind.Null, puzzle.GetProperty("result").ValueKind);
    }

    [Fact]
    public async Task Check_marks_each_position()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;
        var answer = await AnswerAsync(app, await browser.GetJsonAsync($"/api/games/timeline/puzzle?date={Today}"));

        var response = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/timeline/check", new { date = Today, order = new[] { answer[1], answer[0], answer[2], answer[3], answer[4] } });
        var marks = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("marks").EnumerateArray().Select(mark => mark.GetString()).ToList();

        Assert.Equal(["close", "close", "right", "right", "right"], marks);
        Assert.Equal(0, await app.WithDbAsync(db => db.GameResults.CountAsync()));
    }

    [Fact]
    public async Task A_solved_result_is_stored_once_and_counted()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;
        var answer = await AnswerAsync(app, await browser.GetJsonAsync($"/api/games/timeline/puzzle?date={Today}"));
        var first = new[] { answer[1], answer[0], answer[2], answer[3], answer[4] };

        var response = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/timeline/results", new { date = Today, guesses = new[] { first, [.. answer] } });
        var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(result.GetProperty("solved").GetBoolean());
        Assert.Equal(2, result.GetProperty("tries").GetInt32());
        Assert.Equal("Fibotrail Timeline #2 2/4\n🟨🟨🟩🟩🟩\n🟩🟩🟩🟩🟩", result.GetProperty("share").GetString());
        Assert.Equal(answer, result.GetProperty("answer").EnumerateArray().Select(card => card.GetProperty("photoId").GetString()!));
        var trips = await TripLibrary.TripsAsync(app);
        foreach (var card in result.GetProperty("answer").EnumerateArray())
        {
            var trip = Assert.Single(trips, trip => trip.Id == card.GetProperty("tripId").GetInt32());
            Assert.Equal(trip.Name, card.GetProperty("tripName").GetString());
            Assert.Contains(trip.Photos, photo => photo.PhotoId == card.GetProperty("photoId").GetString());
        }
        Assert.Equal((1, 100, 1), (result.GetProperty("stats").GetProperty("played").GetInt32(), result.GetProperty("stats").GetProperty("winPercent").GetInt32(), result.GetProperty("stats").GetProperty("currentStreak").GetInt32()));

        // A second send, even a better one, keeps the first.
        var second = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/timeline/results", new { date = Today, guesses = new[] { answer } });
        Assert.Equal(2, JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement.GetProperty("tries").GetInt32());
        Assert.Equal(1, await app.WithDbAsync(db => db.GameResults.CountAsync()));

        var puzzle = await browser.GetJsonAsync($"/api/games/timeline/puzzle?date={Today}");
        Assert.True(puzzle.GetProperty("result").GetProperty("solved").GetBoolean());
        var games = await browser.GetJsonAsync($"/api/games?date={Today}");
        Assert.True(games[0].GetProperty("playedToday").GetBoolean());
    }

    [Fact]
    public async Task An_unfinished_or_invalid_game_is_not_a_result()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;
        var answer = await AnswerAsync(app, await browser.GetJsonAsync($"/api/games/timeline/puzzle?date={Today}"));
        string[] wrong = [.. Enumerable.Reverse(answer)];

        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendJsonAsync(HttpMethod.Post, "/api/games/timeline/results", new { date = Today, guesses = new[] { wrong } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendJsonAsync(HttpMethod.Post, "/api/games/timeline/results", new { date = Today, guesses = new[] { wrong[..4] } })).StatusCode);
        Assert.Equal(0, await app.WithDbAsync(db => db.GameResults.CountAsync()));

        var lost = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/timeline/results", new { date = Today, guesses = new[] { wrong, wrong, wrong, wrong } });
        Assert.False(JsonDocument.Parse(await lost.Content.ReadAsStringAsync()).RootElement.GetProperty("solved").GetBoolean());
    }
}
