using System.Net;
using System.Text.Json;
using Fibotrail.Games.Fetch;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Tests;

/// <summary>The Fetch game's routes. See docs/specs/fetch-game.md.</summary>
public class FetchGameTests
{
    /// <summary>The second day of games, so the puzzle is #2.</summary>
    private const string Today = "2026-11-20";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Tick 1 is the first flap, so a tap there is refused.</summary>
    private static readonly int[] TickOne = [1];

    private static async Task<(TestApp App, Browser Browser)> StartAsync(int added = 2)
    {
        var app = await TestApp.StartAsync();
        app.Time.Advance(TimeSpan.FromDays(5));
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 5, 1, 9, 0, 0), 3, 20, 51.05, -114.07, "Calgary", "Alberta", "Canada"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 9, 1, 9, 0, 0), 5, 20, 48.85, 2.35, "Paris", "Île-de-France", "France"));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        foreach (var trip in (await TripLibrary.TripsAsync(app)).Take(added))
        {
            Assert.Equal(HttpStatusCode.NoContent, (await browser.SendJsonAsync(HttpMethod.Post, $"/api/trips/{trip.Id}/decision", new { action = "add" })).StatusCode);
        }

        return (app, browser);
    }

    private static FetchCourse CourseOf(JsonElement puzzle) => puzzle.GetProperty("course").Deserialize<FetchCourse>(Json)!;

    /// <summary>Flaps when it's falling below the middle of the next gap, which flies any course.</summary>
    private static List<int> Autopilot(FetchCourse course)
    {
        var physics = course.Physics;
        var flight = new FetchFlight(course);
        var taps = new List<int>();
        flight.Step(true);
        while (flight.End is null)
        {
            var nose = physics.DogX + flight.Scroll + physics.Body.Left;
            var next = course.Gates.FirstOrDefault(gate => gate.X + physics.GateWidth >= nose) ?? course.Gates[^1];
            var flap = flight.Y + ((physics.Body.Top + physics.Body.Bottom) / 2) > (next.GapTop + next.GapBottom) / 2 && flight.Velocity >= 0;
            if (flap)
            {
                taps.Add(flight.Tick + 1);
            }

            flight.Step(flap);
        }

        return taps;
    }

    [Fact]
    public async Task Fetch_needs_one_added_trip_with_a_located_photo()
    {
        var (app, browser) = await StartAsync(added: 0);
        await using var _ = app;

        var puzzle = await browser.GetJsonAsync($"/api/games/fetch/puzzle?date={Today}");

        Assert.Equal(1, puzzle.GetProperty("tripsNeeded").GetInt32());
        Assert.Equal(JsonValueKind.Null, puzzle.GetProperty("course").ValueKind);
    }

    [Fact]
    public async Task The_course_sends_no_trip_name_or_location()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;

        var puzzle = await browser.GetJsonAsync($"/api/games/fetch/puzzle?date={Today}");
        var again = await browser.GetJsonAsync($"/api/games/fetch/puzzle?date={Today}");

        Assert.Equal(3, puzzle.GetProperty("maxTries").GetInt32());
        var course = CourseOf(puzzle);
        Assert.Equal(8, course.Gates.Count);
        Assert.Equal(FetchPhysics.Default, course.Physics);
        var trips = await TripLibrary.TripsAsync(app);
        Assert.Contains(trips.Take(2), trip => course.Treats.All(treat => trip.Photos.Any(photo => photo.PhotoId == treat.PhotoId)));
        var raw = puzzle.GetRawText();
        Assert.All(trips.Select(trip => trip.Name), name => Assert.DoesNotContain(name, raw, StringComparison.Ordinal));
        Assert.DoesNotContain("latitude", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(raw, again.GetRawText());
    }

    [Fact]
    public async Task A_check_grades_one_run_without_storing_it()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;

        var response = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/fetch/check", new { date = Today, taps = Array.Empty<int>() });
        var grade = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ground", grade.GetProperty("end").GetString());
        Assert.Equal(0, grade.GetProperty("gatesPassed").GetInt32());
        Assert.Equal(0, await app.WithDbAsync(db => db.GameResults.CountAsync()));
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.SendJsonAsync(HttpMethod.Post, "/api/games/fetch/check", new { date = Today, taps = TickOne })).StatusCode);
    }

    [Fact]
    public async Task Finishing_stores_the_taps_once_and_shows_the_trip()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;
        var puzzle = await browser.GetJsonAsync($"/api/games/fetch/puzzle?date={Today}");
        var course = CourseOf(puzzle);
        var flown = Autopilot(course);
        var expected = FetchFlight.Fly(course, flown);
        int[][] runs = [[], [.. flown]];

        var response = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/fetch/results", new { date = Today, runs });
        var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(result.GetProperty("solved").GetBoolean());
        Assert.Equal(2, result.GetProperty("tries").GetInt32());
        Assert.Equal(["ground", "finish"], result.GetProperty("runs").EnumerateArray().Select(run => run.GetProperty("end").GetString()));
        Assert.Equal(expected.Caught.Count, result.GetProperty("treats").GetInt32());
        Assert.Equal(expected.Caught.Select(index => course.Treats[index].PhotoId), result.GetProperty("caughtPhotoIds").EnumerateArray().Select(id => id.GetString()));
        Assert.StartsWith($"Fibotrail Fetch #2 2/3 🦴 {expected.Caught.Count}/8\n💥\n", result.GetProperty("share").GetString(), StringComparison.Ordinal);
        var trip = Assert.Single(await TripLibrary.TripsAsync(app), trip => trip.Id == result.GetProperty("trip").GetProperty("tripId").GetInt32());
        Assert.Equal(trip.Name, result.GetProperty("trip").GetProperty("tripName").GetString());
        Assert.Equal(1, result.GetProperty("stats").GetProperty("played").GetInt32());

        // A second send keeps the first.
        int[][] other = [[.. flown]];
        var second = await browser.SendJsonAsync(HttpMethod.Post, "/api/games/fetch/results", new { date = Today, runs = other });
        Assert.Equal(2, JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement.GetProperty("tries").GetInt32());
        var stored = await app.WithDbAsync(db => db.GameResults.SingleAsync());
        Assert.Equal(("fetch", 2, 3), (stored.Game, stored.Tries, stored.MaxTries));
        Assert.Equal(";" + string.Join(',', flown), stored.Marks);

        var again = await browser.GetJsonAsync($"/api/games/fetch/puzzle?date={Today}");
        Assert.Equal(2, again.GetProperty("result").GetProperty("runs").GetArrayLength());
        var games = await browser.GetJsonAsync($"/api/games?date={Today}");
        Assert.True(games.EnumerateArray().Single(game => game.GetProperty("id").GetString() == "fetch").GetProperty("solvedToday").GetBoolean());
    }

    [Fact]
    public async Task Runs_that_do_not_end_the_game_are_refused()
    {
        var (app, browser) = await StartAsync();
        await using var _ = app;
        var flown = Autopilot(CourseOf(await browser.GetJsonAsync($"/api/games/fetch/puzzle?date={Today}")));

        async Task<HttpStatusCode> SendAsync(int[][] runs, string date = Today) =>
            (await browser.SendJsonAsync(HttpMethod.Post, "/api/games/fetch/results", new { date, runs })).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync([[]]));
        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync([]));
        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync([[.. flown], []]));
        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync([[], [], [], []]));
        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync([[9, 5], [.. flown]]));
        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync([[.. flown]], "2026-11-25"));
        Assert.Equal(0, await app.WithDbAsync(db => db.GameResults.CountAsync()));

        // Three crashes end the game, unsolved.
        Assert.Equal(HttpStatusCode.OK, await SendAsync([[], [], []]));
        var stored = await app.WithDbAsync(db => db.GameResults.SingleAsync());
        Assert.False(stored.Solved);
    }
}
