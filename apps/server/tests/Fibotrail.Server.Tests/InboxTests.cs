using System.Net;
using System.Text.Json;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Tests;

public class InboxTests
{
    private static readonly DateTime March15 = new(2027, 3, 15, 9, 0, 0);
    private static readonly DateTime July3 = new(2027, 7, 3, 9, 0, 0);
    private static readonly DateTime September1 = new(2027, 9, 1, 9, 0, 0);

    /// <summary>A library with three trips: Orlando in March, Lakeside in July, and Paris in September.</summary>
    private static async Task<(TestApp App, Browser Browser)> StartWithThreeTripsAsync()
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15, 10, 300));
        app.Immich.Assets.AddRange(TripLibrary.Camera(March15.AddHours(2), 9, 12));
        app.Immich.Assets.AddRange(TripLibrary.Trip(July3, 4, 60, 49.25, -120.35, "Lakeside", "British Columbia", "Canada"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(September1, 6, 22, 48.85, 2.35, "Paris", "Île-de-France", "France"));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);
        return (app, browser);
    }

    private static async Task<JsonElement> InboxAsync(Browser browser) => await browser.GetJsonAsync("/api/inbox");

    private static int IdOf(JsonElement inbox, string place, string list = "suggestions") =>
        inbox.GetProperty(list).EnumerateArray().Single(card => card.GetProperty("place").GetString() == place).GetProperty("id").GetInt32();

    private static Task<HttpResponseMessage> DecideAsync(Browser browser, int id, object body) =>
        browser.SendJsonAsync(HttpMethod.Post, $"/api/trips/{id}/decision", body);

    private static List<string?> Places(JsonElement inbox, string list = "suggestions") =>
        [.. inbox.GetProperty(list).EnumerateArray().Select(card => card.GetProperty("place").GetString())];

    [Fact]
    public async Task A_suggestion_card_has_what_the_inbox_shows()
    {
        var (app, browser) = await StartWithThreeTripsAsync();
        await using var _ = app;

        var inbox = await InboxAsync(browser);
        var card = inbox.GetProperty("suggestions").EnumerateArray().Single(candidate => candidate.GetProperty("place").GetString() == "Orlando");

        Assert.Equal("Orlando, March 2027", card.GetProperty("name").GetString());
        Assert.Equal("2027-03-15", card.GetProperty("startDate").GetString());
        Assert.Equal("2027-03-24", card.GetProperty("endDate").GetString());
        Assert.Equal(312, card.GetProperty("photoCount").GetInt32());
        Assert.Equal("high", card.GetProperty("confidence").GetString());
        Assert.Equal("suggested", card.GetProperty("status").GetString());
        Assert.Equal(["Orlando"], card.GetProperty("places").EnumerateArray().Select(place => place.GetString()));
        var route = Assert.Single(card.GetProperty("route").EnumerateArray());
        Assert.Equal((28.4, 300), (Math.Round(route.GetProperty("latitude").GetDouble(), 2), route.GetProperty("photoCount").GetInt32()));
    }

    [Fact]
    public async Task The_photo_strip_has_eight_photos_spread_across_the_days_with_alt_text()
    {
        var (app, browser) = await StartWithThreeTripsAsync();
        await using var _ = app;

        var inbox = await InboxAsync(browser);
        var strip = inbox.GetProperty("suggestions").EnumerateArray()
            .Single(card => card.GetProperty("place").GetString() == "Orlando")
            .GetProperty("strip").EnumerateArray().ToList();

        Assert.Equal(8, strip.Count);
        var alts = strip.Select(photo => photo.GetProperty("alt").GetString()!).ToList();
        Assert.Equal("Photo taken Mar 15, 2027 in Orlando", alts[0]);
        Assert.StartsWith("Photo taken Mar 24, 2027", alts[7], StringComparison.Ordinal);
        // Eight different days of a ten-day trip.
        Assert.Equal(8, alts.Select(alt => alt[..24]).Distinct().Count());
    }

    [Fact]
    public async Task New_suggestions_come_first_then_later_ones_newest_trip_first_and_the_badge_counts_new_ones_only()
    {
        var (app, browser) = await StartWithThreeTripsAsync();
        await using var _ = app;
        var before = await InboxAsync(browser);

        // Paris, the newest trip, is set aside with Later.
        var response = await DecideAsync(browser, IdOf(before, "Paris"), new { action = "later" });
        var after = await InboxAsync(browser);
        var count = await browser.GetJsonAsync("/api/inbox/count");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["Paris", "Lakeside", "Orlando"], Places(before));
        Assert.Equal(3, before.GetProperty("count").GetInt32());
        Assert.Equal(["Lakeside", "Orlando", "Paris"], Places(after));
        Assert.Equal(2, after.GetProperty("count").GetInt32());
        Assert.Equal(2, count.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Add_trip_with_a_new_name_and_cover_removes_the_card_and_saves_both_as_a_decision()
    {
        var (app, browser) = await StartWithThreeTripsAsync();
        await using var _ = app;
        var id = IdOf(await InboxAsync(browser), "Orlando");
        var photos = await browser.GetJsonAsync($"/api/trips/{id}/photos");
        var cover = photos[150].GetProperty("id").GetString();

        var response = await DecideAsync(browser, id, new { action = "add", name = "  Disney with the kids ", coverPhotoId = cover });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(312, photos.GetArrayLength());
        Assert.Equal(["Paris", "Lakeside"], Places(await InboxAsync(browser)));
        var trip = await app.WithDbAsync(db => db.Trips.AsNoTracking().SingleAsync(candidate => candidate.Id == id));
        Assert.Equal((TripStatus.Added, "Disney with the kids", cover), (trip.Status, trip.Name, trip.CoverPhotoId));
        var decision = await app.WithDbAsync(db => db.Decisions.AsNoTracking().SingleAsync());
        Assert.Equal((trip.Fingerprint, TripStatus.Added, "Disney with the kids", cover), (decision.Fingerprint, decision.Status, decision.Name, decision.CoverPhotoId));
    }

    [Fact]
    public async Task Add_trip_with_the_suggested_name_and_cover_stores_no_name_so_a_better_suggestion_can_replace_it()
    {
        var (app, browser) = await StartWithThreeTripsAsync();
        await using var _ = app;
        var inbox = await InboxAsync(browser);
        var card = inbox.GetProperty("suggestions").EnumerateArray().Single(candidate => candidate.GetProperty("place").GetString() == "Orlando");

        await DecideAsync(browser, card.GetProperty("id").GetInt32(), new { action = "add", name = "Orlando, March 2027", coverPhotoId = card.GetProperty("coverPhotoId").GetString() });

        var decision = await app.WithDbAsync(db => db.Decisions.AsNoTracking().SingleAsync());
        Assert.Equal((TripStatus.Added, null, null), (decision.Status, decision.Name, decision.CoverPhotoId));
    }

    [Fact]
    public async Task Not_a_trip_moves_the_card_to_ignored_and_restore_brings_it_back()
    {
        var (app, browser) = await StartWithThreeTripsAsync();
        await using var _ = app;
        var id = IdOf(await InboxAsync(browser), "Lakeside");

        await DecideAsync(browser, id, new { action = "ignore" });
        var ignored = await InboxAsync(browser);
        await DecideAsync(browser, IdOf(ignored, "Lakeside", "ignored"), new { action = "restore" });
        var restored = await InboxAsync(browser);

        Assert.Equal(["Paris", "Orlando"], Places(ignored));
        Assert.Equal(["Lakeside"], Places(ignored, "ignored"));
        Assert.Equal(2, ignored.GetProperty("count").GetInt32());
        Assert.Equal(["Paris", "Lakeside", "Orlando"], Places(restored));
        Assert.Empty(Places(restored, "ignored"));
        Assert.Equal(3, restored.GetProperty("count").GetInt32());
    }

    [Theory]
    [InlineData("add")]
    [InlineData("ignore")]
    [InlineData("later")]
    public async Task A_rescan_after_each_decision_changes_nothing(string action)
    {
        var (app, browser) = await StartWithThreeTripsAsync();
        await using var _ = app;
        await DecideAsync(browser, IdOf(await InboxAsync(browser), "Orlando"), new { action, name = action == "add" ? "Disney" : null });
        var before = (await TripLibrary.TripsAsync(app)).Select(trip => (trip.Id, trip.Status, trip.Name, trip.CoverPhotoId)).ToList();
        var inboxBefore = (await InboxAsync(browser)).GetRawText();

        // More photos arrive for the same trip, and the library is scanned twice.
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15.AddDays(10), 2, 50));
        await app.ScanAsync();
        await app.ScanAsync();

        var after = (await TripLibrary.TripsAsync(app)).Select(trip => (trip.Id, trip.Status, trip.Name, trip.CoverPhotoId)).ToList();
        Assert.Equal(before, after);
        var inboxAfter = await InboxAsync(browser);
        Assert.Equal(JsonDocument.Parse(inboxBefore).RootElement.GetProperty("count").GetInt32(), inboxAfter.GetProperty("count").GetInt32());
        Assert.Equal(action == "ignore" ? ["Orlando"] : [], Places(inboxAfter, "ignored"));
        Assert.Equal(action == "add" ? 2 : action == "ignore" ? 2 : 3, inboxAfter.GetProperty("suggestions").GetArrayLength());
    }

    [Fact]
    public async Task After_restore_a_rescan_keeps_the_card_in_the_inbox()
    {
        var (app, browser) = await StartWithThreeTripsAsync();
        await using var _ = app;
        var id = IdOf(await InboxAsync(browser), "Orlando");
        await DecideAsync(browser, id, new { action = "ignore" });
        await DecideAsync(browser, id, new { action = "restore" });

        await app.ScanAsync();

        Assert.Equal(["Paris", "Lakeside", "Orlando"], Places(await InboxAsync(browser)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_name_is_refused_and_nothing_changes(string name)
    {
        var (app, browser) = await StartWithThreeTripsAsync();
        await using var _ = app;
        var id = IdOf(await InboxAsync(browser), "Orlando");

        var response = await DecideAsync(browser, id, new { action = "add", name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Give the trip a name", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, await app.WithDbAsync(db => db.Decisions.CountAsync()));
        Assert.Equal(3, (await InboxAsync(browser)).GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task A_cover_from_another_trip_is_refused()
    {
        var (app, browser) = await StartWithThreeTripsAsync();
        await using var _ = app;
        var inbox = await InboxAsync(browser);
        var other = (await browser.GetJsonAsync($"/api/trips/{IdOf(inbox, "Paris")}/photos"))[0].GetProperty("id").GetString();

        var response = await DecideAsync(browser, IdOf(inbox, "Orlando"), new { action = "add", coverPhotoId = other });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await app.WithDbAsync(db => db.Decisions.CountAsync()));
    }

    [Fact]
    public async Task A_decision_for_a_trip_that_doesnt_exist_is_not_found()
    {
        var (app, browser) = await StartWithThreeTripsAsync();
        await using var _ = app;

        var decision = await DecideAsync(browser, 9999, new { action = "ignore" });
        var photos = await browser.GetAsync("/api/trips/9999/photos", asPage: false);

        Assert.Equal(HttpStatusCode.NotFound, decision.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, photos.StatusCode);
    }

    [Fact]
    public async Task A_decision_needs_the_anti_forgery_token()
    {
        var (app, browser) = await StartWithThreeTripsAsync();
        await using var _ = app;
        var id = IdOf(await InboxAsync(browser), "Orlando");
        browser.ForgetCsrfToken();

        var response = await DecideAsync(browser, id, new { action = "ignore" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await app.WithDbAsync(db => db.Decisions.CountAsync()));
    }

    [Fact]
    public async Task With_no_trips_the_inbox_is_empty()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();

        var inbox = await InboxAsync(browser);

        Assert.Equal(0, inbox.GetProperty("suggestions").GetArrayLength());
        Assert.Equal(0, inbox.GetProperty("count").GetInt32());
    }
}

public class PhotoStripTests
{
    private static List<(int Number, DateOnly Day)> Photos(params int[] perDay) =>
        [.. perDay.SelectMany((count, day) => Enumerable.Range(0, count).Select(index => ((day * 1000) + index, new DateOnly(2027, 3, 1).AddDays(day))))];

    [Fact]
    public void With_more_days_than_slots_the_days_are_evenly_spaced_from_first_to_last()
    {
        var picked = PhotoStrip.Pick(Photos(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5), photo => photo.Day, 8);

        Assert.Equal([0, 2, 4, 6, 8, 10, 12, 14], picked.Select(photo => photo.Number / 1000));
    }

    [Fact]
    public void With_fewer_days_than_slots_each_day_gives_several_photos_spread_across_it()
    {
        var picked = PhotoStrip.Pick(Photos(100, 100), photo => photo.Day, 8);

        Assert.Equal([0, 33, 66, 99, 1000, 1033, 1066, 1099], picked.Select(photo => photo.Number));
    }

    [Fact]
    public void A_day_with_few_photos_gives_its_spare_slots_to_the_others()
    {
        var picked = PhotoStrip.Pick(Photos(1, 50, 2), photo => photo.Day, 8);

        Assert.Equal(8, picked.Count);
        Assert.Equal([1, 5, 2], picked.GroupBy(photo => photo.Number / 1000).Select(group => group.Count()));
    }

    [Fact]
    public void A_trip_with_few_photos_shows_them_all()
    {
        Assert.Equal(5, PhotoStrip.Pick(Photos(2, 3), photo => photo.Day, 8).Count);
    }
}
