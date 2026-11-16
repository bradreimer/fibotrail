using static Fibotrail.Detection.Tests.Synthetic;

namespace Fibotrail.Detection.Tests;

/// <summary>The candidate rule for suggesting stops that are on the way. See docs/specs/on-the-way.md.</summary>
public class OnTheWayRuleTests
{
    private static readonly DateTime Start = Time(2027, 3, 15, 9);

    private static StopSummary Stop(string place, int photos, TimeSpan span, int day = 0) =>
        new(place, Start.AddDays(day), Start.AddDays(day) + span, photos);

    private static StopSummary Long(string place, int photos = 40, int day = 0) => Stop(place, photos, TimeSpan.FromDays(2), day);

    [Fact]
    public void A_trip_with_no_stops_has_nothing_to_assess()
    {
        Assert.Empty(OnTheWayRule.Assess([]));
    }

    [Fact]
    public void A_long_busy_stop_is_not_flagged()
    {
        var assessment = Assert.Single(OnTheWayRule.Assess([Long("Orlando")]));

        Assert.False(assessment.IsFlagged);
        Assert.False(assessment.HasFewPhotos);
        Assert.False(assessment.HasShortSpan);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    public void Fewer_than_5_photos_counts_as_few_at_the_edge_of_a_trip(int photos, bool flagged)
    {
        var stops = new[] { Stop("Hounslow", photos, TimeSpan.FromDays(1)), Long("Orlando", day: 2) };

        var first = OnTheWayRule.Assess(stops)[0];

        Assert.Equal(flagged, first.HasFewPhotos);
        Assert.Equal(flagged, first.IsFlagged);
        Assert.False(first.HasShortSpan);
    }

    [Theory]
    [InlineData(179, true)]
    [InlineData(180, false)]
    [InlineData(181, false)]
    public void A_span_under_3_hours_is_short_but_exactly_3_hours_is_not(int minutes, bool flagged)
    {
        var stops = new[] { Long("Vancouver"), Stop("Layover", 40, TimeSpan.FromMinutes(minutes), day: 1), Long("Orlando", day: 2) };

        var middle = OnTheWayRule.Assess(stops)[1];

        Assert.Equal(flagged, middle.HasShortSpan);
        Assert.Equal(flagged, middle.IsFlagged);
        Assert.False(middle.IsFirstOrLast);
    }

    [Fact]
    public void A_stop_with_one_photo_has_a_span_of_0_and_is_short()
    {
        var stops = new[] { Long("Vancouver"), Stop("Window", 1, TimeSpan.Zero, day: 1), Long("Orlando", day: 2) };

        var middle = OnTheWayRule.Assess(stops)[1];

        Assert.Equal(TimeSpan.Zero, middle.Stop.Span);
        Assert.True(middle.HasShortSpan);
    }

    [Fact]
    public void A_stop_in_the_middle_with_few_photos_is_not_flagged_by_the_photo_count()
    {
        var stops = new[] { Long("Vancouver"), Stop("Roadside", 2, TimeSpan.FromDays(1), day: 1), Long("Orlando", day: 2) };

        var middle = OnTheWayRule.Assess(stops)[1];

        Assert.True(middle.HasFewPhotos);
        Assert.False(middle.IsFirstOrLast);
        Assert.False(middle.IsFlagged);
    }

    [Fact]
    public void A_busy_first_or_last_stop_is_not_flagged_by_position_alone()
    {
        var assessments = OnTheWayRule.Assess([Long("Vancouver"), Long("Orlando", day: 2)]);

        Assert.All(assessments, assessment => Assert.True(assessment.IsFirstOrLast));
        Assert.All(assessments, assessment => Assert.False(assessment.IsFlagged));
    }

    [Fact]
    public void A_trip_with_one_stop_has_a_stop_that_is_both_first_and_last()
    {
        var assessment = Assert.Single(OnTheWayRule.Assess([Stop("Orlando", 3, TimeSpan.FromDays(1))]));

        Assert.True(assessment.IsFirstOrLast);
        Assert.Equal(1, assessment.Position);
        Assert.True(assessment.IsFlagged);
        Assert.True(assessment.IsEdgeStopWithFewPhotos);
    }

    [Fact]
    public void A_trip_with_two_stops_has_a_first_and_a_last_and_no_middle()
    {
        var assessments = OnTheWayRule.Assess([Stop("Hounslow", 3, TimeSpan.FromDays(1)), Stop("Orlando", 3, TimeSpan.FromDays(1), day: 2)]);

        Assert.Equal([1, 2], assessments.Select(assessment => assessment.Position));
        Assert.All(assessments, assessment => Assert.True(assessment.IsFirstOrLast));
        Assert.All(assessments, assessment => Assert.True(assessment.IsFlagged));
    }

    [Fact]
    public void A_stop_can_hold_both_conditions()
    {
        var stops = new[] { Stop("Hounslow", 2, TimeSpan.FromHours(1)), Long("Orlando", day: 2) };

        var first = OnTheWayRule.Assess(stops)[0];

        Assert.True(first.IsEdgeStopWithFewPhotos);
        Assert.True(first.HasShortSpan);
        Assert.True(first.IsFlagged);
    }

    [Fact]
    public void The_results_follow_the_order_of_the_stops()
    {
        var stops = new[] { Long("A"), Long("B", day: 1), Long("C", day: 2) };

        var assessments = OnTheWayRule.Assess(stops);

        Assert.Equal(["A", "B", "C"], assessments.Select(assessment => assessment.Stop.Place));
        Assert.Equal([true, false, true], assessments.Select(assessment => assessment.IsFirstOrLast));
    }

    [Fact]
    public void The_thresholds_can_be_changed()
    {
        var stops = new[] { Stop("Hounslow", 8, TimeSpan.FromHours(5)), Long("Orlando", day: 2) };
        var strict = new OnTheWaySettings { FewPhotos = 10, ShortSpan = TimeSpan.FromHours(6) };

        Assert.False(OnTheWayRule.Assess(stops)[0].IsFlagged);

        var first = OnTheWayRule.Assess(stops, strict)[0];

        Assert.True(first.HasFewPhotos);
        Assert.True(first.HasShortSpan);
    }

    [Fact]
    public void The_defaults_are_the_ones_in_the_spec()
    {
        Assert.Equal(5, OnTheWaySettings.Default.FewPhotos);
        Assert.Equal(TimeSpan.FromHours(3), OnTheWaySettings.Default.ShortSpan);
    }
}
