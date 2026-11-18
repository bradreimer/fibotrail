using static Fibotrail.Detection.Tests.Synthetic;

namespace Fibotrail.Detection.Tests;

/// <summary>A photo you pinned to a trip counts as taken on a day of that trip.</summary>
public class PinnedPhotosTests
{
    private static readonly DateOnly Start = new(2005, 7, 31);
    private static readonly DateOnly End = new(2005, 8, 6);

    [Theory]
    [InlineData(2005, 7, 1, 2005, 7, 31)]
    [InlineData(2005, 8, 3, 2005, 8, 3)]
    [InlineData(2005, 9, 9, 2005, 8, 6)]
    public void A_photo_goes_to_the_day_of_the_trip_nearest_its_own(int year, int month, int day, int toYear, int toMonth, int toDay)
    {
        Assert.Equal(new DateOnly(toYear, toMonth, toDay), PinnedPhotos.DayIn(new DateOnly(year, month, day), Start, End));
    }

    [Fact]
    public void A_trip_that_ends_before_it_starts_is_refused()
    {
        Assert.Throws<ArgumentException>(() => PinnedPhotos.DayIn(Start, End, Start));
    }

    [Fact]
    public void A_pinned_photo_moves_to_its_day_and_keeps_its_time_of_day_and_position()
    {
        var photo = new PhotoPoint("a", new DateTime(2006, 1, 2, 15, 30, 0), 10, 20, "Town");
        var other = new PhotoPoint("b", new DateTime(2006, 1, 2, 9, 0, 0));

        var result = PinnedPhotos.Apply([photo, other], new Dictionary<string, DateOnly> { ["a"] = new(2005, 8, 6) });

        Assert.Equal(photo with { LocalTime = new DateTime(2005, 8, 6, 15, 30, 0) }, result[0]);
        Assert.Same(other, result[1]);
    }

    [Fact]
    public void With_no_pins_the_photos_are_returned_as_they_are()
    {
        var photos = At(null, 3, Time(2005, 7, 31, 19), Time(2005, 8, 1, 0));

        Assert.Same(photos, PinnedPhotos.Apply(photos, new Dictionary<string, DateOnly>()));
    }

    [Fact]
    public void A_pinned_photo_with_no_position_joins_a_manual_trip_it_was_taken_outside_of()
    {
        var photos = At(null, 40, Time(2005, 7, 31, 19), Time(2005, 8, 6, 0));
        var stray = At(null, 1, Time(2005, 9, 20, 12), Time(2005, 9, 20, 12))[0];
        var spec = new ManualTripSpec(Start, End, "Bay Area", null, null, null);
        Assert.Equal(40, ManualTrips.Build([.. photos, stray], spec, Home)!.PhotoIds.Count);

        var pinned = PinnedPhotos.Apply([.. photos, stray], new Dictionary<string, DateOnly> { [stray.Id] = End });

        Assert.Equal(41, ManualTrips.Build(pinned, spec, Home)!.PhotoIds.Count);
    }
}
