using Fibotrail.Games.Timeline;

namespace Fibotrail.Games.Tests;

/// <summary>The Timeline game. See docs/specs/timeline-game.md.</summary>
public class TimelinePuzzleTests
{
    private static readonly DateOnly Day = new(2026, 11, 20);

    private static GameTrip Trip(int id, int year, int photos = 3) => new(
        id,
        $"Trip {id}",
        new DateOnly(year, 6, 1),
        new DateOnly(year, 6, 5),
        "Somewhere",
        [.. Enumerable.Range(0, photos).Select(index => new GamePhoto($"t{id}-p{index}", new DateTime(year, 6, 1 + index, 10, 0, 0)))]);

    private static GameLibrary Library(int trips) => new([.. Enumerable.Range(1, trips).Select(id => Trip(id, 2000 + id))]);

    private static TimelinePuzzle Puzzle(int trips = 8, TimelineTwist twist = TimelineTwist.Classic) => TimelinePuzzle.Create(Library(trips), Day, twist)!;

    private static IReadOnlyList<string> AnswerIds(TimelinePuzzle puzzle) => [.. puzzle.Answer.Select(card => card.PhotoId)];

    [Fact]
    public void Fewer_than_five_trips_with_photos_has_no_puzzle()
    {
        var library = new GameLibrary([Trip(1, 2001), Trip(2, 2002), Trip(3, 2003), Trip(4, 2004), Trip(5, 2005, photos: 0)]);

        Assert.Null(TimelinePuzzle.Create(library, Day));
        Assert.Equal(1, TimelinePuzzle.TripsNeeded(library));
        Assert.Equal(5, TimelinePuzzle.TripsNeeded(GameLibrary.Empty));
    }

    [Fact]
    public void A_puzzle_has_five_cards_from_five_trips_in_date_order()
    {
        var puzzle = Puzzle();

        Assert.Equal(5, puzzle.Answer.Count);
        Assert.Equal(5, puzzle.Answer.Select(card => card.TripId).Distinct().Count());
        Assert.All(puzzle.Answer, card => Assert.Equal($"Trip {card.TripId}", card.TripName));
        Assert.Equal(puzzle.Answer.OrderBy(card => card.Taken), puzzle.Answer);
        Assert.Equal(puzzle.Answer.ToHashSet(), puzzle.Shown.ToHashSet());
    }

    [Fact]
    public void The_shown_order_is_never_the_answer()
    {
        for (var offset = 0; offset < 200; offset++)
        {
            var puzzle = TimelinePuzzle.Create(Library(6), Day.AddDays(offset))!;
            Assert.NotEqual(puzzle.Answer, puzzle.Shown);
        }
    }

    [Fact]
    public void The_same_date_gives_the_same_puzzle_whatever_the_trip_order()
    {
        var library = Library(10);
        var reversed = new GameLibrary([.. library.Trips.Reverse()]);

        var first = TimelinePuzzle.Create(library, Day)!;
        var second = TimelinePuzzle.Create(reversed, Day)!;

        Assert.Equal(first.Shown, second.Shown);
        Assert.Equal(first.Answer, second.Answer);
    }

    [Fact]
    public void Another_date_gives_another_puzzle()
    {
        var library = Library(10);
        var puzzles = Enumerable.Range(0, 7).Select(offset => string.Join(',', TimelinePuzzle.Create(library, Day.AddDays(offset))!.Shown.Select(card => card.PhotoId)));

        Assert.True(puzzles.Distinct().Count() > 1);
    }

    [Fact]
    public void The_number_counts_from_the_first_day()
    {
        Assert.Equal(2, Puzzle().Number);
    }

    [Fact]
    public void Grading_marks_right_close_and_wrong()
    {
        var puzzle = Puzzle();
        var ids = AnswerIds(puzzle);

        // Swap the first two (each one place off) and the last with the middle (two places off).
        var marks = puzzle.Grade([ids[1], ids[0], ids[4], ids[3], ids[2]]);

        Assert.Equal([Mark.Close, Mark.Close, Mark.Wrong, Mark.Right, Mark.Wrong], marks);
        Assert.All(puzzle.Grade(ids)!, mark => Assert.Equal(Mark.Right, mark));
    }

