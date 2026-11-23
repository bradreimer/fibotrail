using Fibotrail.Games.Slide;

namespace Fibotrail.Games.Tests;

/// <summary>The Slide game. See docs/specs/slide-game.md.</summary>
public class SlidePuzzleTests
{
    private static readonly DateOnly Day = new(2026, 11, 20);

    private static GameTrip Trip(int id, int photos = 3) => new(
        id,
        $"Trip {id}",
        new DateOnly(2020 + id, 6, 1),
        new DateOnly(2020 + id, 6, 5),
        $"Place {id}",
        [.. Enumerable.Range(0, photos).Select(index => new GamePhoto($"t{id}-p{index}", new DateTime(2020 + id, 6, 1 + index, 10, 0, 0)))]);

    private static GameLibrary Library(int trips = 4) => new([.. Enumerable.Range(1, trips).Select(id => Trip(id))]);

    /// <summary>The fewest moves from a board to solved, by breadth-first search over the 181,440 boards a start can reach.</summary>
    internal static List<int> Solve(IReadOnlyList<int> start)
    {
        var key = string.Concat(SlidePuzzle.Solved);
        var from = new Dictionary<string, (string Previous, int Tile)> { [string.Concat(start)] = (string.Empty, 0) };
        var queue = new Queue<int[]>([[.. start]]);
        while (queue.Count > 0)
        {
            var board = queue.Dequeue();
            var here = string.Concat(board);
            if (here == key)
            {
                var moves = new List<int>();
                for (var at = here; from[at].Previous.Length > 0; at = from[at].Previous)
                {
                    moves.Add(from[at].Tile);
                }

                moves.Reverse();
                return moves;
            }

            foreach (var tile in Enumerable.Range(1, 8))
            {
                if (SlidePuzzle.Move(board, tile) is { } next && from.TryAdd(string.Concat(next), (here, tile)))
                {
                    queue.Enqueue(next);
                }
            }
        }

        throw new InvalidOperationException("The board can't be solved.");
    }

    [Fact]
    public void A_library_with_no_photos_has_no_puzzle()
    {
        var library = new GameLibrary([Trip(1, photos: 0)]);

        Assert.Null(SlidePuzzle.Create(library, Day));
        Assert.Equal(1, SlidePuzzle.TripsNeeded(library));
        Assert.Equal(0, SlidePuzzle.TripsNeeded(Library()));
    }

    [Fact]
    public void The_same_date_and_library_give_the_same_puzzle()
    {
        var first = SlidePuzzle.Create(Library(), Day)!;
        var second = SlidePuzzle.Create(new GameLibrary([.. Library().Trips.Reverse()]), Day)!;

        Assert.Equal(first.Photo, second.Photo);
        Assert.Equal(first.Start, second.Start);
        Assert.Equal(2, first.Number);
    }

    [Fact]
    public void The_photo_comes_from_a_trip_with_its_name_dates_and_place()
    {
        var puzzle = SlidePuzzle.Create(Library(), Day)!;
        var trip = Library().Trips.Single(trip => trip.Id == puzzle.Photo.TripId);

        Assert.Contains(trip.Photos, photo => photo.Id == puzzle.Photo.PhotoId);
        Assert.Equal((trip.Name, trip.StartDate, trip.EndDate, trip.Place), (puzzle.Photo.TripName, puzzle.Photo.StartDate, puzzle.Photo.EndDate, puzzle.Photo.Place));
    }

    [Fact]
    public void Every_start_is_solvable_and_not_solved_with_the_empty_cell_at_the_bottom_right()
    {
        for (ulong seed = 0; seed < 500; seed++)
        {
            var start = SlidePuzzle.Shuffle(new SeededRandom(seed));

            Assert.Equal(0, start[^1]);
            Assert.Equal(Enumerable.Range(0, 9), start.Order());
            Assert.Equal(0, SlidePuzzle.Inversions(start) % 2);
            Assert.NotEqual(SlidePuzzle.Solved, start);
        }
    }

    [Fact]
    public void A_tile_moves_only_when_it_is_next_to_the_empty_cell()
    {
        int[] board = [1, 2, 3, 4, 5, 6, 7, 8, 0];

        Assert.Equal([1, 2, 3, 4, 5, 0, 7, 8, 6], SlidePuzzle.Move(board, 6)!);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 0, 8], SlidePuzzle.Move(board, 8)!);
        Assert.Null(SlidePuzzle.Move(board, 5));
        Assert.Null(SlidePuzzle.Move(board, 1));
        Assert.Null(SlidePuzzle.Move(board, 0));
        Assert.Null(SlidePuzzle.Move(board, 9));

        // Cell 3 starts a row, so it isn't next to cell 2, at the end of the row before.
        Assert.Null(SlidePuzzle.Move([1, 2, 0, 3, 4, 5, 6, 7, 8], 3));
    }

    [Fact]
    public void Moves_solve_the_puzzle_only_when_the_replay_ends_solved()
    {
        var puzzle = SlidePuzzle.Create(Library(), Day)!;
        var moves = Solve(puzzle.Start);

        Assert.True(puzzle.IsSolvedBy(moves));
        Assert.False(puzzle.IsSolvedBy([]));
        Assert.False(puzzle.IsSolvedBy(moves[..^1]));
        Assert.False(puzzle.IsSolvedBy([.. moves, moves[^1]]));
        Assert.False(puzzle.IsSolvedBy([9, .. moves]));
    }

    [Fact]
    public void The_moves_are_stored_as_digits_and_the_share_text_counts_them()
    {
        Assert.Equal("6853", SlidePuzzle.Encode([6, 8, 5, 3]));
        Assert.Equal("Fibotrail Slide #5 42 moves", SlidePuzzle.ShareText(5, 42));
        Assert.Equal("Fibotrail Slide #5 1 move", SlidePuzzle.ShareText(5, 1));
    }
}
