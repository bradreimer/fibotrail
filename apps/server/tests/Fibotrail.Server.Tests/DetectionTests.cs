using System.Net;
using Fibotrail.Immich.Tests;
using Fibotrail.Server.Cli;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fibotrail.Server.Tests;

/// <summary>A made-up library for detection tests: a home, and trips to add to it.</summary>
public static class TripLibrary
{
    public const double HomeLatitude = 49.25;
    public const double HomeLongitude = -123.1;

    private static int _next = 100000;

    /// <summary>
    /// Photos at home on 60 days in a row, so the home suggestion has something to find. They end
    /// before any trip in these tests starts, because a photo at home ends a trip.
    /// </summary>
    public static IEnumerable<FakeAsset> AtHome() =>
        Enumerable.Range(0, 60).Select(day => Asset(
            new DateTime(2026, 11, 1, 12, 0, 0).AddDays(day), HomeLatitude, HomeLongitude, "Vancouver", "British Columbia", "Canada"));

    /// <summary>A trip: photos spread over some days at one place, far from home.</summary>
    public static List<FakeAsset> Trip(DateTime first, int days, int photos, double latitude = 28.4, double longitude = -81.55, string city = "Orlando", string state = "Florida", string country = "United States of America")
    {
        var step = TimeSpan.FromHours(((days - 1) * 24.0) + 8) / Math.Max(1, photos - 1);
        return [.. Enumerable.Range(0, photos).Select(index => Asset(first + (step * index), latitude, longitude, city, state, country))];
    }

    /// <summary>Photos with no position, as a camera takes them.</summary>
    public static List<FakeAsset> Camera(DateTime first, int days, int photos)
    {
        var step = TimeSpan.FromHours(((days - 1) * 24.0) + 6) / Math.Max(1, photos - 1);
        return [.. Enumerable.Range(0, photos).Select(index => Asset(first + (step * index), null, null, null, null, null))];
    }

    public static FakeAsset Asset(DateTime local, double? latitude, double? longitude, string? city, string? state, string? country) =>
        new(FakeAsset.IdFor(Interlocked.Increment(ref _next)), local.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture))
        {
            Latitude = latitude,
            Longitude = longitude,
            City = city,
            State = state,
            Country = country,
            // Each asset is newer than the last, so a later scan picks up what was added.
            UpdatedAt = new DateTime(2028, 1, 1).AddSeconds(_next).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
        };

    /// <summary>Scans, signs in, and confirms the home location, which runs detection.</summary>
    public static async Task<Browser> ScanAndConfirmHomeAsync(TestApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var browser = await app.SignedInBrowserAsync();
        await app.ScanAsync();
        await browser.GetJsonAsync("/api/session");
        var response = await browser.SendJsonAsync(HttpMethod.Put, "/api/home", new { latitude = HomeLatitude, longitude = HomeLongitude });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return browser;
    }

    public static Task<List<Trip>> TripsAsync(TestApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.WithDbAsync(db => db.Trips.AsNoTracking().Include(trip => trip.Photos).OrderBy(trip => trip.StartDate).ToListAsync());
    }
}

