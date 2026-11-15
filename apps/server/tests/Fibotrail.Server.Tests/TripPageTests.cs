using System.Net;
using System.Text.Json;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Tests;

public class TripPageTests
{
    private static readonly DateTime March15 = new(2027, 3, 15, 9, 0, 0);

    /// <summary>A four-stop trip, 30 km legs to the north, with photos at known times and two with no position.</summary>
    private static async Task<(TestApp App, Browser Browser, int Id)> StartWithTripAsync()
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        const double km = 1 / 111.195;
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15, 1, 10, 28.4, -81.55, "Orlando", "Florida", "United States of America"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15.AddDays(1), 1, 10, 28.4 + (30 * km), -81.55, "Sanford", "Florida", "United States of America"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15.AddDays(2), 1, 10, 28.4 + (60 * km), -81.55, "Deltona", "Florida", "United States of America"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15.AddDays(3), 1, 10, 28.4 + (90 * km), -81.55, "DeLand", "Florida", "United States of America"));
        // 23:30 on day 2, local time.
        app.Immich.Assets.Add(TripLibrary.Asset(new DateTime(2027, 3, 16, 23, 30, 0), 28.4 + (30 * km), -81.55, "Sanford", "Florida", "United States of America"));
        app.Immich.Assets.AddRange(TripLibrary.Camera(March15.AddHours(3), 2, 2));
        var video = TripLibrary.Asset(new DateTime(2027, 3, 17, 10, 0, 0), 28.4 + (60 * km), -81.55, "Deltona", "Florida", "United States of America") with { IsVideo = true };
        app.Immich.Assets.Add(video);
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        var id = await app.WithDbAsync(db => db.Trips.Select(trip => trip.Id).SingleAsync());
        return (app, browser, id);
    }

    [Fact]
    public async Task The_page_has_the_header_stops_stats_and_days_of_one_trip()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;

        var trip = await browser.GetJsonAsync($"/api/trips/{id}");

        // Sanford has the most photos: 10, and one more at 23:30.
        Assert.Equal("Sanford, March 2027", trip.GetProperty("name").GetString());
        Assert.Equal(("2027-03-15", "2027-03-18"), (trip.GetProperty("startDate").GetString(), trip.GetProperty("endDate").GetString()));
        var stats = trip.GetProperty("stats");
        Assert.Equal(4, stats.GetProperty("days").GetInt32());
        Assert.Equal(44, stats.GetProperty("photos").GetInt32());
        Assert.Equal(4, stats.GetProperty("stops").GetInt32());
        Assert.Equal(2, stats.GetProperty("unplaced").GetInt32());
        var stops = trip.GetProperty("stops").EnumerateArray().ToList();
        Assert.Equal(["Orlando", "Sanford", "Deltona", "DeLand"], stops.Select(stop => stop.GetProperty("place").GetString()));
        Assert.Equal([10, 11, 11, 10], stops.Select(stop => stop.GetProperty("photoCount").GetInt32()));
    }

    [Fact]
    public async Task The_distance_tile_matches_the_sum_of_the_straight_lines_to_within_1_km()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;

        var stats = (await browser.GetJsonAsync($"/api/trips/{id}")).GetProperty("stats");

        Assert.InRange(stats.GetProperty("distanceKm").GetDouble(), 89, 91);
        Assert.True(stats.GetProperty("farthestFromHomeKm").GetDouble() > 4000);
    }

    [Fact]
    public async Task Photos_are_grouped_by_the_local_day_they_were_taken()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;

        var days = (await browser.GetJsonAsync($"/api/trips/{id}")).GetProperty("days").EnumerateArray().ToList();

        Assert.Equal([1, 2, 3, 4], days.Select(day => day.GetProperty("number").GetInt32()));
        Assert.Equal(["Orlando", "Sanford", "Deltona", "DeLand"], days.Select(day => day.GetProperty("place").GetString()));
        var dayTwo = days[1].GetProperty("photos").EnumerateArray().ToList();
        Assert.Equal("2027-03-16T23:30:00", dayTwo[^1].GetProperty("localTime").GetString());
        var times = days.SelectMany(day => day.GetProperty("photos").EnumerateArray()).Select(photo => photo.GetProperty("localTime").GetString()).ToList();
        Assert.Equal(times.Order(StringComparer.Ordinal), times);
        Assert.Equal("Day 2 · Tue, Mar 16 · Sanford", TripPage.DayHeading(new TripDay(2, new DateOnly(2027, 3, 16), "Sanford", [])));
    }

    [Fact]
    public async Task Each_photo_has_alt_text_its_stop_and_its_immich_address()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;

        var photos = (await browser.GetJsonAsync($"/api/trips/{id}")).GetProperty("days").EnumerateArray()
            .SelectMany(day => day.GetProperty("photos").EnumerateArray()).ToList();

        var first = photos[0];
        Assert.Equal("Photo taken Mar 15, 2027 in Orlando", first.GetProperty("alt").GetString());
        Assert.Equal(0, first.GetProperty("stopIndex").GetInt32());
        Assert.Equal($"https://photos.example.com/photos/{first.GetProperty("id").GetString()}", first.GetProperty("immichUrl").GetString());
        var unplaced = photos.Where(photo => photo.GetProperty("latitude").ValueKind == JsonValueKind.Null).ToList();
        Assert.Equal(2, unplaced.Count);
        Assert.All(unplaced, photo => Assert.Equal(JsonValueKind.Null, photo.GetProperty("stopIndex").ValueKind));
        var video = Assert.Single(photos, photo => photo.GetProperty("isVideo").GetBoolean());
        Assert.Equal(8836, video.GetProperty("durationMs").GetInt32());
        Assert.StartsWith("Video taken Mar 17, 2027", video.GetProperty("alt").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://photos.example.com", "abc", "https://photos.example.com/photos/abc")]
    [InlineData("https://photos.example.com/", "abc", "https://photos.example.com/photos/abc")]
    [InlineData("https://example.com/immich", "abc", "https://example.com/immich/photos/abc")]
    [InlineData(null, "abc", null)]
    public void The_immich_link_is_built_from_the_external_address(string? external, string id, string? expected)
    {
        Assert.Equal(expected, TripPage.ImmichPhotoUrl(external is null ? null : new Uri(external), id));
    }

    [Fact]
    public async Task With_no_external_address_no_photo_has_an_immich_link()
    {
        var folder = Directory.CreateTempSubdirectory("fibotrail-test-").FullName;
        await using var app = await TestApp.StartAsync(folder, externalUrl: null);
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15, 3, 25));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        var id = await app.WithDbAsync(db => db.Trips.Select(trip => trip.Id).SingleAsync());

        var photos = (await browser.GetJsonAsync($"/api/trips/{id}")).GetProperty("days").EnumerateArray()
            .SelectMany(day => day.GetProperty("photos").EnumerateArray());

        Assert.All(photos, photo => Assert.Equal(JsonValueKind.Null, photo.GetProperty("immichUrl").ValueKind));
    }

    [Fact]
    public async Task A_trip_that_doesnt_exist_is_not_found()
    {
        var (app, browser, _) = await StartWithTripAsync();
        await using var disposable = app;

        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/api/trips/9999", asPage: false)).StatusCode);
    }

    [Fact]
    public async Task A_rename_and_a_new_cover_survive_a_rescan()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;
        var photos = (await browser.GetJsonAsync($"/api/trips/{id}")).GetProperty("days")[2].GetProperty("photos");
        var cover = photos[3].GetProperty("id").GetString();

        var response = await browser.SendJsonAsync(HttpMethod.Put, $"/api/trips/{id}", new { name = "Florida road trip", coverPhotoId = cover });
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15.AddDays(4), 1, 12, 29.2, -81.0, "Daytona Beach", "Florida", "United States of America"));
        await app.ScanAsync();
        var trip = await browser.GetJsonAsync($"/api/trips/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Florida road trip", trip.GetProperty("name").GetString());
        Assert.Equal(cover, trip.GetProperty("coverPhotoId").GetString());
        Assert.Equal(5, trip.GetProperty("stats").GetProperty("days").GetInt32());
        // Renaming isn't adding: the status stays as it was.
        Assert.Equal("suggested", trip.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_rename_alone_keeps_the_cover_and_an_empty_name_is_refused()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;
        var before = await browser.GetJsonAsync($"/api/trips/{id}");

        var renamed = await browser.SendJsonAsync(HttpMethod.Put, $"/api/trips/{id}", new { name = "Spring break" });
        var empty = await browser.SendJsonAsync(HttpMethod.Put, $"/api/trips/{id}", new { name = " " });
        var missing = await browser.SendJsonAsync(HttpMethod.Put, "/api/trips/9999", new { name = "Nowhere" });
        var after = await browser.GetJsonAsync($"/api/trips/{id}");

        Assert.Equal(HttpStatusCode.NoContent, renamed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("Spring break", after.GetProperty("name").GetString());
        Assert.Equal(before.GetProperty("coverPhotoId").GetString(), after.GetProperty("coverPhotoId").GetString());
        Assert.Equal(1, await app.WithDbAsync(db => db.Decisions.CountAsync()));
    }
}
