using System.Globalization;

namespace Fibotrail.Games.Slide;

/// <summary>The trip a puzzle's photo comes from, shown when the puzzle is solved.</summary>
/// <param name="Place">The trip's main destination, or null when none is known.</param>
public sealed record SlidePhoto(string PhotoId, int TripId, string TripName, DateOnly StartDate, DateOnly EndDate, string? Place);

/// <summary>
/// The Slide game: one trip photo cut into a 3 by 3 grid, with the bottom-right tile removed and the
/// other 8 shuffled. You slide tiles into the empty cell until the photo is whole. Pure functions
/// over the game library. See docs/specs/slide-game.md.
/// </summary>
/// <param name="Start">
/// The board as first shown: 9 cells, row by row from the top left. A cell holds a tile number from
/// 1 to 8, where tile n belongs in cell n - 1, or 0 for the empty cell.
/// </param>
public sealed record SlidePuzzle(DateOnly Date, int Number, SlidePhoto Photo, IReadOnlyList<int> Start)
{
    public const string Id = "slide";
    public const string Name = "Slide";

    /// <summary>The grid is Size by Size cells.</summary>
    public const int Size = 3;

    /// <summary>The most moves a result may hold. A real game takes far fewer.</summary>
    public const int MostMoves = 5000;

    /// <summary>The solved board: tiles 1 to 8 in order, and the empty cell at the bottom right.</summary>
    public static readonly IReadOnlyList<int> Solved = [1, 2, 3, 4, 5, 6, 7, 8, 0];

    /// <summary>How many more trips with photos the library needs before there's a puzzle: 1 or 0.</summary>
    public static int TripsNeeded(GameLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        return library.Trips.Any(trip => trip.Photos.Count > 0) ? 0 : 1;
    }

    /// <summary>The puzzle of a date, or null when no added trip has a photo.</summary>
    public static SlidePuzzle? Create(GameLibrary library, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(library);
        var random = new SeededRandom(DailySeed.For(Id, date));

        // Sort first, so the picks don't depend on the order the database returned.
        var eligible = library.Trips.Where(trip => trip.Photos.Count > 0).OrderBy(trip => trip.Id).ToList();
        if (eligible.Count == 0)
        {
            return null;
        }

        var trip = eligible[random.Next(eligible.Count)];
        var photos = trip.Photos.OrderBy(photo => photo.LocalTime).ThenBy(photo => photo.Id, StringComparer.Ordinal).ToList();
        var photo = photos[random.Next(photos.Count)];
        return new SlidePuzzle(
            date,
            DailySeed.PuzzleNumber(date),
            new SlidePhoto(photo.Id, trip.Id, trip.Name, trip.StartDate, trip.EndDate, trip.Place),
            Shuffle(random));
    }

    /// <summary>
    /// A random start that can be solved and isn't solved. With the empty cell in the bottom-right
    /// corner, a 3 by 3 board can be solved exactly when its 8 tiles hold an even number of
    /// inversions (pairs in the wrong order), so an odd shuffle swaps its first two tiles.
    /// </summary>
    public static IReadOnlyList<int> Shuffle(SeededRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var tiles = random.Shuffle(Enumerable.Range(1, (Size * Size) - 1));
        if (Inversions(tiles) % 2 == 1)
        {
            (tiles[0], tiles[1]) = (tiles[1], tiles[0]);
        }

        if (tiles.SequenceEqual(Solved.Take(tiles.Count)))
        {
            // A 3-cycle keeps the inversions even, so the board stays solvable.
            (tiles[0], tiles[1], tiles[2]) = (tiles[1], tiles[2], tiles[0]);
        }

        return [.. tiles, 0];
    }

    /// <summary>Pairs of tiles in the wrong order.</summary>
    public static int Inversions(IReadOnlyList<int> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        var count = 0;
        for (var i = 0; i < tiles.Count; i++)
        {
            for (var j = i + 1; j < tiles.Count; j++)
            {
                if (tiles[i] != 0 && tiles[j] != 0 && tiles[i] > tiles[j])
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// The board after sliding <paramref name="tile"/> into the empty cell, or null when the tile
    /// isn't next to it. Diagonals don't count.
    /// </summary>
    public static int[]? Move(IReadOnlyList<int> board, int tile)
    {
        ArgumentNullException.ThrowIfNull(board);
        var from = IndexOf(board, tile);
        var empty = IndexOf(board, 0);
        if (tile == 0 || from < 0 || empty < 0)
        {
            return null;
        }

        var apart = Math.Abs((from / Size) - (empty / Size)) + Math.Abs((from % Size) - (empty % Size));
        if (apart != 1)
        {
            return null;
        }

        var next = board.ToArray();
        (next[from], next[empty]) = (next[empty], next[from]);
        return next;
    }

    /// <summary>
    /// Replays a list of moves from the start. True only when every move is legal, the board ends
    /// solved, and no move comes after it's solved.
    /// </summary>
    public bool IsSolvedBy(IReadOnlyList<int> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);
        if (moves.Count == 0 || moves.Count > MostMoves)
        {
            return false;
        }

        IReadOnlyList<int> board = Start;
        foreach (var tile in moves)
        {
            if (board.SequenceEqual(Solved) || Move(board, tile) is not { } next)
            {
                return false;
            }

            board = next;
        }

        return board.SequenceEqual(Solved);
    }

    /// <summary>The moves as stored text: one digit per tile moved, such as "6853".</summary>
    public static string Encode(IEnumerable<int> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);
        return string.Concat(moves.Select(tile => tile.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// The text you paste to brag: the game, the puzzle number, and the moves. It holds no trip
    /// name, place, date, or photo.
    /// </summary>
    public static string ShareText(int puzzleNumber, int moves) =>
        string.Create(CultureInfo.InvariantCulture, $"Fibotrail {Name} #{puzzleNumber} {moves} {(moves == 1 ? "move" : "moves")}");

    private static int IndexOf(IReadOnlyList<int> board, int tile)
    {
        for (var i = 0; i < board.Count; i++)
        {
            if (board[i] == tile)
            {
                return i;
            }
        }

        return -1;
    }
}
