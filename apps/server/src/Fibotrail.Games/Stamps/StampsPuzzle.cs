using System.Globalization;

namespace Fibotrail.Games.Stamps;

/// <summary>The trip of one region, shown on the passport page when the board is solved.</summary>
/// <param name="Region">The region's number, 0 for A.</param>
/// <param name="StampCell">The cell of the region's stamp in the solution.</param>
/// <param name="PhotoId">The photo in the stamp cell, which becomes the passport photo.</param>
public sealed record StampsTrip(int Region, int StampCell, string PhotoId, int TripId, string TripName, DateOnly StartDate, DateOnly EndDate);

/// <summary>
/// The Stamps game: an N by N board split into N regions, one per trip. Place one stamp in each
/// row, column, and region, with no two touching. Pure functions over the game library. See
/// docs/specs/stamps-game.md.
/// </summary>
/// <param name="Size">N, the board's rows and columns.</param>
/// <param name="Label">How hard the size is: Easy, Medium, or Hard.</param>
/// <param name="SizeNote">Why the board is smaller than the weekday's size, or null when it isn't.</param>
/// <param name="Regions">The region of each cell, row by row from the top left.</param>
/// <param name="Solution">The column of the stamp in each row.</param>
/// <param name="Photos">The photo of each cell, row by row.</param>
/// <param name="Trips">The trip of each region, by region number.</param>
public sealed record StampsPuzzle(
    DateOnly Date,
    int Number,
    int Size,
    string Label,
    string? SizeNote,
    IReadOnlyList<int> Regions,
    IReadOnlyList<int> Solution,
    IReadOnlyList<string> Photos,
    IReadOnlyList<StampsTrip> Trips)
{
    public const string Id = "stamps";
    public const string Name = "Stamps";

    /// <summary>The smallest board, and so the fewest trips with photos the game needs.</summary>
    public const int SmallestSize = 5;

    /// <summary>The longest time a result may report, a day in seconds.</summary>
    public const int MostSeconds = 86_400;

    public const string Rule = "Place one stamp in each row, column, and trip. No two stamps touch, even at a corner.";

    /// <summary>The cells of the solution's stamps, by row.</summary>
    public IReadOnlyList<int> StampCells => [.. Enumerable.Range(0, Size).Select(row => (row * Size) + Solution[row])];

    /// <summary>The weekday's size: 6 on Monday and Tuesday, 7 from Wednesday to Friday, 8 on the weekend.</summary>
    public static int SizeFor(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday or DayOfWeek.Tuesday => 6,
        DayOfWeek.Saturday or DayOfWeek.Sunday => 8,
        _ => 7,
    };

    /// <summary>How hard a size is.</summary>
    public static string LabelFor(int size) => size switch
    {
        <= 6 => "Easy",
        7 => "Medium",
        _ => "Hard",
    };

    /// <summary>How many more trips with photos the library needs before there's a board.</summary>
    public static int TripsNeeded(GameLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        return Math.Max(0, SmallestSize - Eligible(library).Count);
    }

    /// <summary>The puzzle of a date, or null when the library has fewer than 5 trips with photos.</summary>
    public static StampsPuzzle? Create(GameLibrary library, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(library);
        var random = new SeededRandom(DailySeed.For(Id, date));
        var eligible = Eligible(library);
        if (eligible.Count < SmallestSize)
        {
            return null;
        }

        var planned = SizeFor(date.DayOfWeek);
        var trips = random.Shuffle(eligible);
        for (var size = Math.Min(planned, trips.Count); size >= SmallestSize; size--)
        {
            if (StampsBoard.Generate(random, size) is not { } layout)
            {
                continue;
            }

            var note = size == planned
                ? null
                : size == trips.Count
                    ? string.Create(CultureInfo.InvariantCulture, $"{size} × {size} today, because you have {trips.Count} trips.")
                    : string.Create(CultureInfo.InvariantCulture, $"{size} × {size} today.");
            return Build(date, layout, LabelFor(size), note, trips.Take(size).ToList(), random);
        }

        return null;
    }

    /// <summary>The cells a result's stamps are in, when they're a solution of this board.</summary>
    public bool IsSolvedBy(IReadOnlyList<int> stamps) => StampsBoard.IsSolution(Size, Regions, stamps);

    /// <summary>
    /// A hint from a board you're playing: a stamp the solution doesn't have or a ✕ on a solution
    /// cell first, and otherwise the logic solver's next step. Null when nothing is left to find.
    /// </summary>
    public StampsStep? Hint(IReadOnlyList<int> stamps, IReadOnlyList<int> crosses)
    {
        ArgumentNullException.ThrowIfNull(stamps);
        ArgumentNullException.ThrowIfNull(crosses);
        var solution = new HashSet<int>(StampCells);
        if (stamps.FirstOrDefault(cell => !solution.Contains(cell), -1) is var wrong and >= 0)
        {
            return new StampsStep(wrong, false, StampsRule.WrongStamp, "This stamp isn't part of the solution.");
        }

        if (crosses.FirstOrDefault(solution.Contains, -1) is var crossed and >= 0)
        {
            return new StampsStep(crossed, true, StampsRule.WrongCross, "This cell needs a stamp, so it can't be ✕.");
        }

        var board = new StampsCell[Size * Size];
        foreach (var cell in crosses)
        {
            board[cell] = StampsCell.Out;
        }

        foreach (var cell in stamps)
        {
            board[cell] = StampsCell.Stamp;
        }

        return StampsBoard.NextStep(Size, Regions, board);
    }

    /// <summary>True when every cell of a hint request is on the board, and none is both a stamp and a ✕.</summary>
    public bool IsOnBoard(IReadOnlyList<int> stamps, IReadOnlyList<int> crosses)
    {
        ArgumentNullException.ThrowIfNull(stamps);
        ArgumentNullException.ThrowIfNull(crosses);
        var all = stamps.Concat(crosses).ToList();
        return all.All(cell => cell >= 0 && cell < Size * Size) && all.Distinct().Count() == all.Count;
    }

    /// <summary>The letter of a region: A for 0, B for 1, and so on.</summary>
    public static string Letter(int region) => ((char)('A' + region)).ToString();

    /// <summary>The regions as rows of letters, such as "AABBBCC".</summary>
    public IReadOnlyList<string> RegionRows() =>
        [.. Enumerable.Range(0, Size).Select(row => string.Concat(Enumerable.Range(0, Size).Select(column => Letter(Regions[(row * Size) + column]))))];

    /// <summary>
    /// The stored marks of a result: the stamp's column in each row, one digit per row, then h and
    /// the hints, such as "1503624h0".
    /// </summary>
    public static string Encode(int size, IReadOnlyList<int> stamps, int hints)
    {
        ArgumentNullException.ThrowIfNull(stamps);
        var columns = stamps.OrderBy(cell => cell).Select(cell => (cell % size).ToString(CultureInfo.InvariantCulture));
        return string.Create(CultureInfo.InvariantCulture, $"{string.Concat(columns)}h{hints}");
    }

    /// <summary>The board size of stored marks: the digits before the h.</summary>
    public static int SizeOf(string marks)
    {
        ArgumentNullException.ThrowIfNull(marks);
        var h = marks.IndexOf('h', StringComparison.Ordinal);
        return h < 0 ? marks.Length : h;
    }

    /// <summary>The hints of stored marks: the number after the h.</summary>
    public static int HintsOf(string marks)
    {
        ArgumentNullException.ThrowIfNull(marks);
        var h = marks.IndexOf('h', StringComparison.Ordinal);
        return h < 0 ? 0 : int.Parse(marks.AsSpan(h + 1), CultureInfo.InvariantCulture);
    }

    /// <summary>The label the time stats are split by: the board size, such as "7 × 7".</summary>
    public static string? TimeLabel(string marks) =>
        string.Create(CultureInfo.InvariantCulture, $"{SizeOf(marks)} × {SizeOf(marks)}");

    /// <summary>A time as minutes and seconds, such as "2:41", or hours too, such as "1:02:41".</summary>
    public static string FormatTime(int seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{(int)time.TotalMinutes}:{time.Seconds:00}");
    }

    /// <summary>
    /// The text you paste to brag, such as "Fibotrail Stamps #12 Wed 7×7 2:41 💡0". It holds no trip
    /// name, place, date, or photo.
    /// </summary>
    public static string ShareText(int puzzleNumber, DateOnly date, int size, int seconds, int hints) =>
        string.Create(CultureInfo.InvariantCulture, $"Fibotrail {Name} #{puzzleNumber} {date.ToString("ddd", CultureInfo.InvariantCulture)} {size}×{size} {FormatTime(seconds)} 💡{hints}");

    private static List<GameTrip> Eligible(GameLibrary library) =>
        [.. library.Trips.Where(trip => trip.Photos.Count > 0).OrderBy(trip => trip.Id)];

    private static StampsPuzzle Build(DateOnly date, StampsLayout layout, string label, string? note, List<GameTrip> trips, SeededRandom random)
    {
        var size = layout.Size;
        var photos = new string[size * size];
        var stampCells = Enumerable.Range(0, size).Select(row => (row * size) + layout.Solution[row]).ToList();
        var regionTrips = new List<StampsTrip>();
        for (var region = 0; region < size; region++)
        {
            var trip = trips[region];
            var tripPhotos = trip.Photos.OrderBy(photo => photo.LocalTime).ThenBy(photo => photo.Id, StringComparer.Ordinal).ToList();
            var offset = random.Next(tripPhotos.Count);
            var cells = Enumerable.Range(0, size * size).Where(cell => layout.Regions[cell] == region).ToList();
            for (var i = 0; i < cells.Count; i++)
            {
                photos[cells[i]] = tripPhotos[(offset + i) % tripPhotos.Count].Id;
            }

            var stamp = stampCells.First(cell => layout.Regions[cell] == region);
            regionTrips.Add(new StampsTrip(region, stamp, photos[stamp], trip.Id, trip.Name, trip.StartDate, trip.EndDate));
        }

        return new StampsPuzzle(date, DailySeed.PuzzleNumber(date), size, label, note, layout.Regions, layout.Solution, photos, regionTrips);
    }
}
