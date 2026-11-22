namespace Fibotrail.Games.Timeline;

/// <summary>
/// The rule that changes how one day's Timeline plays. The weekday picks it. See
/// docs/specs/timeline-game.md#daily-twists.
/// </summary>
public enum TimelineTwist
{
    /// <summary>Five trips, Right, Close, and Wrong marks, 4 tries.</summary>
    Classic,

    /// <summary>A card that isn't Right says Earlier or Later instead of Close or Wrong.</summary>
    Arrows,

    /// <summary>Five trips in a row by start date, so the dates are close.</summary>
    Neighbors,

    /// <summary>Six trips instead of five.</summary>
    SixCards,

    /// <summary>Five photos from one trip, each from a different day.</summary>
    OneTrip,

    /// <summary>A check says only how many cards are in place, and has 6 tries.</summary>
    CountOnly,
}

/// <summary>What each twist changes: the day it's on, its name and rule, cards, and tries.</summary>
public static class TimelineTwists
{
    /// <summary>The most tries any twist allows, for the stats.</summary>
    public const int MostTries = 6;

    /// <summary>The planned twist of a date, by its weekday.</summary>
    public static TimelineTwist For(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Tuesday => TimelineTwist.Arrows,
        DayOfWeek.Wednesday => TimelineTwist.Neighbors,
        DayOfWeek.Friday => TimelineTwist.SixCards,
        DayOfWeek.Saturday => TimelineTwist.OneTrip,
        DayOfWeek.Sunday => TimelineTwist.CountOnly,
        _ => TimelineTwist.Classic,
    };

    public static string Name(TimelineTwist twist) => twist switch
    {
        TimelineTwist.Arrows => "Arrows",
        TimelineTwist.Neighbors => "Neighbors",
        TimelineTwist.SixCards => "Six cards",
        TimelineTwist.OneTrip => "One trip",
        TimelineTwist.CountOnly => "Count only",
        _ => "Classic",
    };

    /// <summary>The one-line rule shown above the cards.</summary>
    public static string Rule(TimelineTwist twist) => twist switch
    {
        TimelineTwist.Arrows => "A photo out of place shows whether it goes earlier or later.",
        TimelineTwist.Neighbors => "Five trips in a row, so the dates are close together.",
        TimelineTwist.SixCards => "Six photos today, and still 4 tries.",
        TimelineTwist.OneTrip => "Five photos from one trip, each from a different day.",
        TimelineTwist.CountOnly => "A check shows only how many photos are in place, not which ones. You get 6 tries.",
        _ => "Green is in place. Yellow is one place away.",
    };

    /// <summary>
    /// The rule when the library can't support the planned twist, so the day plays as Classic.
    /// </summary>
    public static string FallbackRule(TimelineTwist planned) => planned switch
    {
        TimelineTwist.SixCards => $"{Name(planned)} needs 6 trips with photos, so today is Classic. {Rule(TimelineTwist.Classic)}",
        TimelineTwist.OneTrip => $"{Name(planned)} needs a trip with photos on 5 different days, so today is Classic. {Rule(TimelineTwist.Classic)}",
        _ => Rule(TimelineTwist.Classic),
    };

    public static int Cards(TimelineTwist twist) => twist == TimelineTwist.SixCards ? 6 : 5;

    public static int MaxTries(TimelineTwist twist) => twist == TimelineTwist.CountOnly ? MostTries : 4;
}
