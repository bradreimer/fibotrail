using Fibotrail.Games.Stamps;

namespace Fibotrail.Games.Tests.Stamps;

/// <summary>The Stamps game's daily puzzle. See docs/specs/stamps-game.md.</summary>
public class StampsPuzzleTests
{
    /// <summary>A Wednesday, so a 7 by 7 board.</summary>
    private static readonly DateOnly Wednesday = new(2026, 11, 25);

    private static GameTrip Trip(int id, int photos = 3) => new(
        id,
        $"Trip {id}",
        new DateOnly(2030 - id, 6, 1),
        new DateOnly(2030 - id, 6, 5),
        $"Place {id}",
        [.. Enumerable.Range(0, photos).Select(index => new GamePhoto($"t{id}-p{index}", new DateTime(2030 - id, 6, 1 + index, 10, 0, 0)))]);

    private static GameLibrary Library(int trips = 9) => new([.. Enumerable.Range(1, trips).Select(id => Trip(id))]);

    [Theory]
    [InlineData(2026, 11, 23, 6, "Easy")]
    [InlineData(2026, 11, 24, 6, "Easy")]
    [InlineData(2026, 11, 25, 7, "Medium")]
    [InlineData(2026, 11, 27, 7, "Medium")]
    [InlineData(2026, 11, 28, 8, "Hard")]
    [InlineData(2026, 11, 29, 8, "Hard")]
    public void The_weekday_sets_the_size(int year, int month, int day, int size, string label)
    {
        var puzzle = StampsPuzzle.Create(Library(), new DateOnly(year, month, day))!;

        Assert.Equal((size, label), (puzzle.Size, puzzle.Label));
        Assert.Null(puzzle.SizeNote);
        Assert.Equal(size * size, puzzle.Regions.Count);
        Assert.Equal(size * size, puzzle.Photos.Count);
        Assert.Equal(size, puzzle.Trips.Count);
    }

    [Fact]
    public void Fewer_trips_than_the_size_make_a_smaller_board()
    {
        var puzzle = StampsPuzzle.Create(Library(5), new DateOnly(2026, 11, 28))!;

        Assert.Equal(5, puzzle.Size);
        Assert.Equal("Easy", puzzle.Label);
        Assert.Equal("5 × 5 today, because you have 5 trips.", puzzle.SizeNote);
    }

    [Fact]
    public void Fewer_than_five_trips_with_photos_make_no_board()
    {
        var library = new GameLibrary([.. Library(4).Trips, Trip(9, photos: 0)]);

        Assert.Null(StampsPuzzle.Create(library, Wednesday));
        Assert.Equal(1, StampsPuzzle.TripsNeeded(library));
        Assert.Equal(5, StampsPuzzle.TripsNeeded(GameLibrary.Empty));
        Assert.Equal(0, StampsPuzzle.TripsNeeded(Library()));
    }

    [Fact]
    public void The_same_date_and_library_give_the_same_board_whatever_the_trip_order()
    {
        var first = StampsPuzzle.Create(Library(), Wednesday)!;
        var reversed = StampsPuzzle.Create(new GameLibrary([.. Library().Trips.Reverse()]), Wednesday)!;

        Assert.Equal(first.Regions, reversed.Regions);
        Assert.Equal(first.Photos, reversed.Photos);
        Assert.Equal(first.Trips, reversed.Trips);
        Assert.NotEqual(first.Regions, StampsPuzzle.Create(Library(), Wednesday.AddDays(7))!.Regions);
    }

    [Fact]
    public void Each_region_shows_its_own_trip_photos_and_its_stamp_cell_is_the_passport_photo()
    {
        var puzzle = StampsPuzzle.Create(Library(), Wednesday)!;

        foreach (var trip in puzzle.Trips)
        {
            var cells = Enumerable.Range(0, puzzle.Regions.Count).Where(cell => puzzle.Regions[cell] == trip.Region);
            Assert.All(cells, cell => Assert.StartsWith($"t{trip.TripId}-", puzzle.Photos[cell], StringComparison.Ordinal));
            Assert.Equal(puzzle.Photos[trip.StampCell], trip.PhotoId);
            Assert.Contains(trip.StampCell, puzzle.StampCells);
        }

        Assert.True(puzzle.IsSolvedBy(puzzle.StampCells));
    }

    [Fact]
    public void A_hint_points_at_a_wrong_stamp_then_a_wrong_cross_then_the_next_step()
    {
        var puzzle = StampsPuzzle.Create(Library(), Wednesday)!;
        var solution = puzzle.StampCells;
        var wrong = Enumerable.Range(0, puzzle.Regions.Count).First(cell => !solution.Contains(cell));

        Assert.Equal(new StampsStep(wrong, false, StampsRule.WrongStamp, "This stamp isn't part of the solution."), puzzle.Hint([wrong], []));
        Assert.Equal(StampsRule.WrongCross, puzzle.Hint([], [solution[2]])!.Rule);

        var next = puzzle.Hint([], [])!;
        Assert.Equal(next.Stamp, solution.Contains(next.Cell));
        Assert.Null(puzzle.Hint(solution, []));
    }

    [Fact]
    public void Following_hints_from_an_empty_board_solves_it()
    {
        var puzzle = StampsPuzzle.Create(Library(), new DateOnly(2026, 11, 28))!;
        var stamps = new List<int>();
        var crosses = new List<int>();
        for (var hint = puzzle.Hint(stamps, crosses); hint is not null; hint = puzzle.Hint(stamps, crosses))
        {
            (hint.Stamp ? stamps : crosses).Add(hint.Cell);
        }

        Assert.True(puzzle.IsSolvedBy(stamps));
    }

    [Fact]
    public void A_hint_board_must_be_on_the_board_with_no_cell_twice()
    {
        var puzzle = StampsPuzzle.Create(Library(), Wednesday)!;

        Assert.True(puzzle.IsOnBoard([0], [1, 2]));
        Assert.False(puzzle.IsOnBoard([49], []));
        Assert.False(puzzle.IsOnBoard([-1], []));
        Assert.False(puzzle.IsOnBoard([3], [3]));
    }

    [Fact]
    public void Marks_hold_the_columns_and_the_hints()
    {
        var marks = StampsPuzzle.Encode(5, [23, 0, 7, 14, 16], 2);

        Assert.Equal("02413h2", marks);
        Assert.Equal(5, StampsPuzzle.SizeOf(marks));
        Assert.Equal(2, StampsPuzzle.HintsOf(marks));
        Assert.Equal("5 × 5", StampsPuzzle.TimeLabel(marks));
    }

    [Theory]
    [InlineData(5, "0:05")]
    [InlineData(161, "2:41")]
    [InlineData(3599, "59:59")]
    [InlineData(3761, "1:02:41")]
    public void Times_show_as_minutes_and_seconds(int seconds, string shown)
    {
        Assert.Equal(shown, StampsPuzzle.FormatTime(seconds));
    }

    [Fact]
    public void The_share_text_has_no_trip_name_place_or_date()
    {
        Assert.Equal("Fibotrail Stamps #7 Wed 7×7 2:41 💡0", StampsPuzzle.ShareText(7, Wednesday, 7, 161, 0));
    }
}
