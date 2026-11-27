using System.Net;
using System.Text.Json;
using Fibotrail.Games.Slide;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Tests;

/// <summary>The Games tab and its games. See docs/specs/games.md and each game's spec.</summary>
public class GamesTests
{
    /// <summary>A Friday. Six cards needs 6 added trips, and these tests add 5, so it plays as Classic.</summary>
    private const string Today = "2026-11-20";

    /// <summary>The Sunday after, with the Count only twist.</summary>
    private const string Sunday = "2026-11-22";

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
    public async Task The_games_list_has_every_game_with_empty_stats()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;

        var games = await browser.GetJsonAsync($"/api/games?date={Today}");

        Assert.Equal(["timeline", "slide", "pin", "fetch"], games.EnumerateArray().Select(game => game.GetProperty("id").GetString()));
        var timeline = games[0];
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
        var twist = puzzle.GetProperty("twist");
        Assert.Equal(("classic", "sixCards", "Classic"), (twist.GetProperty("id").GetString(), twist.GetProperty("planned").GetString(), twist.GetProperty("name").GetString()));
        Assert.StartsWith("Six cards needs 6 trips", twist.GetProperty("rule").GetString(), StringComparison.Ordinal);
        Assert.Equal(4, puzzle.GetProperty("maxTries").GetInt32());
    }

    [Fact]
    public async Task Sunday_counts_only_and_allows_six_tries()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;
        app.Time.Advance(TimeSpan.FromDays(2));
        var puzzle = await browser.GetJsonAsync($"/api/games/timeline/puzzle?date={Sunday}");
        var answer = await AnswerAsync(app, puzzle);
        string[] swapped = [answer[1], answer[0], answer[2], answer[3], answer[4]];
        string[] wrong = [.. Enumerable.Reverse(answer)];

        var check = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/timeline/check", new { date = Sunday, order = swapped });
        var marks = JsonDocument.Parse(await check.Content.ReadAsStringAsync()).RootElement.GetProperty("marks").EnumerateArray().Select(mark => mark.GetString()).ToList();
        var early = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/timeline/results", new { date = Sunday, guesses = new[] { wrong, wrong, wrong, wrong } });
        var lost = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/timeline/results", new { date = Sunday, guesses = new[] { wrong, wrong, wrong, wrong, wrong, wrong } });
        var result = JsonDocument.Parse(await lost.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("countOnly", puzzle.GetProperty("twist").GetProperty("id").GetString());
        Assert.Equal(6, puzzle.GetProperty("maxTries").GetInt32());
        Assert.Equal(["right", "right", "right", "wrong", "wrong"], marks);
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Equal(HttpStatusCode.OK, lost.StatusCode);
        Assert.False(result.GetProperty("solved").GetBoolean());
        Assert.StartsWith("Fibotrail Timeline #4 Count only X/6\n", result.GetProperty("share").GetString(), StringComparison.Ordinal);
        Assert.Equal(6, result.GetProperty("stats").GetProperty("tries").GetArrayLength());
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
            Assert.Equal($"https://photos.example.com/photos/{card.GetProperty("photoId").GetString()}", card.GetProperty("immichUrl").GetString());
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
        Assert.False(games[1].GetProperty("playedToday").GetBoolean());
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

    /// <summary>The fewest moves from a board to solved, by breadth-first search.</summary>
    private static List<int> Solve(IReadOnlyList<int> start)
    {
        var solved = string.Concat(SlidePuzzle.Solved);
        var from = new Dictionary<string, (string Previous, int Tile)> { [string.Concat(start)] = (string.Empty, 0) };
        var queue = new Queue<int[]>([[.. start]]);
        while (queue.TryDequeue(out var board))
        {
            var here = string.Concat(board);
            if (here == solved)
            {
                var moves = new List<int>();
                for (var at = here; from[at].Previous.Length > 0; at = from[at].Previous)
                {
                    moves.Add(from[at].Tile);
                }

                moves.Reverse();
                return moves;
            }

            foreach (var tile in Enumerable.Range(1, 8))
            {
                if (SlidePuzzle.Move(board, tile) is { } next && from.TryAdd(string.Concat(next), (here, tile)))
                {
                    queue.Enqueue(next);
                }
            }
        }

        throw new InvalidOperationException("The board can't be solved.");
    }

    private static readonly int[] OneMove = [1];

    private static List<int> TilesOf(JsonElement puzzle) => [.. puzzle.GetProperty("tiles").EnumerateArray().Select(tile => tile.GetInt32())];

    [Fact]
    public async Task Slide_needs_one_added_trip_with_photos()
    {
        var (app, browser) = await StartAsync(added: 0);
        await using var _ = app;

        var puzzle = await browser.GetJsonAsync($"/api/games/slide/puzzle?date={Today}");

        Assert.Equal(1, puzzle.GetProperty("tripsNeeded").GetInt32());
        Assert.Equal(JsonValueKind.Null, puzzle.GetProperty("photoId").ValueKind);
        Assert.Empty(puzzle.GetProperty("tiles").EnumerateArray());
    }

    [Fact]
    public async Task The_slide_puzzle_is_a_shuffled_photo_with_no_trip_name()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;

        var puzzle = await browser.GetJsonAsync($"/api/games/slide/puzzle?date={Today}");
        var again = await browser.GetJsonAsync($"/api/games/slide/puzzle?date={Today}");

        var tiles = TilesOf(puzzle);
        Assert.Equal(Enumerable.Range(0, 9), tiles.Order());
        Assert.Equal(0, tiles[^1]);
        Assert.NotEqual(SlidePuzzle.Solved, tiles);
        var trips = await TripLibrary.TripsAsync(app);
        Assert.Contains(trips.Take(5), trip => trip.Photos.Any(photo => photo.PhotoId == puzzle.GetProperty("photoId").GetString()));
        Assert.All(trips.Select(trip => trip.Name), name => Assert.DoesNotContain(name, puzzle.GetRawText(), StringComparison.Ordinal));
        Assert.Equal(puzzle.GetRawText(), again.GetRawText());
    }

    [Fact]
    public async Task Solving_the_slide_puzzle_stores_the_moves_once_and_shows_the_trip()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;
        var puzzle = await browser.GetJsonAsync($"/api/games/slide/puzzle?date={Today}");
        var moves = Solve(TilesOf(puzzle));

        var response = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/slide/results", new { date = Today, moves });
        var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(result.GetProperty("solved").GetBoolean());
        Assert.Equal(moves.Count, result.GetProperty("moves").GetInt32());
        Assert.Equal($"Fibotrail Slide #2 {moves.Count} moves", result.GetProperty("share").GetString());
        var answer = result.GetProperty("answer");
        var trip = Assert.Single(await TripLibrary.TripsAsync(app), trip => trip.Id == answer.GetProperty("tripId").GetInt32());
        Assert.Equal(trip.Name, answer.GetProperty("tripName").GetString());
        Assert.Equal(puzzle.GetProperty("photoId").GetString(), answer.GetProperty("photoId").GetString());
        Assert.False(string.IsNullOrEmpty(answer.GetProperty("startDate").GetString()));
        Assert.Equal($"https://photos.example.com/photos/{answer.GetProperty("photoId").GetString()}", answer.GetProperty("immichUrl").GetString());
        Assert.Equal(1, result.GetProperty("stats").GetProperty("played").GetInt32());

        // A second send keeps the first.
        var second = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/slide/results", new { date = Today, moves = OneMove });
        Assert.Equal(moves.Count, JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement.GetProperty("moves").GetInt32());
        var stored = await app.WithDbAsync(db => db.GameResults.SingleAsync());
        Assert.Equal(("slide", 1, 1), (stored.Game, stored.Tries, stored.MaxTries));
        Assert.Equal(string.Concat(moves), stored.Marks);

        var again = await browser.GetJsonAsync($"/api/games/slide/puzzle?date={Today}");
        Assert.Equal(moves.Count, again.GetProperty("result").GetProperty("moves").GetInt32());
        var games = await browser.GetJsonAsync($"/api/games?date={Today}");
        Assert.True(games[1].GetProperty("solvedToday").GetBoolean());
    }

    [Fact]
    public async Task Moves_that_do_not_solve_the_slide_puzzle_are_refused()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;
        var moves = Solve(TilesOf(await browser.GetJsonAsync($"/api/games/slide/puzzle?date={Today}")));

        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendJsonAsync(HttpMethod.Post, "/api/games/slide/results", new { date = Today, moves = moves[..^1] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendJsonAsync(HttpMethod.Post, "/api/games/slide/results", new { date = Today, moves = Array.Empty<int>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendJsonAsync(HttpMethod.Post, "/api/games/slide/results", new { date = "2026-11-25", moves })).StatusCode);
        Assert.Equal(0, await app.WithDbAsync(db => db.GameResults.CountAsync()));
    }

    private static async Task<(double Latitude, double Longitude)> LocationAsync(TestApp app, JsonElement puzzle)
    {
        var id = puzzle.GetProperty("photoId").GetString();
        var photo = await app.WithDbAsync(db => db.Photos.AsNoTracking().SingleAsync(photo => photo.Id == id));
        return (photo.Latitude!.Value, photo.Longitude!.Value);
    }

    [Fact]
    public async Task Pin_needs_one_added_trip_with_a_located_photo()
    {
        var (app, browser) = await StartAsync(added: 0);
        await using var _ = app;

        var puzzle = await browser.GetJsonAsync($"/api/games/pin/puzzle?date={Today}");

        Assert.Equal(1, puzzle.GetProperty("tripsNeeded").GetInt32());
        Assert.Equal(JsonValueKind.Null, puzzle.GetProperty("photoId").ValueKind);
    }

    [Fact]
    public async Task The_pin_puzzle_sends_a_photo_with_no_location_or_trip_name()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;

        var puzzle = await browser.GetJsonAsync($"/api/games/pin/puzzle?date={Today}");
        var again = await browser.GetJsonAsync($"/api/games/pin/puzzle?date={Today}");

        Assert.Equal(4, puzzle.GetProperty("maxTries").GetInt32());
        var trips = await TripLibrary.TripsAsync(app);
        Assert.Contains(trips.Take(5), trip => trip.Photos.Any(photo => photo.PhotoId == puzzle.GetProperty("photoId").GetString()));
        var raw = puzzle.GetRawText();
        Assert.All(trips.Select(trip => trip.Name), name => Assert.DoesNotContain(name, raw, StringComparison.Ordinal));
        Assert.DoesNotContain("latitude", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(raw, again.GetRawText());
    }

    [Fact]
    public async Task A_pin_check_grades_one_pin_without_storing_it()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;
        var (latitude, longitude) = await LocationAsync(app, await browser.GetJsonAsync($"/api/games/pin/puzzle?date={Today}"));

        // 10 degrees of latitude south is about 1,112 km, so the answer is Orange and to the north.
        var response = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/pin/check", new { date = Today, pin = new { latitude = latitude - 10, longitude } });
        var mark = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("orange", mark.GetProperty("band").GetString());
        Assert.Equal("n", mark.GetProperty("direction").GetString());
        Assert.Equal(1200, mark.GetProperty("distanceKm").GetInt32());
        Assert.Equal(0, await app.WithDbAsync(db => db.GameResults.CountAsync()));
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendJsonAsync(HttpMethod.Post, "/api/games/pin/check", new { date = Today, pin = new { latitude = 95, longitude } })).StatusCode);
    }

    [Fact]
    public async Task Solving_the_pin_puzzle_stores_the_pins_once_and_shows_the_trip()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;
        var puzzle = await browser.GetJsonAsync($"/api/games/pin/puzzle?date={Today}");
        var (latitude, longitude) = await LocationAsync(app, puzzle);
        object[] pins = [new { latitude = latitude - 30, longitude }, new { latitude = latitude + 0.1, longitude }];

        var response = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/pin/results", new { date = Today, pins });
        var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(result.GetProperty("solved").GetBoolean());
        Assert.Equal(2, result.GetProperty("tries").GetInt32());
        Assert.Equal(["red", "green"], result.GetProperty("marks").EnumerateArray().Select(mark => mark.GetProperty("band").GetString()));
        Assert.Equal("Fibotrail Pin #2 2/4\n🟥⬆️\n🟩", result.GetProperty("share").GetString());
        var answer = result.GetProperty("answer");
        var trip = Assert.Single(await TripLibrary.TripsAsync(app), trip => trip.Id == answer.GetProperty("tripId").GetInt32());
        Assert.Equal(trip.Name, answer.GetProperty("tripName").GetString());
        Assert.Equal(latitude, answer.GetProperty("latitude").GetDouble(), 6);
        Assert.Equal($"https://photos.example.com/photos/{puzzle.GetProperty("photoId").GetString()}", answer.GetProperty("immichUrl").GetString());
        Assert.Equal(1, result.GetProperty("stats").GetProperty("played").GetInt32());

        // A second send keeps the first.
        object[] other = [new { latitude, longitude }];
        var second = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/pin/results", new { date = Today, pins = other });
        Assert.Equal(2, JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement.GetProperty("tries").GetInt32());
        var stored = await app.WithDbAsync(db => db.GameResults.SingleAsync());
        Assert.Equal(("pin", 2, 4), (stored.Game, stored.Tries, stored.MaxTries));

        var again = await browser.GetJsonAsync($"/api/games/pin/puzzle?date={Today}");
        Assert.Equal(2, again.GetProperty("result").GetProperty("marks").GetArrayLength());
        var games = await browser.GetJsonAsync($"/api/games?date={Today}");
        Assert.True(games[2].GetProperty("solvedToday").GetBoolean());
    }

    [Fact]
    public async Task Pins_that_do_not_end_the_pin_game_are_refused()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;
        var (latitude, longitude) = await LocationAsync(app, await browser.GetJsonAsync($"/api/games/pin/puzzle?date={Today}"));
        var far = new { latitude = latitude - 30, longitude };
        var right = new { latitude, longitude };

        async Task<HttpStatusCode> SendAsync(object[] pins, string date = Today) =>
            (await browser.SendJsonAsync(HttpMethod.Post, "/api/games/pin/results", new { date, pins })).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync([far]));
        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync([]));
        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync([right, far]));
        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync([far, far, far, far, far]));
        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync([new { latitude = 0, longitude = 200 }, right]));
        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync([right], "2026-11-25"));
        Assert.Equal(0, await app.WithDbAsync(db => db.GameResults.CountAsync()));

        // Four misses end the game, unsolved.
        Assert.Equal(HttpStatusCode.OK, await SendAsync([far, far, far, far]));
        var stored = await app.WithDbAsync(db => db.GameResults.SingleAsync());
        Assert.False(stored.Solved);
    }
}
