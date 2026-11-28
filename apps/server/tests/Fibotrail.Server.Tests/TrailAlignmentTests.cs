using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fibotrail.Server.Trails;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Tests;

public class TrailAlignmentTests
{
    private static readonly DateTime March15 = new(2027, 3, 15, 9, 0, 0);

    /// <summary>
    /// The walk starts at 20:00 UTC on the trip's first day and lasts 50 minutes: 15:53 to 16:43
    /// local. The day photos are about 2 hours apart, at 15:09 and 17:12 then, so none falls in it.
    /// </summary>
    private static readonly DateTime WalkStartUtc = new(2027, 3, 15, 20, 0, 0, DateTimeKind.Utc);

    /// <summary>Orlando in March is UTC−4, and the camera runs 7 minutes slow: photo local time = UTC − 4 h 07 min.</summary>
    private const int Offset = -(4 * 60) - 7;

    /// <summary>The walk's points where a phone photo was taken.</summary>
    private static readonly int[] PhonePhotoPoints = [60, 180, 300, 420, 540];

    private const double WalkLatitude = 28.5;
    private const double WalkLongitude = -81.4;

    /// <summary>A point of the walk: one every 5 seconds, 7 m apart, heading north.</summary>
    private static (DateTime Time, double Latitude, double Longitude) Point(int index) =>
        (WalkStartUtc.AddSeconds(index * 5), WalkLatitude + (index * 0.007 / 111.195), WalkLongitude);

    private static string Gpx(IEnumerable<int> indexes)
    {
        var text = new StringBuilder("<gpx version=\"1.1\" xmlns=\"http://www.topografix.com/GPX/1/1\"><trk><trkseg>");
        foreach (var (time, latitude, longitude) in indexes.Select(Point))
        {
            text.Append(CultureInfo.InvariantCulture, $"<trkpt lat=\"{latitude:R}\" lon=\"{longitude:R}\"><time>{time:yyyy-MM-dd'T'HH:mm:ss'Z'}</time></trkpt>");
        }

        return text.Append("</trkseg></trk></gpx>").ToString();
    }

    /// <summary>The photo's local time, as the camera shows it, for a point of the walk.</summary>
    private static DateTime LocalAt(int index) => DateTime.SpecifyKind(Point(index).Time.AddMinutes(Offset), DateTimeKind.Unspecified);

