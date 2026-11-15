using System.Text.Json;
using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Tests;

public class TripLibraryTests
{
    /// <summary>Five trips: three added, one left as a suggestion, and one ignored.</summary>
    private static async Task<(TestApp App, Browser Browser)> StartWithFiveTripsAsync()
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 10, 300));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 5, 1, 9, 0, 0), 3, 30, 51.05, -114.07, "Calgary", "Alberta", "Canada"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 7, 3, 9, 0, 0), 4, 60, 49.25, -120.35, "Lakeside", "British Columbia", "Canada"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 9, 1, 9, 0, 0), 6, 40, 48.85, 2.35, "Paris", "Île-de-France", "France"));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 11, 10, 9, 0, 0), 2, 25, 47.6, -117.4, "Spokane", "Washington", "United States of America"));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);

        var inbox = await browser.GetJsonAsync("/api/inbox");
        foreach (var card in inbox.GetProperty("suggestions").EnumerateArray())
        {
            var action = card.GetProperty("place").GetString() switch
            {
                "Calgary" => "ignore",
                "Spokane" => null,
                _ => "add",
            };
            if (action is not null)
            {
                await browser.SendJsonAsync(HttpMethod.Post, $"/api/trips/{card.GetProperty("id").GetInt32()}/decision", new { action });
            }
        }

        return (app, browser);
    }

    [Fact]
    public async Task The_library_lists_added_trips_only_newest_first()
    {
        var (app, browser) = await StartWithFiveTripsAsync();
        await using var _ = app;

        var trips = await browser.GetJsonAsync("/api/trips");

        Assert.Equal(["Paris", "Lakeside", "Orlando"], trips.EnumerateArray().Select(trip => trip.GetProperty("place").GetString()));
        Assert.Equal(5, await app.WithDbAsync(db => db.Trips.CountAsync()));
    }

    [Fact]
    public async Task A_trip_in_the_list_has_what_its_card_shows()
    {
        var (app, browser) = await StartWithFiveTripsAsync();
        await using var _ = app;

        var trips = await browser.GetJsonAsync("/api/trips");
        var orlando = trips.EnumerateArray().Single(trip => trip.GetProperty("place").GetString() == "Orlando");
        var stored = await app.WithDbAsync(db => db.Trips.AsNoTracking().SingleAsync(trip => trip.MainPlace == "Orlando"));

        Assert.Equal(stored.Id, orlando.GetProperty("id").GetInt32());
        Assert.Equal("Orlando, March 2027", orlando.GetProperty("name").GetString());
        Assert.Equal("2027-03-15", orlando.GetProperty("startDate").GetString());
        Assert.Equal("2027-03-24", orlando.GetProperty("endDate").GetString());
        Assert.Equal(10, orlando.GetProperty("days").GetInt32());
        Assert.Equal(stored.CoverPhotoId, orlando.GetProperty("coverPhotoId").GetString());
    }

    [Fact]
    public async Task With_no_added_trips_the_list_is_empty()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();

        Assert.Equal(JsonValueKind.Array, (await browser.GetJsonAsync("/api/trips")).ValueKind);
        Assert.Equal(0, (await browser.GetJsonAsync("/api/trips")).GetArrayLength());
    }

    [Fact]
    public async Task Two_hundred_trips_are_listed_in_well_under_a_second()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        await app.WithDbAsync(async db =>
        {
            db.Trips.AddRange(Enumerable.Range(0, 200).Select(index => new Trip
            {
                Fingerprint = $"fingerprint-{index}",
                Status = TripStatus.Added,
                Name = $"Trip {index}",
                SuggestedName = $"Trip {index}",
                CoverPhotoId = "cover",
                SuggestedCoverPhotoId = "cover",
                StartDate = new DateOnly(2010, 1, 1).AddDays(index * 30),
                EndDate = new DateOnly(2010, 1, 5).AddDays(index * 30),
                Days = 5,
                StopsJson = "[]",
            }));
            return await db.SaveChangesAsync();
        });
        await browser.GetJsonAsync("/api/trips");

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var trips = await browser.GetJsonAsync("/api/trips");
        watch.Stop();

        Assert.Equal(200, trips.GetArrayLength());
        Assert.Equal("Trip 199", trips[0].GetProperty("name").GetString());
        Assert.True(watch.ElapsedMilliseconds < 500, $"The list took {watch.ElapsedMilliseconds} ms.");
    }
}
