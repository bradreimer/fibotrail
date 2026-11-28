using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trails;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Tests;

public class TrailTests
{
    private static readonly DateTime March15 = new(2027, 3, 15, 9, 0, 0);

    /// <summary>14:00 UTC on the trip's first day: 10:00 in Orlando.</summary>
    private static readonly DateTime March15Utc = new(2027, 3, 15, 14, 0, 0, DateTimeKind.Utc);

    private static async Task<(TestApp App, Browser Browser, int Id)> StartWithTripAsync(TestApp? app = null)
    {
        app ??= await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15, 4, 40));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        var id = await app.WithDbAsync(db => db.Trips.Select(trip => trip.Id).SingleAsync());
        return (app, browser, id);
    }

    /// <summary>A walk north in Orlando: one point every 5 seconds, 7 m apart.</summary>
    private static IEnumerable<(DateTime Time, double Latitude, double Longitude)> Walk(DateTime start, int points, double latitude = 28.4) =>
        Enumerable.Range(0, points).Select(index => (start.AddSeconds(index * 5), latitude + (index * 0.007 / 111.195), -81.55));

    private static string Gpx(params IEnumerable<(DateTime Time, double Latitude, double Longitude)>[] segments)
    {
        var text = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<gpx version=\"1.1\" creator=\"GPSBabel\" xmlns=\"http://www.topografix.com/GPX/1/1\"><trk>");
        foreach (var segment in segments)
        {
            text.Append("<trkseg>");
            foreach (var (time, latitude, longitude) in segment)
            {
                text.Append(CultureInfo.InvariantCulture, $"<trkpt lat=\"{latitude:R}\" lon=\"{longitude:R}\"><time>{time:yyyy-MM-dd'T'HH:mm:ss'Z'}</time></trkpt>");
            }

            text.Append("</trkseg>");
        }

        return text.Append("</trk></gpx>").ToString();
    }

    private static async Task<HttpResponseMessage> UploadAsync(Browser browser, string path, byte[] file, string name = "2027-03-15.gpx")
    {
        // Read the session first, so the browser holds the anti-forgery token the web app sends.
        await browser.GetJsonAsync("/api/session");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{path}?name={Uri.EscapeDataString(name)}");
        request.Content = new ByteArrayContent(file);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/gpx+xml");
        request.Headers.Add("X-CSRF-Token", await CsrfAsync(browser));
        return await browser.SendAsync(request);
    }

    private static async Task<string> CsrfAsync(Browser browser) =>
        (await browser.GetJsonAsync("/api/session")).GetProperty("csrfToken").GetString()!;

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static Task<int> StoredFilesAsync(TestApp app) =>
        Task.FromResult(Directory.Exists(app.Settings.TrailDirectory) ? Directory.GetFiles(app.Settings.TrailDirectory).Length : 0);

    [Fact]
    public async Task A_preview_gives_the_dates_distance_and_points_and_saves_nothing()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;

        var response = await UploadAsync(browser, $"/api/trips/{id}/trails/preview", Encoding.UTF8.GetBytes(Gpx(Walk(March15Utc, 101))));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await JsonAsync(response);
        Assert.Equal("2027-03-15.gpx", preview.GetProperty("originalName").GetString());
        Assert.Equal(101, preview.GetProperty("pointCount").GetInt32());
        Assert.Equal(0.7, preview.GetProperty("distanceKm").GetDouble(), 2);
        Assert.Equal(8, preview.GetProperty("movingMinutes").GetInt32());
        Assert.Equal(March15Utc, preview.GetProperty("startUtc").GetDateTime().ToUniversalTime());
        Assert.True(preview.GetProperty("overlapsTrip").GetBoolean());
        Assert.Equal(0, await app.WithDbAsync(db => db.Trails.CountAsync()));
        Assert.Equal(0, await StoredFilesAsync(app));
    }

    [Fact]
    public async Task An_added_trail_is_kept_byte_for_byte_and_listed_with_its_line()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;
        // Windows line endings and a comment, so a rewrite of the file would show.
        var file = Encoding.UTF8.GetBytes(Gpx(Walk(March15Utc, 50)).Replace("<trk>", "\r\n<!-- logger 7 -->\r\n<trk>", StringComparison.Ordinal));

        var response = await UploadAsync(browser, $"/api/trips/{id}/trails", file);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await app.WithDbAsync(db => db.Trails.AsNoTracking().SingleAsync());
        Assert.Equal(file, await File.ReadAllBytesAsync(Path.Combine(app.Settings.TrailDirectory, stored.FileName)));
        var trail = Assert.Single((await browser.GetJsonAsync($"/api/trips/{id}/trails")).EnumerateArray());
        Assert.Equal(50, trail.GetProperty("pointCount").GetInt32());
        var line = Assert.Single(trail.GetProperty("segments").EnumerateArray());
        Assert.Equal(28.4, line[0].GetProperty("latitude").GetDouble(), 6);
        Assert.Equal(-81.55, line[0].GetProperty("longitude").GetDouble(), 6);
    }

    [Fact]
    public async Task A_10_minute_gap_is_two_separate_lines()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;
        var morning = Walk(March15Utc, 60).ToList();
        var later = Walk(morning[^1].Time.AddMinutes(10), 60, morning[^1].Latitude);

        await UploadAsync(browser, $"/api/trips/{id}/trails", Encoding.UTF8.GetBytes(Gpx([.. morning, .. later])));

        var trail = Assert.Single((await browser.GetJsonAsync($"/api/trips/{id}/trails")).EnumerateArray());
        Assert.Equal(2, trail.GetProperty("segments").GetArrayLength());
    }

    [Fact]
    public async Task A_trail_of_100000_points_sends_no_more_than_5000_to_the_browser()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;
        var points = Enumerable.Range(0, 100_000).Select(index => (
            March15Utc.AddSeconds(index),
            28.4 + (index * 0.00002),
            -81.55 + (0.002 * Math.Sin(index / 40.0)) + (0.0004 * Math.Sin(index / 7.0))));

        var response = await UploadAsync(browser, $"/api/trips/{id}/trails", Encoding.UTF8.GetBytes(Gpx(points)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var trail = Assert.Single((await browser.GetJsonAsync($"/api/trips/{id}/trails")).EnumerateArray());
        Assert.Equal(100_000, trail.GetProperty("pointCount").GetInt32());
        Assert.InRange(trail.GetProperty("segments").EnumerateArray().Sum(segment => segment.GetArrayLength()), 2, 5000);
    }

    [Theory]
    [InlineData("{\"type\":\"FeatureCollection\"}", "This file isn't a GPX file.")]
    [InlineData("<gpx version=\"1.1\"><wpt lat=\"1\" lon=\"1\"/></gpx>", "This GPX file has no track points")]
    public async Task A_file_that_isnt_usable_GPX_is_refused_with_a_plain_message_and_nothing_is_stored(string text, string message)
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;

        foreach (var path in new[] { $"/api/trips/{id}/trails/preview", $"/api/trips/{id}/trails" })
        {
            var response = await UploadAsync(browser, path, Encoding.UTF8.GetBytes(text));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.StartsWith(message, (await JsonAsync(response)).GetProperty("detail").GetString(), StringComparison.Ordinal);
        }

        Assert.Equal(0, await app.WithDbAsync(db => db.Trails.CountAsync()));
        Assert.Equal(0, await StoredFilesAsync(app));
    }

    [Fact]
    public async Task A_file_over_50_MB_is_refused_with_413_and_nothing_is_stored()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;
        var file = new byte[(int)TrailStore.MaxBytes + 1];

        var response = await UploadAsync(browser, $"/api/trips/{id}/trails", file);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(TrailStore.TooLarge, (await JsonAsync(response)).GetProperty("detail").GetString());
        Assert.Equal(0, await StoredFilesAsync(app));
    }

    [Fact]
    public async Task A_trail_that_doesnt_overlap_the_trip_is_flagged_and_can_still_be_added()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;
        var file = Encoding.UTF8.GetBytes(Gpx(Walk(March15Utc.AddDays(-30), 20)));

        var preview = await JsonAsync(await UploadAsync(browser, $"/api/trips/{id}/trails/preview", file));
        var added = await UploadAsync(browser, $"/api/trips/{id}/trails", file);

        Assert.False(preview.GetProperty("overlapsTrip").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
    }

    [Fact]
    public async Task A_trip_can_have_several_trails_and_each_can_be_removed_with_its_file()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;
        await UploadAsync(browser, $"/api/trips/{id}/trails", Encoding.UTF8.GetBytes(Gpx(Walk(March15Utc, 20))), "day-1.gpx");
        await UploadAsync(browser, $"/api/trips/{id}/trails", Encoding.UTF8.GetBytes(Gpx(Walk(March15Utc.AddDays(1), 20))), "day-2.gpx");
        var trails = (await browser.GetJsonAsync($"/api/trips/{id}/trails")).EnumerateArray().ToList();
        Assert.Equal(["day-1.gpx", "day-2.gpx"], trails.Select(trail => trail.GetProperty("originalName").GetString()));

        var removed = await browser.SendJsonAsync(HttpMethod.Delete, $"/api/trips/{id}/trails/{trails[0].GetProperty("id").GetInt32()}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        var left = Assert.Single((await browser.GetJsonAsync($"/api/trips/{id}/trails")).EnumerateArray());
        Assert.Equal("day-2.gpx", left.GetProperty("originalName").GetString());
        Assert.Equal(1, await StoredFilesAsync(app));
    }

    [Fact]
    public async Task A_trip_that_doesnt_exist_answers_404()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;
        var file = Encoding.UTF8.GetBytes(Gpx(Walk(March15Utc, 5)));

        Assert.Equal(HttpStatusCode.NotFound, (await UploadAsync(browser, $"/api/trips/{id + 99}/trails", file)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UploadAsync(browser, $"/api/trips/{id + 99}/trails/preview", file)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/trips/{id + 99}/trails", asPage: false)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.SendJsonAsync(HttpMethod.Delete, $"/api/trips/{id}/trails/42")).StatusCode);
    }

    [Fact]
    public async Task An_upload_without_the_anti_forgery_token_is_refused()
    {
        var (app, browser, id) = await StartWithTripAsync();
        await using var _ = app;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/trips/{id}/trails") { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(Gpx(Walk(March15Utc, 5)))) };

        var response = await browser.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await StoredFilesAsync(app));
    }

    [Fact]
    public async Task A_trail_follows_its_trip_when_a_rescan_moves_the_fingerprint_and_survives_a_restart()
    {
        var (app, browser, id) = await StartWithTripAsync();
        var folder = app.Settings.DataDirectory;
        var immich = app.Immich;
        await UploadAsync(browser, $"/api/trips/{id}/trails", Encoding.UTF8.GetBytes(Gpx(Walk(March15Utc, 20))));
        var before = await app.WithDbAsync(db => db.Trips.AsNoTracking().SingleAsync());

        // Photos from the day before the trip are uploaded later. The start date and the fingerprint change.
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15.AddDays(-1), 1, 15));
        await app.ScanAsync();
        var after = await app.WithDbAsync(db => db.Trips.AsNoTracking().SingleAsync());
        Assert.NotEqual(before.Fingerprint, after.Fingerprint);
        Assert.Single((await browser.GetJsonAsync($"/api/trips/{after.Id}/trails")).EnumerateArray());

        await app.StopAsync();
        await using var restarted = await TestApp.StartAsync(folder, immich);
        var again = await restarted.SignedInBrowserAsync();
        Assert.Single((await again.GetJsonAsync($"/api/trips/{after.Id}/trails")).EnumerateArray());
        Assert.Equal(1, await StoredFilesAsync(restarted));
    }

    [Theory]
    [InlineData("day 1.gpx", "day 1.gpx")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\Users\\me\\log.gpx", "log.gpx")]
    [InlineData("", "trail.gpx")]
    [InlineData(null, "trail.gpx")]
    public void A_picked_files_name_keeps_no_folder(string? name, string expected)
    {
        Assert.Equal(expected, TrailStore.CleanName(name));
    }
}
