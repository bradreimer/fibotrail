using Fibotrail.Games.Stamps;

namespace Fibotrail.Games.Tests.Stamps;

/// <summary>The rules, generator, and solvers of a Stamps board. See docs/specs/stamps-game.md#the-puzzle.</summary>
public class StampsBoardTests
{
    /// <summary>A 5 by 5 board where each row is its own region, so it has many solutions.</summary>
    private static readonly int[] RowRegions =
    [
        0, 0, 0, 0, 0,
        1, 1, 1, 1, 1,
        2, 2, 2, 2, 2,
        3, 3, 3, 3, 3,
        4, 4, 4, 4, 4,
    ];

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Generated_boards_have_one_solution_that_logic_reaches(int size)
    {
        for (var day = 0; day < 365; day++)
        {
            var random = new SeededRandom(DailySeed.For("stamps", new DateOnly(2027, 1, 1).AddDays(day)));
            var layout = StampsBoard.Generate(random, size);

            Assert.NotNull(layout);
            Assert.Equal(size * size, layout.Regions.Count);
            Assert.Equal(size, layout.Regions.Distinct().Count());
            Assert.Equal(1, StampsBoard.CountSolutions(size, layout.Regions, 2));
            Assert.Equal(layout.Solution, StampsBoard.Solutions(size, layout.Regions, 1)[0]);
            Assert.True(StampsBoard.SolvesByLogic(size, layout.Regions));
            Assert.True(layout.Attempts <= 200, $"Day {day} took {layout.Attempts} attempts.");
        }
    }

    [Fact]
    public void The_same_seed_gives_the_same_board()
    {
        var first = StampsBoard.Generate(new SeededRandom(7), 8)!;
        var second = StampsBoard.Generate(new SeededRandom(7), 8)!;

        Assert.Equal(first.Regions, second.Regions);
        Assert.Equal(first.Solution, second.Solution);
    }

    [Fact]
    public void Regions_are_connected_and_numbered_in_reading_order()
    {
        var layout = StampsBoard.Generate(new SeededRandom(11), 7)!;
        var size = layout.Size;

        // Each region's first cell, row by row, comes in the order of its number.
        var firsts = Enumerable.Range(0, size).Select(region => layout.Regions.ToList().IndexOf(region)).ToList();
        Assert.Equal(firsts.Order(), firsts);

        for (var region = 0; region < size; region++)
        {
            var cells = Enumerable.Range(0, size * size).Where(cell => layout.Regions[cell] == region).ToHashSet();
            var seen = new HashSet<int> { cells.First() };
            var queue = new Queue<int>(seen);
            while (queue.TryDequeue(out var cell))
            {
                foreach (var next in new[] { cell - size, cell + size, cell % size > 0 ? cell - 1 : -1, cell % size < size - 1 ? cell + 1 : -1 })
                {
                    if (cells.Contains(next) && seen.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            Assert.Equal(cells.Count, seen.Count);
        }
    }

    [Fact]
    public void Placed_stamps_never_touch()
    {
        var random = new SeededRandom(3);
        for (var i = 0; i < 100; i++)
        {
            var columns = StampsBoard.PlaceStamps(random, 6);

            Assert.Equal(6, columns.Distinct().Count());
            for (var row = 1; row < 6; row++)
            {
                Assert.True(Math.Abs(columns[row] - columns[row - 1]) >= 2);
            }
        }
    }

    [Fact]
    public void A_solution_has_one_stamp_per_row_column_and_region_and_none_touching()
    {
        Assert.True(StampsBoard.IsSolution(5, RowRegions, [0, 7, 14, 16, 23]));

        // Two in one column.
        Assert.False(StampsBoard.IsSolution(5, RowRegions, [0, 7, 14, 15, 22]));

        // Touching at a corner: row 0 column 0 and row 1 column 1.
        Assert.False(StampsBoard.IsSolution(5, RowRegions, [0, 6, 14, 17, 23]));

        // Too few, off the board, or repeated.
        Assert.False(StampsBoard.IsSolution(5, RowRegions, [0, 7, 14, 16]));
        Assert.False(StampsBoard.IsSolution(5, RowRegions, [0, 7, 14, 16, 25]));
        Assert.False(StampsBoard.IsSolution(5, RowRegions, [0, 0, 14, 16, 23]));
    }

    [Fact]
    public void A_stamp_rules_out_its_row_column_region_and_neighbors()
    {
        var regions = new[]
        {
            0, 0, 1,
            2, 1, 1,
            2, 2, 1,
        };

        // The center cell, region 1: every other cell touches it.
        Assert.Equal(8, StampsBoard.RuledOut(3, regions, 4).Count);

        // The top-left cell, region 0: its row, its column, and the center.
        Assert.Equal([1, 2, 3, 4, 6], StampsBoard.RuledOut(3, regions, 0));
    }

    [Fact]
    public void Touching_means_sharing_a_side_or_a_corner()
    {
        Assert.True(StampsBoard.Touch(5, 0, 6));
        Assert.True(StampsBoard.Touch(5, 0, 1));
        Assert.False(StampsBoard.Touch(5, 0, 2));
        Assert.False(StampsBoard.Touch(5, 4, 5));
        Assert.False(StampsBoard.Touch(5, 3, 3));
    }

    [Fact]
    public void Rule_one_stamps_the_only_open_cell_of_a_region()
    {
        // Region 1 is the single cell in the middle of row 1.
        var regions = new[]
        {
            0, 0, 0, 0, 0,
            0, 0, 1, 0, 0,
            2, 2, 2, 2, 2,
            3, 3, 3, 3, 3,
            4, 4, 4, 4, 4,
        };

        var step = StampsBoard.NextStep(5, regions, new StampsCell[25]);

        Assert.Equal(new StampsStep(7, true, StampsRule.OnlyCellInRegion, "Only one open cell is left in this trip."), step);
    }

    [Fact]
    public void A_stamp_rules_out_cells_before_the_next_step()
    {
        var board = new StampsCell[25];
        board[0] = StampsCell.Stamp;

        var step = StampsBoard.NextStep(5, RowRegions, board);

        // Row 0 is stamped, so no step changes it, and row 1's open cells start at column 2.
        Assert.NotNull(step);
        Assert.True(step.Cell >= 5);
        Assert.NotEqual(StampsCell.Stamp, board[step.Cell]);
    }

    [Fact]
    public void Rule_three_rules_out_the_rest_of_a_row_that_holds_a_whole_region()
    {
        // Region 1 lies in row 0, columns 3 and 4, so the rest of row 0 can't hold a stamp.
        var regions = new[]
        {
            0, 0, 0, 1, 1,
            0, 0, 0, 0, 0,
            2, 2, 2, 2, 2,
            3, 3, 3, 3, 3,
            4, 4, 4, 4, 4,
        };

        var step = StampsBoard.NextStep(5, regions, new StampsCell[25]);

        Assert.NotNull(step);
        Assert.False(step.Stamp);
        Assert.Equal(StampsRule.RegionsFillRows, step.Rule);
        Assert.Equal(0, step.Cell);
    }

    [Fact]
    public void Logic_solves_a_board_by_its_rules_and_stops_when_it_is_stuck()
    {
        Assert.True(StampsBoard.SolvesByLogic(5, StampsBoard.Generate(new SeededRandom(1), 5)!.Regions));

        // Every row is its own region, so many solutions remain and no rule applies.
        Assert.False(StampsBoard.SolvesByLogic(5, RowRegions));
        Assert.Null(StampsBoard.NextStep(5, RowRegions, new StampsCell[25]));
    }
}
