namespace Fibotrail.Games.Tests;

/// <summary>The daily seed, the puzzle number, and marks. See docs/specs/games.md.</summary>
public class SeedAndMarksTests
{
    [Fact]
    public void The_seed_is_FNV_1a_of_the_game_and_the_date()
    {
        var date = new DateOnly(2026, 11, 19);

        // FNV-1a 64 of "timeline:2026-11-19". A change here means every past puzzle changed.
        Assert.Equal(0x6EDC87B45C04EA80UL, DailySeed.For("timeline", date));
        Assert.NotEqual(DailySeed.For("timeline", date), DailySeed.For("timeline", date.AddDays(1)));
        Assert.NotEqual(DailySeed.For("timeline", date), DailySeed.For("other", date));
    }

    [Fact]
    public void The_generator_is_SplitMix64()
    {
        // The first output of SplitMix64 with seed 0, from the reference implementation.
        Assert.Equal(0xE220A8397B1DCDAFUL, new SeededRandom(0).NextUInt64());
    }

    [Fact]
    public void Puzzle_1_is_the_first_day()
    {
        Assert.Equal(1, DailySeed.PuzzleNumber(new DateOnly(2026, 11, 19)));
        Assert.Equal(43, DailySeed.PuzzleNumber(new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Marks_round_trip_as_text()
    {
        IReadOnlyList<IReadOnlyList<Mark>> rows = [[Mark.Close, Mark.Right, Mark.Wrong], [Mark.Right, Mark.Right, Mark.Right]];

        var text = Marks.Encode(rows);

        Assert.Equal("CRW,RRR", text);
        Assert.Equal(rows, Marks.Decode(text));
    }

    [Fact]
    public void The_share_text_has_squares_and_no_names()
    {
        IReadOnlyList<IReadOnlyList<Mark>> rows = [[Mark.Close, Mark.Right, Mark.Wrong], [Mark.Right, Mark.Right, Mark.Right]];

        Assert.Equal("Fibotrail Timeline #7 2/4\n🟨🟩⬜\n🟩🟩🟩", Marks.ShareText("Timeline", 7, true, rows, 4));
        Assert.StartsWith("Fibotrail Timeline #7 X/4\n", Marks.ShareText("Timeline", 7, false, rows, 4), StringComparison.Ordinal);
    }

    [Fact]
    public void Arrow_marks_round_trip_and_share_with_the_variant()
    {
        IReadOnlyList<IReadOnlyList<Mark>> rows = [[Mark.Earlier, Mark.Right, Mark.Later], [Mark.Right, Mark.Right, Mark.Right]];

        Assert.Equal("ERL,RRR", Marks.Encode(rows));
        Assert.Equal(rows, Marks.Decode("ERL,RRR"));
        Assert.Equal("Fibotrail Timeline #7 Arrows 2/4\n⬆️🟩⬇️\n🟩🟩🟩", Marks.ShareText("Timeline", 7, true, rows, 4, "Arrows"));
    }
}