public class HomeLocationTests
{
    [Fact]
    public async Task After_the_first_scan_a_home_location_is_suggested_and_no_detection_runs_until_one_is_confirmed()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 10, 300));

        await app.ScanAsync();
        var home = await browser.GetJsonAsync("/api/home");

        Assert.Equal(System.Text.Json.JsonValueKind.Null, home.GetProperty("confirmed").ValueKind);
        var suggestion = home.GetProperty("suggestion");
        Assert.Equal(TripLibrary.HomeLatitude, suggestion.GetProperty("latitude").GetDouble(), 3);
        Assert.Equal("Vancouver, British Columbia, Canada", suggestion.GetProperty("name").GetString());
        Assert.Equal(60, suggestion.GetProperty("days").GetInt32());
        Assert.True(home.GetProperty("hasPositions").GetBoolean());
        Assert.Empty(await TripLibrary.TripsAsync(app));
    }

    [Fact]
    public async Task Confirming_the_home_location_saves_it_and_runs_detection()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 10, 300));
        var browser = await app.SignedInBrowserAsync();
        await app.ScanAsync();
        await browser.GetJsonAsync("/api/session");

        var response = await browser.SendJsonAsync(HttpMethod.Put, "/api/home", new { latitude = TripLibrary.HomeLatitude, longitude = TripLibrary.HomeLongitude });
        using var saved = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var home = await browser.GetJsonAsync("/api/home");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, saved.RootElement.GetProperty("trips").GetInt32());
        Assert.Equal(1, saved.RootElement.GetProperty("newSuggestions").GetInt32());
        Assert.Equal("Vancouver, British Columbia, Canada", home.GetProperty("confirmed").GetProperty("name").GetString());
        Assert.Equal(3, await app.WithDbAsync(db => db.Settings.CountAsync()));
        var trip = Assert.Single(await TripLibrary.TripsAsync(app));
        Assert.Equal("Orlando, March 2027", trip.Name);
        Assert.Equal(TripStatus.Suggested, trip.Status);
    }

    [Fact]
    public async Task Changing_the_home_location_detects_every_trip_again()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 10, 300));
        var browser = await TripLibrary.ScanAndConfirmHomeAsync(app);

        // Home moves to Orlando: the Orlando photos are now home, and no trip is left that passes the rules.
        await browser.SendJsonAsync(HttpMethod.Put, "/api/home", new { latitude = 28.4, longitude = -81.55 });

        var home = await browser.GetJsonAsync("/api/home");
        Assert.Equal("Orlando, Florida, United States of America", home.GetProperty("confirmed").GetProperty("name").GetString());
        Assert.DoesNotContain(await TripLibrary.TripsAsync(app), trip => trip.MainPlace == "Orlando");
    }

    [Fact]
    public async Task A_pin_far_from_any_city_in_your_photos_is_named_by_its_position()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        var browser = await app.SignedInBrowserAsync();
        await app.ScanAsync();
        await browser.GetJsonAsync("/api/session");

        await browser.SendJsonAsync(HttpMethod.Put, "/api/home", new { latitude = -33.87, longitude = 151.21 });

        var home = await browser.GetJsonAsync("/api/home");
        Assert.Equal("33.87° S, 151.21° E", home.GetProperty("confirmed").GetProperty("name").GetString());
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(0, 181)]
    [InlineData(-90.5, -200)]
    public async Task A_position_that_isnt_on_the_map_is_refused(double latitude, double longitude)
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        await browser.GetJsonAsync("/api/session");

        var response = await browser.SendJsonAsync(HttpMethod.Put, "/api/home", new { latitude, longitude });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await app.WithDbAsync(db => db.Settings.CountAsync()));
    }

    [Fact]
    public async Task You_can_search_the_cities_in_your_photos()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 10, 300));
        var browser = await app.SignedInBrowserAsync();
        await app.ScanAsync();

        var all = await browser.GetJsonAsync("/api/home/places");
        var found = await browser.GetJsonAsync("/api/home/places?query=vanc");
        var none = await browser.GetJsonAsync("/api/home/places?query=paris");

        Assert.Equal(2, all.GetArrayLength());
        Assert.Equal("Orlando, Florida, United States of America", all[0].GetProperty("name").GetString());
        Assert.Equal("Vancouver, British Columbia, Canada", Assert.Single(found.EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(0, none.GetArrayLength());
    }

    [Fact]
    public async Task A_library_with_no_positions_says_so()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.Camera(new DateTime(2027, 3, 15, 9, 0, 0), 5, 40));
        var browser = await app.SignedInBrowserAsync();
        await app.ScanAsync();

        var home = await browser.GetJsonAsync("/api/home");

        Assert.False(home.GetProperty("hasPositions").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, home.GetProperty("suggestion").ValueKind);
        Assert.Equal(40, home.GetProperty("photoCount").GetInt32());
    }
}

public class DetectionAndDecisionTests
{
    private static readonly DateTime March15 = new(2027, 3, 15, 9, 0, 0);

