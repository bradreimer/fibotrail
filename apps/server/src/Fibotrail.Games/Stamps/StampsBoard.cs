using System.Numerics;

namespace Fibotrail.Games.Stamps;

/// <summary>What is known about one cell of a board.</summary>
public enum StampsCell
{
    /// <summary>Could still hold a stamp.</summary>
    Open,

    /// <summary>Can't hold a stamp. ✕</summary>
    Out,

    /// <summary>Holds a stamp. 🛂</summary>
    Stamp,
}

/// <summary>The rule behind a step of the logic solver, or a hint.</summary>
public enum StampsRule
{
    /// <summary>A row with one open cell gets a stamp there.</summary>
    OnlyCellInRow,

    /// <summary>A column with one open cell gets a stamp there.</summary>
    OnlyCellInColumn,

    /// <summary>A region with one open cell gets a stamp there.</summary>
    OnlyCellInRegion,

    /// <summary>k regions whose open cells are all in the same k rows rule out the rest of those rows.</summary>
    RegionsFillRows,

    /// <summary>k regions whose open cells are all in the same k columns rule out the rest of those columns.</summary>
    RegionsFillColumns,

    /// <summary>A stamp here would leave some row, column, or region with no open cell.</summary>
    WouldLeaveNoRoom,

    /// <summary>A hint only: a stamp the solution doesn't have.</summary>
    WrongStamp,

    /// <summary>A hint only: a ✕ on a cell the solution stamps.</summary>
    WrongCross,
}

/// <summary>One step of the logic solver: a cell that must hold a stamp, or must not.</summary>
/// <param name="Cell">The cell, row by row from the top left.</param>
/// <param name="Stamp">True for a stamp, false for a ✕.</param>
/// <param name="Reason">The rule in one plain sentence, for a hint.</param>
public sealed record StampsStep(int Cell, bool Stamp, StampsRule Rule, string Reason);

/// <summary>A generated board: its regions and its one solution.</summary>
/// <param name="Regions">The region of each cell, row by row. Regions are numbered from 0 in the order they first appear.</param>
/// <param name="Solution">The column of the stamp in each row.</param>
/// <param name="Attempts">How many boards were tried before this one was kept.</param>
public sealed record StampsLayout(int Size, IReadOnlyList<int> Regions, IReadOnlyList<int> Solution, int Attempts);

/// <summary>
/// The rules of a Stamps board, as pure functions: an N by N grid split into N regions, where one
/// stamp goes in each row, each column, and each region, and no two stamps touch, even at a corner.
/// It generates boards, counts their solutions, and solves them by logic alone. See
/// docs/specs/stamps-game.md#the-puzzle.
/// </summary>
public static class StampsBoard
{
    /// <summary>The most boards tried for one size before the generator gives up on it.</summary>
    public const int MostAttempts = 2000;