    [Fact]
    public void A_try_must_hold_every_card_once()
    {
        var puzzle = Puzzle();
        var ids = AnswerIds(puzzle);

        Assert.Null(puzzle.Grade([.. ids.Take(4)]));
        Assert.Null(puzzle.Grade([ids[0], ids[0], ids[2], ids[3], ids[4]]));
        Assert.Null(puzzle.Grade([ids[0], ids[1], ids[2], ids[3], "not-a-card"]));
    }

    [Fact]
    public void Solved_on_the_second_try()
    {
        var puzzle = Puzzle();
        var ids = AnswerIds(puzzle);

        var outcome = puzzle.Score([[ids[1], ids[0], ids[2], ids[3], ids[4]], ids])!;

        Assert.True(outcome.Solved);
        Assert.Equal(2, outcome.Rows.Count);
    }

    [Fact]
    public void Four_wrong_tries_is_not_solved()
    {
        var puzzle = Puzzle();
        IReadOnlyList<string> wrong = [.. AnswerIds(puzzle).Reverse()];

        var outcome = puzzle.Score([wrong, wrong, wrong, wrong])!;

        Assert.False(outcome.Solved);
        Assert.Equal(4, outcome.Rows.Count);
    }

    [Fact]
    public void Too_many_tries_no_tries_or_a_try_after_solving_is_refused()
    {
        var puzzle = Puzzle();
        var ids = AnswerIds(puzzle);
        IReadOnlyList<string> wrong = [.. ids.Reverse()];

        Assert.Null(puzzle.Score([]));
        Assert.Null(puzzle.Score([wrong, wrong, wrong, wrong, ids]));
        Assert.Null(puzzle.Score([ids, ids]));
    }

    // Daily twists. See docs/specs/timeline-game.md#daily-twists.

    /// <summary>A trip with one photo at noon on each of <paramref name="days"/> days.</summary>
    private static GameTrip LongTrip(int id, int year, int days) => new(
        id,
        $"Trip {id}",
        new DateOnly(year, 6, 1),
        new DateOnly(year, 6, days),
        "Somewhere",
        [.. Enumerable.Range(0, days).Select(day => new GamePhoto($"t{id}-d{day}", new DateTime(year, 6, 1 + day, 12, 0, 0)))]);

    [Theory]
    [InlineData(2026, 11, 23, TimelineTwist.Classic)]
    [InlineData(2026, 11, 24, TimelineTwist.Arrows)]
    [InlineData(2026, 11, 25, TimelineTwist.Neighbors)]
    [InlineData(2026, 11, 26, TimelineTwist.Classic)]
    [InlineData(2026, 11, 27, TimelineTwist.SixCards)]
    [InlineData(2026, 11, 28, TimelineTwist.OneTrip)]
    [InlineData(2026, 11, 29, TimelineTwist.CountOnly)]
    public void The_weekday_picks_the_twist(int year, int month, int day, TimelineTwist twist)
    {
        Assert.Equal(twist, TimelineTwists.For(new DateOnly(year, month, day)));
    }

    [Fact]
    public void Only_count_only_has_six_tries_and_only_six_cards_has_six_cards()
    {
        Assert.All(Enum.GetValues<TimelineTwist>(), twist =>
        {
            Assert.Equal(twist == TimelineTwist.CountOnly ? 6 : 4, TimelineTwists.MaxTries(twist));
            Assert.Equal(twist == TimelineTwist.SixCards ? 6 : 5, TimelineTwists.Cards(twist));
            Assert.False(string.IsNullOrWhiteSpace(TimelineTwists.Rule(twist)));
        });
        Assert.Equal(TimelineTwists.MostTries, Enum.GetValues<TimelineTwist>().Max(TimelineTwists.MaxTries));
    }

    [Fact]
    public void Arrows_say_earlier_or_later()
    {
        var puzzle = Puzzle(twist: TimelineTwist.Arrows);
        var ids = AnswerIds(puzzle);

        // The newest card is shown first, so it goes later. The two oldest are shown one place and three
        // places too low, so both go earlier.
        var marks = puzzle.Grade([ids[4], ids[0], ids[2], ids[3], ids[1]]);

        Assert.Equal([Mark.Later, Mark.Earlier, Mark.Right, Mark.Right, Mark.Earlier], marks);
        Assert.Equal(4, puzzle.MaxTries);
    }

