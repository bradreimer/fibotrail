using static Fibotrail.Detection.Tests.Synthetic;

namespace Fibotrail.Detection.Tests;

/// <summary>Rule 3c: a join you chose makes the trips that touch it one trip.</summary>
public class JoinTests
{
    private static readonly DateRange Whole = new(new DateOnly(2027, 3, 15), new DateOnly(2027, 3, 25));

    /// <summary>Two stays at a lake 200 km from home with 6 days and no photos between them, which is longer than the 72 hour gap limit.</summary>
    private static List<PhotoPoint> TwoStays(int photosEach = 30) =>
    [
        .. At(Lake, photosEach, Time(2027, 3, 15, 9), Time(2027, 3, 17, 18), "Lakeside", "British Columbia", "Canada"),
        .. At(Lake, photosEach, Time(2027, 3, 23, 9), Time(2027, 3, 25, 18), "Lakeside", "British Columbia", "Canada"),
    ];

    [Fact]
    public void Without_a_join_a_gap_of_six_days_makes_two_trips()
    {
        Assert.Equal(2, TripDetector.Detect(TwoStays(), Home).Count);
    }

    [Fact]
    public void A_join_makes_the_two_trips_one_trip_with_every_photo()
    {
        var trip = Assert.Single(TripDetector.Detect(TwoStays(), Home, joins: [Whole]));

        Assert.Equal(new DateOnly(2027, 3, 15), trip.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 25), trip.EndDate);
        Assert.Equal(11, trip.Days);
        Assert.Equal(60, trip.PhotoIds.Count);
    }

    [Fact]
    public void The_minimums_apply_to_the_joined_trip()
    {
        // 12 photos each are no trip alone, because the minimum is 20. Together they are.
        Assert.Empty(TripDetector.Detect(TwoStays(12), Home));

        var trip = Assert.Single(TripDetector.Detect(TwoStays(12), Home, joins: [Whole]));
        Assert.Equal(24, trip.PhotoIds.Count);
    }

    [Fact]
    public void Joining_is_the_same_each_time_and_keeps_the_fingerprint_of_the_first_day()
    {
        var photos = TwoStays();
        var first = Assert.Single(TripDetector.Detect(photos, Home, joins: [Whole]));
        var again = Assert.Single(TripDetector.Detect(photos.AsEnumerable().Reverse(), Home, joins: [Whole]));

        Assert.Equal(first.Fingerprint, again.Fingerprint);
        Assert.Equal(TripDetector.Fingerprint(new DateOnly(2027, 3, 15), "Lakeside"), first.Fingerprint);
    }

    [Fact]
    public void A_range_that_touches_one_trip_changes_nothing()
    {
        var photos = TwoStays();
        var onlyFirst = new DateRange(new DateOnly(2027, 3, 15), new DateOnly(2027, 3, 18));

        Assert.Equal(2, TripDetector.Detect(photos, Home, joins: [onlyFirst]).Count);
    }

    [Fact]
    public void Two_joins_in_a_chain_make_three_trips_one()
    {
        var photos = new List<PhotoPoint>(TwoStays());
        photos.AddRange(At(Lake, 30, Time(2027, 3, 31, 9), Time(2027, 4, 2, 18), "Lakeside", "British Columbia", "Canada"));
        var second = new DateRange(new DateOnly(2027, 3, 23), new DateOnly(2027, 4, 2));

        var trip = Assert.Single(TripDetector.Detect(photos, Home, joins: [Whole, second]));

        Assert.Equal(90, trip.PhotoIds.Count);
        Assert.Equal(new DateOnly(2027, 4, 2), trip.EndDate);
    }

    /// <summary>A day of 30 photos with no position, enough to be a burst day.</summary>
    private static List<PhotoPoint> BurstDays(int firstDay, int lastDay)
    {
        var photos = new List<PhotoPoint>();
        for (var day = firstDay; day <= lastDay; day++)
        {
            photos.AddRange(At(null, 30, Time(2027, 3, day, 9), Time(2027, 3, day, 18)));
        }

        return photos;
    }

    private static List<PhotoPoint> StayThenBurst() =>
    [
        .. At(Lake, 30, Time(2027, 3, 15, 9), Time(2027, 3, 17, 18), "Lakeside", "British Columbia", "Canada"),
        .. BurstDays(18, 20),
    ];

    [Fact]
    public void A_trip_with_a_position_joins_a_trip_from_photos_with_no_position()
    {
        var photos = StayThenBurst();
        Assert.Equal(2, TripDetector.Detect(photos, Home).Count);

        var trip = Assert.Single(TripDetector.Detect(photos, Home, joins: [new DateRange(new DateOnly(2027, 3, 15), new DateOnly(2027, 3, 20))]));

        Assert.Equal(new DateOnly(2027, 3, 15), trip.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 20), trip.EndDate);
        Assert.Equal(120, trip.PhotoIds.Count);
        Assert.Equal(30, trip.AwayPhotoCount);
        Assert.Equal("Lakeside", trip.MainPlace);
        Assert.Equal("Canada", trip.Country);
        Assert.Equal(TripDetector.Fingerprint(new DateOnly(2027, 3, 15), "Lakeside"), trip.Fingerprint);
        Assert.Single(trip.Stops);
    }

    [Fact]
    public void The_joined_trip_has_the_fingerprint_of_the_trip_with_a_position_in_either_order()
    {
        // The burst comes first, so the joined trip starts with it and has no place before the lake is counted.
        var photos = new List<PhotoPoint>(BurstDays(10, 12));
        photos.AddRange(At(Lake, 30, Time(2027, 3, 13, 9), Time(2027, 3, 15, 18), "Lakeside", "British Columbia", "Canada"));

        var trip = Assert.Single(TripDetector.Detect(photos, Home, joins: [new DateRange(new DateOnly(2027, 3, 10), new DateOnly(2027, 3, 15))]));

        Assert.Equal(new DateOnly(2027, 3, 10), trip.StartDate);
        Assert.Equal("Lakeside", trip.MainPlace);
        Assert.Equal(TripDetector.Fingerprint(new DateOnly(2027, 3, 10), "Lakeside"), trip.Fingerprint);
    }

    [Fact]
    public void Two_trips_from_photos_with_no_position_join_into_one_with_no_stops()
    {
        var photos = new List<PhotoPoint>([.. BurstDays(10, 12), .. BurstDays(14, 16)]);
        Assert.Equal(2, TripDetector.Detect(photos, Home).Count);

        var trip = Assert.Single(TripDetector.Detect(photos, Home, joins: [new DateRange(new DateOnly(2027, 3, 10), new DateOnly(2027, 3, 16))]));

        Assert.Equal(180, trip.PhotoIds.Count);
        Assert.Equal(0, trip.AwayPhotoCount);
        Assert.Null(trip.MainPlace);
        Assert.Empty(trip.Stops);
        Assert.Equal(Confidence.Low, trip.Confidence);
    }

    [Fact]
    public void A_short_far_visit_joins_a_trip_from_photos_with_no_position()
    {
        var photos = new List<PhotoPoint>(At(Stockholm, 3, Time(2027, 3, 12, 9), Time(2027, 3, 12, 18), "Stockholm", null, "Sweden"));
        photos.AddRange(BurstDays(13, 15));
        Assert.Equal(2, TripDetector.Detect(photos, Home).Count);

        var trip = Assert.Single(TripDetector.Detect(photos, Home, joins: [new DateRange(new DateOnly(2027, 3, 12), new DateOnly(2027, 3, 15))]));

        Assert.Equal("Stockholm", trip.MainPlace);
        Assert.Equal(93, trip.PhotoIds.Count);
    }

    [Fact]
    public void Joining_trips_from_different_rules_is_the_same_whatever_the_input_order()
    {
        var photos = StayThenBurst();
        var range = new DateRange(new DateOnly(2027, 3, 15), new DateOnly(2027, 3, 20));

        var first = Assert.Single(TripDetector.Detect(photos, Home, joins: [range]));
        var again = Assert.Single(TripDetector.Detect(photos.AsEnumerable().Reverse(), Home, joins: [range]));

        Assert.Equal(first.Fingerprint, again.Fingerprint);
        Assert.Equal(first.DistanceKm, again.DistanceKm);
        Assert.Equal(first.Confidence, again.Confidence);
        Assert.Equal(first.PhotoIds, again.PhotoIds);
        Assert.Equal(first.CoverPhotoId, again.CoverPhotoId);
    }

    [Fact]
    public void A_range_that_touches_one_trip_from_photos_with_no_position_changes_nothing()
    {
        var onlyBurst = new DateRange(new DateOnly(2027, 3, 19), new DateOnly(2027, 3, 25));

        Assert.Equal(2, TripDetector.Detect(StayThenBurst(), Home, joins: [onlyBurst]).Count);
    }
}
