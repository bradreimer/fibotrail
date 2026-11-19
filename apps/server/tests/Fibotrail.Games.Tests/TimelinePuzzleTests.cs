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

    private static TimelinePuzzle Puzzle(int trips = 8) => TimelinePuzzle.Create(Library(trips), Day)!;

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
        Assert.Equal(5, puzzle.Answer.Select(card => card.TripName).Distinct().Count());
        Assert.Equal(puzzle.Answer.OrderBy(card => card.Taken), puzzle.Answer);
        Assert.Equal(puzzle.Answer.ToHashSet(), puzzle.Shown.ToHashSet());
    }

    [Fact]
    public void The_shown_order_is_never_the_answer()
    {
        for (var offset = 0; offset < 200; offset++)
        {
            var puzzle = TimelinePuzzle.Create(Library(5), Day.AddDays(offset))!;
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
}