    [Fact]
    public void Count_only_says_how_many_are_in_place_and_not_which()
    {
        var puzzle = Puzzle(twist: TimelineTwist.CountOnly);
        var ids = AnswerIds(puzzle);

        // The last three are in place, but the row puts its Right marks first.
        var marks = puzzle.Grade([ids[1], ids[0], ids[2], ids[3], ids[4]]);

        Assert.Equal([Mark.Right, Mark.Right, Mark.Right, Mark.Wrong, Mark.Wrong], marks);
        Assert.Equal(6, puzzle.MaxTries);
        IReadOnlyList<string> wrong = [.. ids.Reverse()];
        Assert.False(puzzle.Score([wrong, wrong, wrong, wrong, wrong, wrong])!.Solved);
        Assert.Null(puzzle.Score([wrong, wrong, wrong, wrong, wrong, wrong, wrong]));
    }

    [Fact]
    public void Six_cards_has_six_trips()
    {
        var puzzle = Puzzle(twist: TimelineTwist.SixCards);

        Assert.Equal(6, puzzle.Answer.Count);
        Assert.Equal(6, puzzle.Answer.Select(card => card.TripId).Distinct().Count());
    }

    [Fact]
    public void Neighbors_are_five_trips_in_a_row_by_start_date()
    {
        // Ids out of date order, so a pick by id wouldn't be in a row.
        var library = new GameLibrary([.. Enumerable.Range(1, 12).Select(id => Trip(id, 2000 + ((id * 7) % 12)))]);
        var byStart = library.Trips.OrderBy(trip => trip.StartDate).Select(trip => trip.Id).ToList();

        for (var offset = 0; offset < 30; offset++)
        {
            var puzzle = TimelinePuzzle.Create(library, Day.AddDays(offset), TimelineTwist.Neighbors)!;
            var places = puzzle.Answer.Select(card => byStart.IndexOf(card.TripId)).ToList();
            Assert.Equal(Enumerable.Range(places[0], 5), places);
        }
    }

    [Fact]
    public void One_trip_is_five_photos_of_one_trip_from_five_days()
    {
        var library = new GameLibrary([.. Library(6).Trips, LongTrip(20, 2020, 9)]);

        var puzzle = TimelinePuzzle.Create(library, Day, TimelineTwist.OneTrip)!;

        Assert.Equal(5, puzzle.Answer.Count);
        Assert.All(puzzle.Answer, card => Assert.Equal(20, card.TripId));
        Assert.Equal(5, puzzle.Answer.Select(card => card.Taken.Date).Distinct().Count());
        Assert.Equal(puzzle.Answer.OrderBy(card => card.Taken), puzzle.Answer);
    }

    [Fact]
    public void A_twist_the_library_cant_support_falls_back_to_classic()
    {
        // Five trips of 3 days each: too few for Six cards, too short for One trip.
        var library = Library(5);
        var friday = new DateOnly(2026, 11, 27);
        var saturday = new DateOnly(2026, 11, 28);

        var six = TimelinePuzzle.Create(library, friday)!;
        var one = TimelinePuzzle.Create(library, saturday)!;

        Assert.Equal((TimelineTwist.Classic, TimelineTwist.SixCards), (six.Twist, six.Planned));
        Assert.Equal((TimelineTwist.Classic, TimelineTwist.OneTrip), (one.Twist, one.Planned));
        Assert.Equal(5, six.Answer.Count);
        Assert.StartsWith("Six cards needs 6 trips", six.Rule, StringComparison.Ordinal);
        Assert.StartsWith("One trip needs a trip", one.Rule, StringComparison.Ordinal);
        Assert.Null(TimelinePuzzle.Create(Library(4), friday));
    }

    [Fact]
    public void A_supported_twist_is_kept()
    {
        var sunday = TimelinePuzzle.Create(Library(8), new DateOnly(2026, 11, 29))!;

        Assert.Equal((TimelineTwist.CountOnly, TimelineTwist.CountOnly), (sunday.Twist, sunday.Planned));
        Assert.Equal(TimelineTwists.Rule(TimelineTwist.CountOnly), sunday.Rule);
    }
}
