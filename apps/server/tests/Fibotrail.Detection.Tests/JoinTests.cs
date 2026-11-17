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
}
