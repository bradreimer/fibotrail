using static Fibotrail.Detection.Tests.Synthetic;

namespace Fibotrail.Detection.Tests;

public class TripDetectorTests
{
    private static List<PhotoPoint> WeekendAtTheLake(int count = 25) =>
        At(Lake, count, Time(2027, 3, 12, 9), Time(2027, 3, 14, 18), "Lakeside", "British Columbia", "Canada");

    [Fact]
    public void Twenty_five_photos_over_3_days_200_km_from_home_become_one_suggestion()
    {
        var trip = Assert.Single(TripDetector.Detect(WeekendAtTheLake(), Home));

        Assert.Equal(new DateOnly(2027, 3, 12), trip.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 14), trip.EndDate);
        Assert.Equal(3, trip.Days);
        Assert.Equal(25, trip.PhotoIds.Count);
        Assert.InRange(trip.FarthestFromHomeKm, 195, 205);
    }

    [Fact]
    public void Nineteen_photos_over_3_days_become_no_suggestion()
    {
        Assert.Empty(TripDetector.Detect(WeekendAtTheLake(19), Home));
    }

    [Fact]
    public void Twenty_photos_are_enough()
    {
        Assert.Single(TripDetector.Detect(WeekendAtTheLake(20), Home));
    }

    [Fact]
    public void An_outing_on_one_calendar_day_is_not_a_trip()
    {
        var photos = At(Lake, 60, Time(2027, 3, 12, 8), Time(2027, 3, 12, 22));

        Assert.Empty(TripDetector.Detect(photos, Home));
    }

    [Fact]
    public void Photos_inside_the_home_radius_are_never_a_trip()
    {
        // 45 km from home is still home with the 50 km default.
        var photos = At(North(Home, 45), 80, Time(2027, 3, 12), Time(2027, 3, 20));

        Assert.Empty(TripDetector.Detect(photos, Home));
    }

    [Fact]
    public void The_home_radius_is_the_line_between_home_and_away()
    {
        var justInside = At(North(Home, 49), 30, Time(2027, 3, 12), Time(2027, 3, 14));
        var justOutside = At(North(Home, 51), 30, Time(2027, 4, 12), Time(2027, 4, 14));

        var trip = Assert.Single(TripDetector.Detect([.. justInside, .. justOutside], Home));

        Assert.Equal(new DateOnly(2027, 4, 12), trip.StartDate);
    }

    [Fact]
    public void Two_groups_of_away_photos_73_hours_apart_become_two_trips()
    {
        var first = At(Lake, 25, Time(2027, 3, 1, 9), Time(2027, 3, 3, 12));
        var second = At(Lake, 25, Time(2027, 3, 6, 13), Time(2027, 3, 8, 12));

        var trips = TripDetector.Detect([.. first, .. second], Home);

        Assert.Equal(2, trips.Count);
        Assert.Equal(new DateOnly(2027, 3, 3), trips[0].EndDate);
        Assert.Equal(new DateOnly(2027, 3, 6), trips[1].StartDate);
    }

    [Fact]
    public void Two_groups_71_hours_apart_stay_one_trip()
    {
        var first = At(Lake, 25, Time(2027, 3, 1, 9), Time(2027, 3, 3, 12));
        var second = At(Lake, 25, Time(2027, 3, 6, 11), Time(2027, 3, 8, 12));

        var trip = Assert.Single(TripDetector.Detect([.. first, .. second], Home));

        Assert.Equal(8, trip.Days);
    }

    [Fact]
    public void A_photo_at_home_between_two_groups_ends_the_trip()
    {
        var first = At(Lake, 25, Time(2027, 3, 1, 9), Time(2027, 3, 3, 12));
        var atHome = At(Home, 1, Time(2027, 3, 4), Time(2027, 3, 4));
        var second = At(Lake, 25, Time(2027, 3, 5, 9), Time(2027, 3, 7, 12));

        Assert.Equal(2, TripDetector.Detect([.. first, .. atHome, .. second], Home).Count);
    }

    [Fact]
    public void A_photo_with_no_position_taken_during_a_trip_belongs_to_that_trip()
    {
        var away = WeekendAtTheLake();
        var camera = At(null, 40, Time(2027, 3, 12, 10), Time(2027, 3, 14, 17));
        var before = At(null, 5, Time(2027, 3, 11, 10), Time(2027, 3, 12, 8));
        var after = At(null, 5, Time(2027, 3, 14, 19), Time(2027, 3, 16));

        var trip = Assert.Single(TripDetector.Detect([.. away, .. camera, .. before, .. after], Home));

        Assert.Equal(65, trip.PhotoIds.Count);
        Assert.Equal(25, trip.AwayPhotoCount);
        Assert.All(camera, photo => Assert.Contains(photo.Id, trip.PhotoIds));
        Assert.All(before.Concat(after), photo => Assert.DoesNotContain(photo.Id, trip.PhotoIds));
    }

    [Fact]
    public void Photos_with_no_position_dont_count_toward_the_minimum()
    {
        var away = WeekendAtTheLake(19);
        var camera = At(null, 100, Time(2027, 3, 12, 10), Time(2027, 3, 14, 17));

        Assert.Empty(TripDetector.Detect([.. away, .. camera], Home));
    }

    [Fact]
    public void A_library_with_no_positions_has_no_trips()
    {
        Assert.Empty(TripDetector.Detect(At(null, 500, Time(2027, 1, 1), Time(2027, 12, 31)), Home));
    }

    [Fact]
    public void A_photo_taken_at_23_30_local_time_abroad_is_counted_on_that_local_day()
    {
        // The local time is all that detection sees. In UTC, this photo is on March 13.
        var photos = At(Park, 24, Time(2027, 3, 12, 23, 30), Time(2027, 3, 14, 10));
        photos.Add(new PhotoPoint("late-night", Time(2027, 3, 12, 23, 30), Park.Latitude, Park.Longitude));

        var trip = Assert.Single(TripDetector.Detect(photos, Home));

        Assert.Equal(new DateOnly(2027, 3, 12), trip.StartDate);
        Assert.Equal(3, trip.Days);
    }

    [Fact]
    public void Running_detection_twice_gives_the_same_trips_with_the_same_fingerprints()
    {
        var photos = WeekendAtTheLake().Concat(At(Park, 60, Time(2027, 6, 1), Time(2027, 6, 9), "Parkville")).ToList();
        var shuffled = photos.OrderBy(photo => photo.Id.GetHashCode(StringComparison.Ordinal)).ToList();

        var first = TripDetector.Detect(photos, Home);
        var second = TripDetector.Detect(shuffled, Home);

        Assert.Equal(2, first.Count);
        Assert.Equal(first.Select(trip => trip.Fingerprint), second.Select(trip => trip.Fingerprint));
        Assert.Equal(first.Select(trip => trip.PhotoIds), second.Select(trip => trip.PhotoIds));
        Assert.Equal(first.Select(trip => trip.Name), second.Select(trip => trip.Name));
        Assert.Equal(first.SelectMany(trip => trip.Stops).Select(stop => stop.Center), second.SelectMany(trip => trip.Stops).Select(stop => stop.Center));
    }

    [Fact]
    public void A_trip_that_gains_photos_and_days_keeps_its_fingerprint()
    {
        var before = WeekendAtTheLake();
        var more = At(Lake, 30, Time(2027, 3, 15, 9), Time(2027, 3, 17, 18), "Lakeside");

        var first = Assert.Single(TripDetector.Detect(before, Home));
        var grown = Assert.Single(TripDetector.Detect([.. before, .. more], Home));

        Assert.Equal(first.Fingerprint, grown.Fingerprint);
        Assert.Equal(6, grown.Days);
    }

    [Fact]
    public void The_fingerprint_follows_the_start_date_and_the_main_place()
    {
        var start = new DateOnly(2027, 3, 12);

        Assert.Equal(TripDetector.Fingerprint(start, "Orlando"), TripDetector.Fingerprint(start, " orlando "));
        Assert.NotEqual(TripDetector.Fingerprint(start, "Orlando"), TripDetector.Fingerprint(start.AddDays(1), "Orlando"));
        Assert.NotEqual(TripDetector.Fingerprint(start, "Orlando"), TripDetector.Fingerprint(start, "Tampa"));
        Assert.Equal(32, TripDetector.Fingerprint(start, null).Length);
    }

    [Fact]
    public void Photos_within_the_stop_radius_form_one_stop_and_the_stops_are_ordered_by_time()
    {
        var hotel = At(Park, 10, Time(2027, 3, 12, 9), Time(2027, 3, 12, 10), "Parkville");
        var nearHotel = At(North(Park, 0.8), 5, Time(2027, 3, 12, 11), Time(2027, 3, 12, 12), "Parkville");
        var castle = At(North(Park, 6), 12, Time(2027, 3, 13, 9), Time(2027, 3, 13, 15), "Castle Town");
        var backAtHotel = At(Park, 3, Time(2027, 3, 14, 9), Time(2027, 3, 14, 10), "Parkville");

        var trip = Assert.Single(TripDetector.Detect([.. castle, .. backAtHotel, .. hotel, .. nearHotel], Home));

        Assert.Equal(2, trip.Stops.Count);
        Assert.Equal(18, trip.Stops[0].PhotoIds.Count);
        Assert.Equal("Parkville", trip.Stops[0].Place);
        Assert.Equal(Time(2027, 3, 12, 9), trip.Stops[0].FirstTime);
        Assert.Equal(Time(2027, 3, 14, 10), trip.Stops[0].LastTime);
        Assert.Equal(12, trip.Stops[1].PhotoIds.Count);
        Assert.Equal("Castle Town", trip.Stops[1].Place);
    }

    [Fact]
    public void Distance_is_the_sum_of_the_lines_between_consecutive_stops()
    {
        var a = At(Park, 10, Time(2027, 3, 12, 9), Time(2027, 3, 12, 10));
        var b = At(North(Park, 30), 10, Time(2027, 3, 13, 9), Time(2027, 3, 13, 10));
        var c = At(North(Park, 10), 10, Time(2027, 3, 14, 9), Time(2027, 3, 14, 10));

        var trip = Assert.Single(TripDetector.Detect([.. a, .. b, .. c], Home));

        // 30 km north, then 20 km back south.
        Assert.Equal(3, trip.Stops.Count);
        Assert.InRange(trip.DistanceKm, 49.5, 50.5);
        Assert.Equal(Geo.DistanceKm(Home, North(Park, 0)), trip.FarthestFromHomeKm, 0);
    }

    [Fact]
    public void A_trip_with_one_stop_has_no_distance()
    {
        Assert.Equal(0, Assert.Single(TripDetector.Detect(WeekendAtTheLake(), Home)).DistanceKm);
    }

    [Fact]
    public void The_name_is_the_city_with_the_most_days_then_the_month_and_year()
    {
        var orlando = At(Park, 30, Time(2027, 3, 15), Time(2027, 3, 20), "Orlando", "Florida", "United States of America");
        var kissimmee = At(North(Park, -15), 10, Time(2027, 3, 21), Time(2027, 3, 24), "Kissimmee", "Florida", "United States of America");

        var trip = Assert.Single(TripDetector.Detect([.. orlando, .. kissimmee], Home));

        Assert.Equal("Orlando, March 2027", trip.Name);
        Assert.Equal("Orlando", trip.MainPlace);
        Assert.Equal("United States of America", trip.Country);
    }

    [Fact]
    public void A_city_with_more_days_beats_a_city_with_one_busy_day()
    {
        var orlando = At(Park, 12, Time(2027, 3, 15), Time(2027, 3, 17), "Orlando", "Florida", "United States of America");
        var kissimmee = At(North(Park, -15), 300, Time(2027, 3, 18, 9), Time(2027, 3, 18, 20), "Kissimmee", "Florida", "United States of America");

        var trip = Assert.Single(TripDetector.Detect([.. orlando, .. kissimmee], Home));

        Assert.Equal("Orlando", trip.MainPlace);
        Assert.Equal("Orlando, March 2027", trip.Name);
    }

    [Fact]
    public void A_tie_in_days_goes_to_the_city_with_more_photos()
    {
        var orlando = At(Park, 10, Time(2027, 3, 15, 9), Time(2027, 3, 16, 18), "Orlando", "Florida", "United States of America");
        var kissimmee = At(North(Park, -15), 20, Time(2027, 3, 15, 10), Time(2027, 3, 16, 19), "Kissimmee", "Florida", "United States of America");

        Assert.Equal("Kissimmee", Assert.Single(TripDetector.Detect([.. orlando, .. kissimmee], Home)).MainPlace);
    }

    [Fact]
    public void A_day_with_photos_in_two_cities_counts_for_both()
    {
        var orlando = At(Park, 10, Time(2027, 3, 15, 9), Time(2027, 3, 16, 12), "Orlando", "Florida", "United States of America");
        var kissimmee = At(North(Park, -15), 10, Time(2027, 3, 16, 13), Time(2027, 3, 17, 18), "Kissimmee", "Florida", "United States of America");

        var ranked = TripDetector.RankPlaces([.. orlando, .. kissimmee], photo => photo.LocalDay, photo => photo.City);

        // Both have 2 days and 10 photos, so the earlier first photo decides.
        Assert.Equal(["Orlando", "Kissimmee"], ranked);
    }

    [Fact]
    public void With_no_city_the_state_is_chosen_by_days_too()
    {
        var georgia = At(Park, 12, Time(2027, 3, 15), Time(2027, 3, 17), null, "Georgia", "United States of America");
        var florida = At(North(Park, -100), 300, Time(2027, 3, 18, 9), Time(2027, 3, 18, 20), null, "Florida", "United States of America");

        Assert.Equal("Georgia, March 2027", Assert.Single(TripDetector.Detect([.. georgia, .. florida], Home)).Name);
    }

    [Fact]
    public void Ranking_skips_photos_with_no_place_and_returns_nothing_for_no_places()
    {
        var photos = At(Park, 5, Time(2027, 3, 15), Time(2027, 3, 16));

        Assert.Empty(TripDetector.RankPlaces(photos, photo => photo.LocalDay, photo => photo.City));
    }

    [Theory]
    [InlineData(null, "Florida", "United States of America", "Florida, March 2027")]
    [InlineData(null, null, "United States of America", "United States of America, March 2027")]
    [InlineData(null, null, null, "Trip, March 2027")]
    public void With_no_city_the_name_uses_the_state_then_the_country(string? city, string? state, string? country, string expected)
    {
        var photos = At(Park, 30, Time(2027, 3, 15), Time(2027, 3, 20), city, state, country);

        Assert.Equal(expected, Assert.Single(TripDetector.Detect(photos, Home)).Name);
    }

    [Theory]
    [InlineData(60, 60, 4, 200, Confidence.High)]
    [InlineData(50, 50, 3, 150, Confidence.High)]
    [InlineData(49, 49, 3, 200, Confidence.Medium)]
    [InlineData(60, 60, 2, 200, Confidence.Medium)]
    [InlineData(60, 60, 4, 149, Confidence.Medium)]
    [InlineData(200, 25, 2, 60, Confidence.Medium)]
    [InlineData(200, 24, 5, 3000, Confidence.Low)]
    [InlineData(20, 20, 2, 60, Confidence.Low)]
    public void Confidence_follows_photos_days_and_distance(int photos, int awayPhotos, int days, double farthestKm, Confidence expected)
    {
        Assert.Equal(expected, TripDetector.Score(photos, awayPhotos, days, farthestKm, DetectionSettings.Default));
    }

    [Fact]
    public void The_cover_is_an_image_at_the_busiest_stop_and_not_a_video()
    {
        var small = At(North(Park, 5), 5, Time(2027, 3, 12, 9), Time(2027, 3, 12, 10));
        var busy = At(Park, 25, Time(2027, 3, 13, 9), Time(2027, 3, 14, 10));
        busy[0] = busy[0] with { IsVideo = true };

        var trip = Assert.Single(TripDetector.Detect([.. small, .. busy], Home));

        Assert.Equal(busy[1].Id, trip.CoverPhotoId);
    }

    [Fact]
    public void The_settings_change_the_rules()
    {
        var photos = At(North(Home, 30), 10, Time(2027, 3, 12, 9), Time(2027, 3, 12, 20));
        var loose = new DetectionSettings { HomeRadiusKm = 20, MinimumPhotos = 5, MinimumDays = 1 };

        Assert.Empty(TripDetector.Detect(photos, Home));
        Assert.Single(TripDetector.Detect(photos, Home, loose));
    }

    [Fact]
    public void The_defaults_are_the_ones_in_the_spec()
    {
        var defaults = DetectionSettings.Default;

        Assert.Equal(50, defaults.HomeRadiusKm);
        Assert.Equal(TimeSpan.FromHours(72), defaults.GapLimit);
        Assert.Equal(20, defaults.MinimumPhotos);
        Assert.Equal(2, defaults.MinimumDays);
        Assert.Equal(1, defaults.StopRadiusKm);
    }

    [Fact]
    public void Seventy_thousand_photos_are_detected_in_a_few_seconds()
    {
        // The size of the maintainer's library: 85 percent of photos have no position.
        var photos = new List<PhotoPoint>();
        for (var year = 2012; year < 2027; year++)
        {
            photos.AddRange(At(null, 4000, Time(year, 1, 1), Time(year, 12, 31)));
            // Home photos stop while you're away. A photo at home would end the trip.
            photos.AddRange(At(Home, 100, Time(year, 1, 1), Time(year, 3, 9)));
            photos.AddRange(At(Home, 200, Time(year, 3, 21), Time(year, 7, 2)));
            photos.AddRange(At(Home, 200, Time(year, 7, 7), Time(year, 12, 30)));
            photos.AddRange(At(Park, 150, Time(year, 3, 10, 9), Time(year, 3, 20, 18), "Parkville"));
            photos.AddRange(At(Lake, 80, Time(year, 7, 3, 9), Time(year, 7, 6, 18), "Lakeside"));
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var trips = TripDetector.Detect(photos, Home);
        watch.Stop();

        Assert.Equal(30, trips.Count);
        Assert.True(photos.Count > 70000);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"Detection took {watch.Elapsed.TotalSeconds:0.0} seconds.");
    }
}
