using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fibotrail.Server.Tests;

/// <summary>Photos you put on the way stay in their trip and leave its map. See docs/specs/on-the-way.md.</summary>
public class OnTheWayServerTests
{
    private static readonly DateTime March14 = new(2027, 3, 14, 20, 0, 0);
    private static readonly DateTime March15 = new(2027, 3, 15, 9, 0, 0);

    /// <summary>A trip that starts with a London layover and goes on to Orlando, plus a second trip.</summary>
    private static async Task<(TestApp App, Browser Browser, int TripId, List<string> LayoverIds)> StartAsync()
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(March14, 1, 6, 51.47, -0.45, "Hounslow", "England", "United Kingdom"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15, 6, 60));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 7, 3, 9, 0, 0), 4, 60, 49.25, -120.35, "Lakeside", "British Columbia", "Canada"));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        var trips = await TripLibrary.TripsAsync(app);
        var layover = await app.WithDbAsync(db => db.Photos.AsNoTracking().Where(photo => photo.City == "Hounslow").OrderBy(photo => photo.LocalTime).Select(photo => photo.Id).ToListAsync());
        return (app, browser, trips[0].Id, layover);
    }

    private static Task<HttpResponseMessage> SetAsync(Browser browser, string photoId, bool onTheWay) =>
        browser.SendJsonAsync(HttpMethod.Put, $"/api/photos/{photoId}/on-the-way", new { onTheWay });

    private static async Task<JsonElement> TripAsync(Browser browser, int id) => await browser.GetJsonAsync($"/api/trips/{id}");

    private static IEnumerable<JsonElement> PhotosOf(JsonElement trip) =>
        trip.GetProperty("days").EnumerateArray().SelectMany(day => day.GetProperty("photos").EnumerateArray());

    [Fact]
    public async Task A_layover_is_a_stop_until_every_one_of_its_photos_is_on_the_way()
    {
        var (app, browser, tripId, layover) = await StartAsync();
        await using var _ = app;
        var before = await TripAsync(browser, tripId);
        Assert.Equal(["Hounslow", "Orlando"], before.GetProperty("stops").EnumerateArray().Select(stop => stop.GetProperty("place").GetString()));
        Assert.True(before.GetProperty("stats").GetProperty("distanceKm").GetDouble() > 1000);

        // Five of the six photos: the stop stays, with one photo.
        foreach (var id in layover.Take(5))
        {
            Assert.Equal(HttpStatusCode.OK, (await SetAsync(browser, id, true)).StatusCode);
        }

        var some = await TripAsync(browser, tripId);
        Assert.Equal(2, some.GetProperty("stops").GetArrayLength());
        Assert.Equal(1, some.GetProperty("stops")[0].GetProperty("photoCount").GetInt32());

        // The sixth: the stop, its line, and the distance go.
        var response = await SetAsync(browser, layover[5], true);
        var after = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(["Orlando"], after.GetProperty("stops").EnumerateArray().Select(stop => stop.GetProperty("place").GetString()));
        Assert.Equal(0, after.GetProperty("stats").GetProperty("distanceKm").GetDouble());
        Assert.Equal(1, after.GetProperty("stats").GetProperty("stops").GetInt32());
        Assert.True(after.GetProperty("stats").GetProperty("farthestFromHomeKm").GetDouble() < before.GetProperty("stats").GetProperty("farthestFromHomeKm").GetDouble());
    }

    [Fact]
    public async Task Photos_on_the_way_stay_in_the_trip_with_a_flag_and_an_alt_text_that_says_so()
    {
        var (app, browser, tripId, layover) = await StartAsync();
        await using var _ = app;
        var before = await TripAsync(browser, tripId);

        foreach (var id in layover)
        {
            await SetAsync(browser, id, true);
        }

        var after = await TripAsync(browser, tripId);
        Assert.Equal(before.GetProperty("stats").GetProperty("photos").GetInt32(), after.GetProperty("stats").GetProperty("photos").GetInt32());
        Assert.Equal(PhotosOf(before).Count(), PhotosOf(after).Count());
        var hidden = PhotosOf(after).Where(photo => photo.GetProperty("onTheWay").GetBoolean()).ToList();
        Assert.Equal(layover.Order(StringComparer.Ordinal), hidden.Select(photo => photo.GetProperty("id").GetString()!).Order(StringComparer.Ordinal));
        Assert.All(hidden, photo => Assert.EndsWith(", hidden from the map", photo.GetProperty("alt").GetString(), StringComparison.Ordinal));
        Assert.All(PhotosOf(after).Where(photo => !photo.GetProperty("onTheWay").GetBoolean()), photo => Assert.DoesNotContain("hidden", photo.GetProperty("alt").GetString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Bringing_the_photos_back_restores_the_stop()
    {
        var (app, browser, tripId, layover) = await StartAsync();
        await using var _ = app;
        var before = await TripAsync(browser, tripId);
        foreach (var id in layover)
        {
            await SetAsync(browser, id, true);
        }

        foreach (var id in layover)
        {
            await SetAsync(browser, id, false);
        }

        var after = await TripAsync(browser, tripId);
        Assert.Equal(before.GetProperty("stops").EnumerateArray().Select(stop => stop.GetProperty("place").GetString()), after.GetProperty("stops").EnumerateArray().Select(stop => stop.GetProperty("place").GetString()));
        Assert.Equal(before.GetProperty("stats").GetProperty("distanceKm").GetDouble(), after.GetProperty("stats").GetProperty("distanceKm").GetDouble());
        Assert.DoesNotContain(PhotosOf(after), photo => photo.GetProperty("onTheWay").GetBoolean());
        Assert.Equal(0, await app.WithDbAsync(db => db.OnTheWayPhotos.CountAsync()));
    }

    [Fact]
    public async Task The_choice_survives_a_rescan_and_a_full_rebuild_of_the_detected_data()
    {
        var (app, browser, tripId, layover) = await StartAsync();
        await using var _ = app;
        foreach (var id in layover)
        {
            await SetAsync(browser, id, true);
        }

        // A rescan that reads a changed photo, then detection again.
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15.AddDays(6), 1, 4));
        await app.ScanAsync();
        // Delete every trip and trip photo, and build them again from the photo table.
        await app.WithDbAsync(db => db.Trips.ExecuteDeleteAsync());
        await app.WithServicesAsync(services => services.GetRequiredService<DetectionService>().RunAsync(CancellationToken.None));

        var trip = (await TripLibrary.TripsAsync(app)).First(candidate => candidate.StartDate == new DateOnly(2027, 3, 14));
        var stops = DetectionService.ReadStops(trip.StopsJson);
        Assert.Equal(["Orlando"], stops.Select(stop => stop.Place));
        Assert.Equal(0, trip.DistanceKm);
        var detail = await TripAsync(browser, trip.Id);
        Assert.Equal(layover.Count, PhotosOf(detail).Count(photo => photo.GetProperty("onTheWay").GetBoolean()));
        Assert.Equal(layover.Count, await app.WithDbAsync(db => db.OnTheWayPhotos.CountAsync()));
        Assert.True(tripId > 0);
    }

    [Fact]
    public async Task Only_the_trip_of_the_photo_changes()
    {
        var (app, browser, _, layover) = await StartAsync();
        await using var _ = app;
        var other = (await TripLibrary.TripsAsync(app)).Single(trip => trip.StartDate == new DateOnly(2027, 7, 3));

        foreach (var id in layover)
        {
            await SetAsync(browser, id, true);
        }

        var after = (await TripLibrary.TripsAsync(app)).Single(trip => trip.Id == other.Id);
        Assert.Equal((other.StopsJson, other.DistanceKm, other.FarthestFromHomeKm, other.CoverPhotoId), (after.StopsJson, after.DistanceKm, after.FarthestFromHomeKm, after.CoverPhotoId));
    }

    [Fact]
    public async Task A_cover_you_chose_stays_when_stops_change()
    {
        var (app, browser, tripId, layover) = await StartAsync();
        await using var _ = app;
        var cover = layover[2];
        var change = await browser.SendJsonAsync(HttpMethod.Put, $"/api/trips/{tripId}", new { coverPhotoId = cover });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        foreach (var id in layover)
        {
            await SetAsync(browser, id, true);
        }

        var trip = (await TripLibrary.TripsAsync(app)).Single(candidate => candidate.Id == tripId);
        Assert.Equal(cover, trip.CoverPhotoId);
    }

    [Fact]
    public async Task A_photo_that_is_in_no_trip_is_not_found()
    {
        var (app, browser, _, _) = await StartAsync();
        await using var _ = app;
        var inTrip = await app.WithDbAsync(db => db.TripPhotos.AsNoTracking().Select(link => link.PhotoId).ToListAsync());
        var atHome = await app.WithDbAsync(db => db.Photos.AsNoTracking().Select(photo => photo.Id).ToListAsync());
        var free = atHome.Except(inTrip).First();

        Assert.Equal(HttpStatusCode.NotFound, (await SetAsync(browser, free, true)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetAsync(browser, "no-such-photo", true)).StatusCode);
        Assert.Equal(0, await app.WithDbAsync(db => db.OnTheWayPhotos.CountAsync()));
    }

    [Fact]
    public async Task The_route_needs_a_session()
    {
        var (app, _, _, layover) = await StartAsync();
        await using var _ = app;

        var response = await SetAsync(app.NewBrowser(), layover[0], true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