    private static async Task<TestApp> StartWithOrlandoAsync()
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15, 10, 300));
        app.Immich.Assets.AddRange(TripLibrary.Camera(March15.AddHours(2), 9, 12));
        await TripLibrary.ScanAndConfirmHomeAsync(app);
        return app;
    }

    /// <summary>Records a decision the way the inbox will, and applies it to the trip row.</summary>
    private static Task<int> DecideAsync(TestApp app, TripStatus? status = null, string? name = null, string? cover = null) =>
        app.WithDbAsync(async db =>
        {
            var trip = await db.Trips.SingleAsync();
            db.Decisions.Add(new Decision
            {
                Fingerprint = trip.Fingerprint,
                Status = status,
                Name = name,
                CoverPhotoId = cover,
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                DecidedAt = app.Time.GetUtcNow().UtcDateTime,
            });
            trip.Status = status ?? trip.Status;
            trip.Name = name ?? trip.Name;
            trip.CoverPhotoId = cover ?? trip.CoverPhotoId;
            return await db.SaveChangesAsync();
        });

    [Fact]
    public async Task A_suggestion_has_a_name_dates_stops_a_route_and_a_confidence_label()
    {
        await using var app = await StartWithOrlandoAsync();

        var trip = Assert.Single(await TripLibrary.TripsAsync(app));

        Assert.Equal("Orlando, March 2027", trip.Name);
        Assert.Equal((new DateOnly(2027, 3, 15), new DateOnly(2027, 3, 24), 10), (trip.StartDate, trip.EndDate, trip.Days));
        Assert.Equal(312, trip.PhotoCount);
        Assert.Equal(312, trip.Photos.Count);
        Assert.Equal(TripConfidence.High, trip.Confidence);
        Assert.Equal("United States of America", trip.Country);
        Assert.InRange(trip.FarthestFromHomeKm, 4000, 4300);
        var stop = Assert.Single(DetectionService.ReadStops(trip.StopsJson));
        Assert.Equal((28.4, -81.55, "Orlando", 300), (Math.Round(stop.Latitude, 2), Math.Round(stop.Longitude, 2), stop.Place, stop.PhotoCount));
        // A photo with no position belongs to the trip, and to no stop.
        Assert.Equal(12, trip.Photos.Count(photo => photo.StopIndex is null));
        Assert.Contains(trip.CoverPhotoId, trip.Photos.Select(photo => photo.PhotoId));
    }

    [Fact]
    public async Task A_rescan_with_no_changes_keeps_the_same_trip_row()
    {
        await using var app = await StartWithOrlandoAsync();
        var before = Assert.Single(await TripLibrary.TripsAsync(app));

        await app.ScanAsync();
        await app.ScanAsync();

        var after = Assert.Single(await TripLibrary.TripsAsync(app));
        Assert.Equal((before.Id, before.Fingerprint, before.PhotoCount), (after.Id, after.Fingerprint, after.PhotoCount));
    }

    [Fact]
    public async Task After_not_a_trip_a_rescan_that_adds_photos_creates_no_suggestion()
    {
        await using var app = await StartWithOrlandoAsync();
        await DecideAsync(app, TripStatus.Ignored);

        // 200 more photos, and 3 more days.
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15.AddDays(10), 3, 200));
        await app.ScanAsync();

        var trip = Assert.Single(await TripLibrary.TripsAsync(app));
        Assert.Equal(TripStatus.Ignored, trip.Status);
        Assert.Equal(512, trip.PhotoCount);
        Assert.Equal(new DateOnly(2027, 3, 27), trip.EndDate);
    }

    [Fact]
    public async Task After_a_rename_and_a_cover_choice_a_rescan_keeps_both()
    {
        await using var app = await StartWithOrlandoAsync();
        var cover = (await TripLibrary.TripsAsync(app))[0].Photos[200].PhotoId;
        await DecideAsync(app, TripStatus.Added, "Disney with the kids", cover);

        app.Immich.Assets.AddRange(TripLibrary.Trip(March15.AddDays(10), 2, 40));
        await app.ScanAsync();

        var trip = Assert.Single(await TripLibrary.TripsAsync(app));
        Assert.Equal(TripStatus.Added, trip.Status);
        Assert.Equal("Disney with the kids", trip.Name);
        Assert.Equal("Orlando, March 2027", trip.SuggestedName);
        Assert.Equal(cover, trip.CoverPhotoId);
        Assert.Equal(352, trip.PhotoCount);
    }

    [Fact]
    public async Task New_photos_that_continue_an_added_trip_extend_it_without_a_new_suggestion()
    {
        await using var app = await StartWithOrlandoAsync();
        await DecideAsync(app, TripStatus.Added);
        var id = (await TripLibrary.TripsAsync(app))[0].Id;

        app.Immich.Assets.AddRange(TripLibrary.Trip(March15.AddDays(10), 4, 80));
        await app.ScanAsync();

        var trip = Assert.Single(await TripLibrary.TripsAsync(app));
        Assert.Equal((id, TripStatus.Added, 14), (trip.Id, trip.Status, trip.Days));
    }

    [Fact]
    public async Task An_early_photo_that_arrives_late_moves_the_start_date_and_the_decision_still_holds()
    {
        await using var app = await StartWithOrlandoAsync();
        await DecideAsync(app, TripStatus.Ignored);
        var before = (await TripLibrary.TripsAsync(app))[0];

        // Photos from the day before the trip started are uploaded later. The fingerprint changes.
        app.Immich.Assets.AddRange(TripLibrary.Trip(March15.AddDays(-1), 1, 15));
        await app.ScanAsync();

        var trip = Assert.Single(await TripLibrary.TripsAsync(app));
        Assert.Equal(new DateOnly(2027, 3, 14), trip.StartDate);
        Assert.NotEqual(before.Fingerprint, trip.Fingerprint);
        Assert.Equal(TripStatus.Ignored, trip.Status);
        Assert.Equal(before.Id, trip.Id);
        // The decision followed the trip, so the next rescan matches it by fingerprint.
        var decision = await app.WithDbAsync(db => db.Decisions.AsNoTracking().SingleAsync());
        Assert.Equal(trip.Fingerprint, decision.Fingerprint);
    }

    [Fact]
    public async Task Detected_data_can_be_deleted_and_rebuilt_and_the_decisions_come_back()
    {
        await using var app = await StartWithOrlandoAsync();
        await DecideAsync(app, TripStatus.Added, "Disney with the kids");

        await app.WithDbAsync(db => db.Trips.ExecuteDeleteAsync());
        Assert.Empty(await TripLibrary.TripsAsync(app));
        await app.WithServicesAsync(services => services.GetRequiredService<DetectionService>().RunAsync(CancellationToken.None));

        var trip = Assert.Single(await TripLibrary.TripsAsync(app));
        Assert.Equal((TripStatus.Added, "Disney with the kids", 312), (trip.Status, trip.Name, trip.Photos.Count));
        Assert.Equal(1, await app.WithDbAsync(db => db.Decisions.CountAsync()));
    }

    [Fact]
    public async Task A_second_trip_becomes_a_new_suggestion_and_leaves_the_first_alone()
    {
        await using var app = await StartWithOrlandoAsync();
        await DecideAsync(app, TripStatus.Added);

        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 7, 3, 9, 0, 0), 4, 60, 49.25, -120.35, "Lakeside", "British Columbia", "Canada"));
        await app.ScanAsync();

        var trips = await TripLibrary.TripsAsync(app);
        Assert.Equal([TripStatus.Added, TripStatus.Suggested], trips.Select(trip => trip.Status));
        Assert.Equal("Lakeside, July 2027", trips[1].Name);
    }

    [Fact]
    public async Task A_trip_whose_photos_left_immich_is_removed_and_its_decision_stays()
    {
        await using var app = await StartWithOrlandoAsync();
        await DecideAsync(app, TripStatus.Added);

        await app.WithDbAsync(db => db.Photos.Where(photo => photo.City == "Orlando").ExecuteDeleteAsync());
        await app.WithServicesAsync(services => services.GetRequiredService<DetectionService>().RunAsync(CancellationToken.None));

        Assert.Empty(await TripLibrary.TripsAsync(app));
        Assert.Equal(0, await app.WithDbAsync(db => db.TripPhotos.CountAsync()));
        Assert.Equal(1, await app.WithDbAsync(db => db.Decisions.CountAsync()));
    }

    [Fact]
    public async Task A_video_taken_away_from_home_counts_toward_the_minimum_photos()
    {
        await using var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        var away = TripLibrary.Trip(March15, 3, 20);
        away[5] = away[5] with { IsVideo = true };
        app.Immich.Assets.AddRange(away);

        await TripLibrary.ScanAndConfirmHomeAsync(app);

        Assert.Equal(20, Assert.Single(await TripLibrary.TripsAsync(app)).PhotoCount);
    }
}