    /// <summary>
    /// A trip in Orlando with photos spread over 4 days, plus 5 phone photos taken on the walk and 2
    /// camera photos with no position taken during it.
    /// </summary>
    private static async Task<(TestApp App, Browser Browser, int Id)> StartAsync(int phonePhotos = 5)
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15, 4, 40));
        foreach (var index in PhonePhotoPoints.Take(phonePhotos))
        {
            var (_, latitude, longitude) = Point(index);
            app.Immich.Assets.Add(TripLibrary.Asset(LocalAt(index), latitude, longitude, "Orlando", "Florida", "United States of America"));
        }

        app.Immich.Assets.Add(TripLibrary.Asset(LocalAt(240), null, null, null, null, null));
        app.Immich.Assets.Add(TripLibrary.Asset(LocalAt(480).AddSeconds(2), null, null, null, null, null));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        var id = await app.WithDbAsync(db => db.Trips.Select(trip => trip.Id).SingleAsync());
        return (app, browser, id);
    }

    private static async Task<int> AddTrailAsync(Browser browser, int tripId)
    {
        var token = (await browser.GetJsonAsync("/api/session")).GetProperty("csrfToken").GetString()!;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/trips/{tripId}/trails?name=walk.gpx")
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes(Gpx(Enumerable.Range(0, 600)))),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/gpx+xml");
        request.Headers.Add("X-CSRF-Token", token);
        var response = await browser.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<HttpResponseMessage> SetOffsetAsync(Browser browser, int tripId, int trailId, int? minutes)
    {
        await browser.GetJsonAsync("/api/session");
        return await browser.SendJsonAsync(HttpMethod.Put, $"/api/trips/{tripId}/trails/{trailId}/offset", new OffsetChange(minutes));
    }

    [Fact]
    public void The_test_trip_is_4_hours_7_minutes_behind_UTC()
    {
        Assert.Equal(-247, Offset);
        Assert.Equal(new DateTime(2027, 3, 15, 15, 53, 0), LocalAt(0));
    }

    [Fact]
    public async Task The_offset_is_estimated_from_the_phone_photos_and_every_photo_on_the_walk_lands_on_the_trail()
    {
        var (app, browser, id) = await StartAsync();
        await using var _ = app;
        var trailId = await AddTrailAsync(browser, id);

        var alignment = await browser.GetJsonAsync($"/api/trips/{id}/trails/alignment");

        var trail = Assert.Single(alignment.GetProperty("trails").EnumerateArray());
        Assert.Equal(trailId, trail.GetProperty("trailId").GetInt32());
        Assert.Equal(Offset, trail.GetProperty("offsetMinutes").GetInt32());
        Assert.Equal(Offset, trail.GetProperty("estimatedOffsetMinutes").GetInt32());
        Assert.Equal("estimated", trail.GetProperty("source").GetString());
        var photos = trail.GetProperty("photos").EnumerateArray().ToList();
        Assert.Equal(7, photos.Count);
        Assert.Equal([false, false, true, false, false, true, false], photos.Select(photo => photo.GetProperty("estimated").GetBoolean()));
        var times = photos.Select(photo => photo.GetProperty("localTime").GetString()).ToList();
        Assert.Equal(times.Order(StringComparer.Ordinal), times);

        // The camera photo at point 240 lands on point 240.
        var camera = photos[2];
        Assert.Equal(Point(240).Latitude, camera.GetProperty("latitude").GetDouble(), 6);

        // The 40 day photos fall outside the 50-minute walk.
        Assert.Equal(47, alignment.GetProperty("photoCount").GetInt32());
        Assert.Equal(40, alignment.GetProperty("outside").GetArrayLength());
    }

    [Fact]
    public async Task A_trial_offset_moves_the_photos_without_saving()
    {
        var (app, browser, id) = await StartAsync();
        await using var _ = app;
        var trailId = await AddTrailAsync(browser, id);

        var walkPhotos = (await browser.GetJsonAsync($"/api/trips/{id}/trails/alignment")).GetProperty("trails")[0]
            .GetProperty("photos").EnumerateArray().Select(photo => photo.GetProperty("id").GetString()).ToList();

        // Three hours off puts the walk's own photos outside it. A day photo may land on it instead.
        var trial = await browser.GetJsonAsync($"/api/trips/{id}/trails/alignment?trailId={trailId}&offsetMinutes={Offset + 180}");

        var trail = Assert.Single(trial.GetProperty("trails").EnumerateArray());
        Assert.Equal("trial", trail.GetProperty("source").GetString());
        Assert.Equal(Offset + 180, trail.GetProperty("offsetMinutes").GetInt32());
        Assert.Equal(7, walkPhotos.Count);
        Assert.DoesNotContain(trail.GetProperty("photos").EnumerateArray(), photo => walkPhotos.Contains(photo.GetProperty("id").GetString()));
        Assert.Null(await app.WithDbAsync(db => db.Trails.Select(row => row.OffsetMinutes).SingleAsync()));
    }

    [Fact]
    public async Task An_offset_you_set_wins_survives_a_rescan_and_a_restart_and_Use_estimate_removes_it()
    {
        var (app, browser, id) = await StartAsync();
        var folder = app.Settings.DataDirectory;
        var immich = app.Immich;
        var trailId = await AddTrailAsync(browser, id);

        Assert.Equal(HttpStatusCode.NoContent, (await SetOffsetAsync(browser, id, trailId, Offset + 1)).StatusCode);
        await app.ScanAsync();
        await app.StopAsync();

        await using var restarted = await TestApp.StartAsync(folder, immich);
        var again = await restarted.SignedInBrowserAsync();
        var trail = (await again.GetJsonAsync($"/api/trips/{id}/trails/alignment")).GetProperty("trails")[0];
        Assert.Equal(("you", Offset + 1, Offset), (trail.GetProperty("source").GetString(), trail.GetProperty("offsetMinutes").GetInt32(), trail.GetProperty("estimatedOffsetMinutes").GetInt32()));

        Assert.Equal(HttpStatusCode.NoContent, (await SetOffsetAsync(again, id, trailId, null)).StatusCode);
        trail = (await again.GetJsonAsync($"/api/trips/{id}/trails/alignment")).GetProperty("trails")[0];
        Assert.Equal(("estimated", Offset), (trail.GetProperty("source").GetString(), trail.GetProperty("offsetMinutes").GetInt32()));
    }

    [Fact]
    public async Task With_fewer_than_3_phone_photos_the_offset_is_0_and_not_estimated()
    {
        var (app, browser, id) = await StartAsync(phonePhotos: 2);
        await using var _ = app;
        await AddTrailAsync(browser, id);

        var trail = (await browser.GetJsonAsync($"/api/trips/{id}/trails/alignment")).GetProperty("trails")[0];

        Assert.Equal("none", trail.GetProperty("source").GetString());
        Assert.Equal(0, trail.GetProperty("offsetMinutes").GetInt32());
        Assert.Equal(JsonValueKind.Null, trail.GetProperty("estimatedOffsetMinutes").ValueKind);
    }

    [Fact]
    public async Task An_offset_beyond_14_hours_or_for_an_unknown_trail_is_refused()
    {
        var (app, browser, id) = await StartAsync();
        await using var _ = app;
        var trailId = await AddTrailAsync(browser, id);

        Assert.Equal(HttpStatusCode.BadRequest, (await SetOffsetAsync(browser, id, trailId, (14 * 60) + 1)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetOffsetAsync(browser, id, trailId + 1, 0)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/trips/{id + 1}/trails/alignment", asPage: false)).StatusCode);
    }

    [Fact]
    public async Task A_trail_and_its_offset_leave_the_stops_distance_and_photo_positions_as_they_were()
    {
        var (app, browser, id) = await StartAsync();
        await using var _ = app;
        var before = await browser.GetJsonAsync($"/api/trips/{id}");

        var trailId = await AddTrailAsync(browser, id);
        await SetOffsetAsync(browser, id, trailId, Offset);
        await browser.GetJsonAsync($"/api/trips/{id}/trails/alignment");
        await app.ScanAsync();

        var after = await browser.GetJsonAsync($"/api/trips/{id}");
        Assert.Equal(before.GetProperty("stats").GetRawText(), after.GetProperty("stats").GetRawText());
        Assert.Equal(before.GetProperty("stops").GetRawText(), after.GetProperty("stops").GetRawText());
        Assert.Equal(2, await app.WithDbAsync(db => db.Photos.CountAsync(photo => photo.Latitude == null && photo.LocalTime > new DateTime(2027, 3, 15, 15, 50, 0) && photo.LocalTime < new DateTime(2027, 3, 15, 16, 45, 0))));
    }
}