    /// <summary>
    /// A board that has exactly one solution and that <see cref="SolvesByLogic"/> solves, or null
    /// when none was found in <see cref="MostAttempts"/> attempts.
    /// </summary>
    public static StampsLayout? Generate(SeededRandom random, int size)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 4);
        for (var attempt = 1; attempt <= MostAttempts; attempt++)
        {
            var columns = PlaceStamps(random, size);
            var regions = GrowRegions(random, size, columns);
            if (Repair(random, size, regions, columns) && SolvesByLogic(size, regions))
            {
                return new StampsLayout(size, regions, columns, attempt);
            }
        }

        return null;
    }

    /// <summary>
    /// One stamp per row and per column, where stamps in rows next to each other are at least 2
    /// columns apart, so no two touch. The result is the column of each row's stamp.
    /// </summary>
    public static int[] PlaceStamps(SeededRandom random, int size)
    {
        ArgumentNullException.ThrowIfNull(random);
        var columns = new int[size];
        var used = new bool[size];
        if (!Place(0))
        {
            throw new InvalidOperationException($"No stamps fit a {size} by {size} board.");
        }

        return columns;

        bool Place(int row)
        {
            foreach (var column in random.Shuffle(Enumerable.Range(0, size)))
            {
                if (used[column] || (row > 0 && Math.Abs(column - columns[row - 1]) < 2))
                {
                    continue;
                }

                columns[row] = column;
                used[column] = true;
                if (row == size - 1 || Place(row + 1))
                {
                    return true;
                }

                used[column] = false;
            }

            return false;
        }
    }

    /// <summary>
    /// Grows one region from each stamp. Again and again, a random region takes a random empty cell
    /// next to it (up, down, left, or right), so every region is connected. Regions are then
    /// numbered in the order they first appear, row by row.
    /// </summary>
    public static int[] GrowRegions(SeededRandom random, int size, IReadOnlyList<int> columns)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(columns);
        var regions = Enumerable.Repeat(-1, size * size).ToArray();
        for (var row = 0; row < size; row++)
        {
            regions[(row * size) + columns[row]] = row;
        }

        var empty = (size * size) - size;
        while (empty > 0)
        {
            var frontier = new List<int>[size];
            for (var region = 0; region < size; region++)
            {
                frontier[region] = [];
            }

            for (var cell = 0; cell < regions.Length; cell++)
            {
                if (regions[cell] >= 0)
                {
                    continue;
                }

                foreach (var next in Sides(size, cell))
                {
                    if (regions[next] >= 0 && !frontier[regions[next]].Contains(cell))
                    {
                        frontier[regions[next]].Add(cell);
                    }
                }
            }

            var growing = Enumerable.Range(0, size).Where(region => frontier[region].Count > 0).ToList();
            var chosen = growing[random.Next(growing.Count)];
            regions[frontier[chosen][random.Next(frontier[chosen].Count)]] = chosen;
            empty--;
        }

        return Renumber(regions);
    }

    /// <summary>
    /// Makes the planned stamps the board's only solution. Random regions almost never have one
    /// solution on 7 by 7 and larger boards, so while another solution exists, one of its cells that
    /// isn't a planned stamp moves into a region next to it. That other solution then has two stamps
    /// in one region, so it breaks, and the planned one still holds. Regions stay connected, and are
    /// numbered again at the end. False when no move is left, or after 4 moves per cell.
    /// </summary>
    public static bool Repair(SeededRandom random, int size, int[] regions, IReadOnlyList<int> columns)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(columns);
        var planned = new HashSet<int>(Enumerable.Range(0, size).Select(row => (row * size) + columns[row]));
        for (var step = 0; step < 4 * size * size; step++)
        {
            var other = Solutions(size, regions, 2).FirstOrDefault(solution => !solution.SequenceEqual(columns));
            if (other is null)
            {
                Renumber(regions).CopyTo(regions, 0);
                return true;
            }

            var moves = new List<(int Cell, int Region)>();
            foreach (var cell in Enumerable.Range(0, size).Select(row => (row * size) + other[row]).Where(cell => !planned.Contains(cell)))
            {
                foreach (var next in Sides(size, cell).Where(next => regions[next] != regions[cell]))
                {
                    if (StaysConnected(size, regions, cell) && !moves.Contains((cell, regions[next])))
                    {
                        moves.Add((cell, regions[next]));
                    }
                }
            }

            if (moves.Count == 0)
            {
                return false;
            }

            var (moved, region) = moves[random.Next(moves.Count)];
            regions[moved] = region;
        }

        return false;
    }

    /// <summary>Solutions of a board, counted up to <paramref name="limit"/>, by a search row by row.</summary>
    public static int CountSolutions(int size, IReadOnlyList<int> regions, int limit) => Solutions(size, regions, limit).Count;

    /// <summary>
    /// Solutions of a board, up to <paramref name="limit"/>, by a search row by row. Each is the
    /// column of the stamp in each row.
    /// </summary>
    public static IReadOnlyList<int[]> Solutions(int size, IReadOnlyList<int> regions, int limit)
    {
        ArgumentNullException.ThrowIfNull(regions);
        var found = new List<int[]>();
        var columns = new int[size];
        Search(0, 0, 0, -10);
        return found;

        void Search(int row, int usedColumns, int usedRegions, int previous)
        {
            if (row == size)
            {
                found.Add([.. columns]);
                return;
            }

            for (var column = 0; column < size && found.Count < limit; column++)
            {
                var region = regions[(row * size) + column];
                if ((usedColumns & (1 << column)) != 0 || (usedRegions & (1 << region)) != 0 || Math.Abs(column - previous) < 2)
                {
                    continue;
                }

                columns[row] = column;
                Search(row + 1, usedColumns | (1 << column), usedRegions | (1 << region), column);
            }
        }
    }

    /// <summary>True when the region of <paramref name="cell"/> stays in one piece without it.</summary>
    private static bool StaysConnected(int size, int[] regions, int cell)
    {
        var region = regions[cell];
        var rest = Enumerable.Range(0, size * size).Where(other => other != cell && regions[other] == region).ToList();
        if (rest.Count == 0)
        {
            return false;
        }

        var seen = new HashSet<int> { rest[0] };
        var queue = new Queue<int>(seen);
        while (queue.TryDequeue(out var here))
        {
            foreach (var next in Sides(size, here).Where(next => next != cell && regions[next] == region && seen.Add(next)))
            {
                queue.Enqueue(next);
            }
        }

        return seen.Count == rest.Count;
    }

    /// <summary>True when the logic solver's rules alone stamp every row, with no guessing.</summary>
    public static bool SolvesByLogic(int size, IReadOnlyList<int> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);
        var state = new StampsCell[size * size];
        while (NextStep(size, regions, state) is { } step)
        {
            state[step.Cell] = step.Stamp ? StampsCell.Stamp : StampsCell.Out;
        }

        return state.Count(cell => cell == StampsCell.Stamp) == size && IsSolution(size, regions, StampCells(state));
    }

    /// <summary>
    /// The next step of the logic solver from a board, or null when no rule applies. The cells that
    /// a stamp rules out (rule 2) count as ✕ without being a step of their own. A step always
    /// changes an open cell.
    /// </summary>
    public static StampsStep? NextStep(int size, IReadOnlyList<int> regions, IReadOnlyList<StampsCell> board)
    {
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(board);
        var state = board.ToArray();
        foreach (var stamp in StampCells(state))
        {
            foreach (var cell in RuledOut(size, regions, stamp))
            {
                if (state[cell] == StampsCell.Open)
                {
                    state[cell] = StampsCell.Out;
                }
            }
        }

        return OnlyCell(size, regions, state)
            ?? RegionsFill(size, regions, state, byRow: true)
            ?? RegionsFill(size, regions, state, byRow: false)
            ?? WouldLeaveNoRoom(size, regions, state);
    }

    /// <summary>The cells a stamp rules out: the rest of its row, column, and region, and the 8 cells around it.</summary>
    public static IReadOnlyList<int> RuledOut(int size, IReadOnlyList<int> regions, int stamp)
    {
        ArgumentNullException.ThrowIfNull(regions);
        var (row, column) = (stamp / size, stamp % size);
        var cells = new List<int>();
        for (var cell = 0; cell < size * size; cell++)
        {
            if (cell != stamp && (cell / size == row || cell % size == column || regions[cell] == regions[stamp] || Touch(size, cell, stamp)))
            {
                cells.Add(cell);
            }
        }

        return cells;
    }

    /// <summary>True when the stamps are a solution: one in each row, column, and region, and none touching.</summary>
    public static bool IsSolution(int size, IReadOnlyList<int> regions, IReadOnlyList<int> stamps)
    {
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(stamps);
        if (stamps.Count != size || stamps.Any(cell => cell < 0 || cell >= size * size))
        {
            return false;
        }

        var rows = stamps.Select(cell => cell / size).Distinct().Count();
        var columns = stamps.Select(cell => cell % size).Distinct().Count();
        var inRegions = stamps.Select(cell => regions[cell]).Distinct().Count();
        var touching = stamps.Any(a => stamps.Any(b => a != b && Touch(size, a, b)));
        return rows == size && columns == size && inRegions == size && !touching;
    }

    /// <summary>True when two different cells share a side or a corner.</summary>
    public static bool Touch(int size, int a, int b) =>
        a != b && Math.Abs((a / size) - (b / size)) <= 1 && Math.Abs((a % size) - (b % size)) <= 1;

    private static List<int> StampCells(StampsCell[] state) =>
        [.. Enumerable.Range(0, state.Length).Where(cell => state[cell] == StampsCell.Stamp)];

    /// <summary>Rule 1: a row, column, or region with no stamp and one open cell.</summary>
    private static StampsStep? OnlyCell(int size, IReadOnlyList<int> regions, StampsCell[] state)
    {
        var units = new (StampsRule Rule, Func<int, int> Of, string Reason)[]
        {
            (StampsRule.OnlyCellInRow, cell => cell / size, "Only one open cell is left in this row."),
            (StampsRule.OnlyCellInColumn, cell => cell % size, "Only one open cell is left in this column."),
            (StampsRule.OnlyCellInRegion, cell => regions[cell], "Only one open cell is left in this trip."),
        };
        foreach (var (rule, of, reason) in units)
        {
            for (var unit = 0; unit < size; unit++)
            {
                var cells = Enumerable.Range(0, size * size).Where(cell => of(cell) == unit).ToList();
                if (cells.Any(cell => state[cell] == StampsCell.Stamp))
                {
                    continue;
                }

                var open = cells.Where(cell => state[cell] == StampsCell.Open).ToList();
                if (open.Count == 1)
                {
                    return new StampsStep(open[0], true, rule, reason);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Rules 3 and 4: when k regions with no stamp have all their open cells in the same k rows (or
    /// columns), the other open cells of those rows (or columns) are ruled out. k goes up to 3.
    /// </summary>
    private static StampsStep? RegionsFill(int size, IReadOnlyList<int> regions, StampsCell[] state, bool byRow)
    {
        var lines = new int[size];
        var unstamped = new List<int>();
        for (var region = 0; region < size; region++)
        {
            var cells = Enumerable.Range(0, size * size).Where(cell => regions[cell] == region).ToList();
            if (cells.Any(cell => state[cell] == StampsCell.Stamp))
            {
                continue;
            }

            foreach (var cell in cells.Where(cell => state[cell] == StampsCell.Open))
            {
                lines[region] |= 1 << (byRow ? cell / size : cell % size);
            }

            if (lines[region] != 0)
            {
                unstamped.Add(region);
            }
        }

        for (var k = 1; k <= Math.Min(3, unstamped.Count); k++)
        {
            foreach (var group in Combinations(unstamped, k))
            {
                var mask = group.Aggregate(0, (all, region) => all | lines[region]);
                if (BitOperations.PopCount((uint)mask) != k)
                {
                    continue;
                }

                for (var cell = 0; cell < size * size; cell++)
                {
                    var line = byRow ? cell / size : cell % size;
                    if (state[cell] == StampsCell.Open && (mask & (1 << line)) != 0 && !group.Contains(regions[cell]))
                    {
                        var lineWord = byRow ? "row" : "column";
                        var reason = k == 1
                            ? $"This trip's open cells are all in one {lineWord}, so the rest of that {lineWord} is ruled out."
                            : $"These {k} trips fit only in these {k} {lineWord}s, so the rest of those {lineWord}s is ruled out.";
                        return new StampsStep(cell, false, byRow ? StampsRule.RegionsFillRows : StampsRule.RegionsFillColumns, reason);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>Rule 5: a cell is ruled out when a stamp there would leave a row, column, or region with no open cell.</summary>
    private static StampsStep? WouldLeaveNoRoom(int size, IReadOnlyList<int> regions, StampsCell[] state)
    {
        var units = new Func<int, int>[] { cell => cell / size, cell => cell % size, cell => regions[cell] };
        for (var candidate = 0; candidate < size * size; candidate++)
        {
            if (state[candidate] != StampsCell.Open)
            {
                continue;
            }

            var gone = new HashSet<int>(RuledOut(size, regions, candidate));
            foreach (var of in units)
            {
                for (var unit = 0; unit < size; unit++)
                {
                    if (unit == of(candidate))
                    {
                        continue;
                    }

                    var cells = Enumerable.Range(0, size * size).Where(cell => of(cell) == unit).ToList();
                    if (cells.Any(cell => state[cell] == StampsCell.Stamp))
                    {
                        continue;
                    }

                    if (cells.All(cell => state[cell] != StampsCell.Open || gone.Contains(cell)))
                    {
                        return new StampsStep(candidate, false, StampsRule.WouldLeaveNoRoom, "A stamp here would leave no room in another row, column, or trip.");
                    }
                }
            }
        }

        return null;
    }

    private static IEnumerable<List<int>> Combinations(List<int> items, int k)
    {
        if (k == 0)
        {
            yield return [];
            yield break;
        }

        for (var i = 0; i <= items.Count - k; i++)
        {
            foreach (var rest in Combinations(items[(i + 1)..], k - 1))
            {
                yield return [items[i], .. rest];
            }
        }
    }

    private static IEnumerable<int> Sides(int size, int cell)
    {
        var (row, column) = (cell / size, cell % size);
        if (row > 0)
        {
            yield return cell - size;
        }

        if (row < size - 1)
        {
            yield return cell + size;
        }

        if (column > 0)
        {
            yield return cell - 1;
        }

        if (column < size - 1)
        {
            yield return cell + 1;
        }
    }

    private static int[] Renumber(int[] regions)
    {
        var names = new Dictionary<int, int>();
        foreach (var region in regions)
        {
            names.TryAdd(region, names.Count);
        }

        return [.. regions.Select(region => names[region])];
    }
}
