using static Fibotrail.Detection.Tests.Synthetic;

namespace Fibotrail.Detection.Tests;

public class GeoTests
{
    [Theory]
    [InlineData(49.25, -123.1, 49.25, -123.1, 0)]
    [InlineData(0, 0, 0, 1, 111.2)]
    [InlineData(0, 0, 1, 0, 111.2)]
    [InlineData(60, 0, 60, 1, 55.6)]
    [InlineData(49.2827, -123.1207, 28.5384, -81.3789, 4184)]
    [InlineData(0, 179.5, 0, -179.5, 111.2)]
    public void Distance_is_measured_on_the_globe(double latitude1, double longitude1, double latitude2, double longitude2, double expectedKm)
    {
        var distance = Geo.DistanceKm(new GeoPoint(latitude1, longitude1), new GeoPoint(latitude2, longitude2));

        Assert.InRange(distance, expectedKm * 0.99, (expectedKm * 1.01) + 0.001);
    }

    [Fact]
    public void Grouping_joins_points_within_the_radius_and_keeps_the_order()
    {
        var a = new GeoPoint(10, 10);
        var points = new[] { a, North(a, 0.5), North(a, 5), North(a, 0.9), North(a, 5.5), North(a, 20) };

        var groups = Geo.Group(points, point => point, 1);

        Assert.Equal(3, groups.Count);
        Assert.Equal([a, North(a, 0.5), North(a, 0.9)], groups[0]);
        Assert.Equal([North(a, 5), North(a, 5.5)], groups[1]);
        Assert.Equal([North(a, 20)], groups[2]);
    }

    [Fact]
    public void Grouping_works_near_the_poles_and_across_longitudes()
    {
        var arctic = new GeoPoint(78.2, 15.6);
        var nearby = new GeoPoint(78.2, 15.63);

        Assert.Single(Geo.Group([arctic, nearby], point => point, 1));
    }
}

public class HomeFinderTests
{
    [Fact]
    public void Home_is_the_area_with_photos_on_the_most_distinct_days()
    {
        // The park has more photos, and home has photos on more days.
        var park = At(Park, 400, Time(2027, 3, 10), Time(2027, 3, 20), "Parkville", "Florida", "United States of America");
        var home = At(Home, 120, Time(2026, 12, 1), Time(2027, 11, 1), "Vancouver", "British Columbia", "Canada");

        var suggestion = HomeFinder.Suggest([.. park, .. home]);

        Assert.NotNull(suggestion);
        Assert.InRange(Geo.DistanceKm(Home, suggestion.Position), 0, 0.01);
        Assert.Equal("Vancouver, British Columbia, Canada", suggestion.Name);
        Assert.Equal(120, suggestion.Days);
    }

    [Fact]
    public void Only_the_last_12_months_of_the_library_count()
    {
        // You moved: years of photos at the old home, and one year at the new one.
        var oldHome = At(Lake, 900, Time(2020, 1, 1), Time(2025, 10, 30), "Lakeside");
        var newHome = At(Home, 100, Time(2025, 11, 5), Time(2026, 11, 1), "Vancouver");

        var suggestion = HomeFinder.Suggest([.. oldHome, .. newHome]);

        Assert.Equal("Vancouver", suggestion!.Name);
    }

    [Fact]
    public void Photos_within_1_km_count_as_one_area()
    {
        var house = At(Home, 30, Time(2027, 1, 1), Time(2027, 1, 30));
        var corner = At(North(Home, 0.6), 30, Time(2027, 2, 1), Time(2027, 3, 2));
        var office = At(North(Home, 8), 40, Time(2027, 1, 1), Time(2027, 2, 9));

        var suggestion = HomeFinder.Suggest([.. house, .. corner, .. office]);

        Assert.Equal(60, suggestion!.Days);
    }

    [Fact]
    public void A_library_with_no_positions_has_no_suggestion()
    {
        Assert.Null(HomeFinder.Suggest(At(null, 50, Time(2027, 1, 1), Time(2027, 6, 1))));
        Assert.Null(HomeFinder.Suggest([]));
    }

    [Fact]
    public void Known_places_are_the_cities_in_the_photos_most_photos_first()
    {
        var photos = At(Home, 30, Time(2027, 1, 1), Time(2027, 1, 30), "Vancouver", "British Columbia", "Canada")
            .Concat(At(Park, 50, Time(2027, 3, 1), Time(2027, 3, 9), "Parkville", "Florida", "United States of America"))
            .Concat(At(Lake, 5, Time(2027, 5, 1), Time(2027, 5, 2)))
            .ToList();

        var places = HomeFinder.KnownPlaces(photos);

        Assert.Equal(["Parkville, Florida, United States of America", "Vancouver, British Columbia, Canada"], places.Select(place => place.Name));
        Assert.Equal(50, places[0].PhotoCount);
        Assert.Equal("Vancouver, British Columbia, Canada", HomeFinder.NearestPlaceName(places, North(Home, 12), 50));
        Assert.Null(HomeFinder.NearestPlaceName(places, Lake, 50));
    }
}

public class ReplayTests
{
    private static DetectedTrip Detected(DateOnly start, DateOnly end) =>
        new("f", "Trip", null, null, start, end, [], 0, [], 0, 0, Confidence.Medium, "cover");

    [Fact]
    public void A_known_trip_is_matched_when_a_detected_trip_starts_and_ends_within_one_day_of_it()
    {
        var known = new[]
        {
            new KnownTrip("Orlando", new(2027, 3, 15), new(2027, 3, 24)),
            new KnownTrip("Lake", new(2027, 7, 3), new(2027, 7, 6)),
            new KnownTrip("Paris", new(2027, 9, 1), new(2027, 9, 10)),
        };
        var detected = new[]
        {
            Detected(new(2027, 3, 16), new(2027, 3, 23)),
            Detected(new(2027, 7, 3), new(2027, 7, 8)),
            Detected(new(2027, 11, 1), new(2027, 11, 4)),
        };

        var result = Replay.Compare(known, detected);

        Assert.Equal("Orlando", Assert.Single(result.Matched).Known.Name);
        Assert.Equal(["Lake", "Paris"], result.Missed.Select(trip => trip.Name));
        Assert.Equal([new DateOnly(2027, 7, 3), new DateOnly(2027, 11, 1)], result.Extra.Select(trip => trip.StartDate));
        Assert.False(result.PassesGate);
    }

    [Theory]
    [InlineData(8, 2, true)]
    [InlineData(7, 3, false)]
    [InlineData(4, 1, true)]
    [InlineData(0, 0, false)]
    public void The_gate_is_8_of_every_10_known_trips(int matched, int missed, bool passes)
    {
        var known = Enumerable.Range(0, matched + missed)
            .Select(index => new KnownTrip($"Trip {index}", new DateOnly(2020 + index, 1, 1), new DateOnly(2020 + index, 1, 5)))
            .ToList();
        var detected = known.Take(matched).Select(trip => Detected(trip.Start, trip.End));

        Assert.Equal(passes, Replay.Compare(known, detected).PassesGate);
    }

    [Fact]
    public void A_detected_trip_matches_one_known_trip_at_most()
    {
        var known = new[]
        {
            new KnownTrip("First", new(2027, 3, 15), new(2027, 3, 20)),
            new KnownTrip("Same dates", new(2027, 3, 15), new(2027, 3, 20)),
        };

        var result = Replay.Compare(known, [Detected(new(2027, 3, 15), new(2027, 3, 20))]);

        Assert.Single(result.Matched);
        Assert.Single(result.Missed);
    }
}