public class ReplayCommandTests
{
    private static async Task<(int ExitCode, string Output)> RunAsync(TestApp app, string? list, params string[] arguments)
    {
        if (list is not null)
        {
            await File.WriteAllTextAsync(Path.Combine(app.Settings.DataDirectory, ReplayCommand.DefaultFileName), list);
        }

        var console = new ScriptedConsole();
        var exitCode = await app.WithServicesAsync(services => ReplayCommand.RunAsync(
            arguments,
            console,
            services.GetRequiredService<FibotrailDbContext>(),
            services.GetRequiredService<HomeLocation>(),
            app.Settings));
        return (exitCode, string.Join('\n', console.Lines));
    }

    private static async Task<TestApp> StartWithTwoTripsAsync()
    {
        var app = await TestApp.StartAsync();
        app.Immich.Assets.AddRange(TripLibrary.AtHome());
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 3, 15, 9, 0, 0), 10, 300));
        app.Immich.Assets.AddRange(TripLibrary.Trip(new DateTime(2027, 7, 3, 9, 0, 0), 4, 60, 49.25, -120.35, "Lakeside", "British Columbia", "Canada"));
        await TripLibrary.ScanAndConfirmHomeAsync(app);
        return app;
    }

    [Fact]
    public async Task The_report_lists_matched_missed_and_extra_trips()
    {
        await using var app = await StartWithTwoTripsAsync();

        var (exitCode, output) = await RunAsync(app, """
            [
              { "name": "Disney", "start": "2027-03-16", "end": "2027-03-24" },
              { "name": "Paris", "start": "2027-09-01", "end": "2027-09-10" }
            ]
            """);

        Assert.Equal(1, exitCode);
        Assert.Contains("Matched: 1", output, StringComparison.Ordinal);
        Assert.Contains("Disney, 2027-03-16 to 2027-03-24 -> Orlando, March 2027, 2027-03-15 to 2027-03-24, 300 photos, High confidence", output, StringComparison.Ordinal);
        Assert.Contains("Missed: 1\n  Paris, 2027-09-01 to 2027-09-10", output, StringComparison.Ordinal);
        Assert.Contains("Extra: 1\n  Lakeside, July 2027", output, StringComparison.Ordinal);
        Assert.Contains("The gate isn't met: 1 of 2 known trips were found.", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_command_exits_0_when_the_gate_is_met_and_changes_nothing()
    {
        await using var app = await StartWithTwoTripsAsync();
        var before = (await TripLibrary.TripsAsync(app)).Select(trip => (trip.Id, trip.Fingerprint, trip.Status)).ToList();

        var (exitCode, output) = await RunAsync(app, """
            [
              { "name": "Disney", "start": "2027-03-15", "end": "2027-03-24" },
              { "name": "Lake", "start": "2027-07-03", "end": "2027-07-06" }
            ]
            """);

        Assert.Equal(0, exitCode);
        Assert.Contains("The gate is met: 2 of 2 known trips were found.", output, StringComparison.Ordinal);
        Assert.Equal(before, (await TripLibrary.TripsAsync(app)).Select(trip => (trip.Id, trip.Fingerprint, trip.Status)));
        Assert.Equal(0, await app.WithDbAsync(db => db.Decisions.CountAsync()));
    }

    [Fact]
    public async Task With_no_list_the_command_shows_what_the_file_looks_like()
    {
        await using var app = await StartWithTwoTripsAsync();

        var (exitCode, output) = await RunAsync(app, null);

        Assert.Equal(1, exitCode);
        Assert.Contains("known-trips.json", output, StringComparison.Ordinal);
        Assert.Contains("\"start\": \"2027-03-15\"", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""[{ "name": "Disney", "start": "March 15", "end": "2027-03-24" }]""")]
    [InlineData("[]")]
    public async Task A_list_that_cant_be_read_is_explained(string list)
    {
        await using var app = await StartWithTwoTripsAsync();

        var (exitCode, output) = await RunAsync(app, list);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain("Matched", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_no_home_location_the_command_says_to_confirm_one()
    {
        await using var app = await TestApp.StartAsync();

        var (exitCode, output) = await RunAsync(app, """[{ "name": "Disney", "start": "2027-03-15", "end": "2027-03-24" }]""");

        Assert.Equal(1, exitCode);
        Assert.Contains("No home location is confirmed.", output, StringComparison.Ordinal);
    }
}
