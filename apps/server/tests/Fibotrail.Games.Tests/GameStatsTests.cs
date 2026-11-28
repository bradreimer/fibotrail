namespace Fibotrail.Games.Tests;

/// <summary>Game stats and streaks. See docs/specs/games.md#results-and-stats.</summary>
public class GameStatsTests
{
    private static readonly DateOnly Today = new(2026, 12, 10);

    private static GameDay Day(int daysAgo, bool solved = true, int tries = 2) => new(Today.AddDays(-daysAgo), solved, tries);

    [Fact]
    public void No_days_is_all_zero()
    {
        var stats = GameStats.From([], Today, 4);

        Assert.Equal((0, 0, 0, 0), (stats.Played, stats.WinPercent, stats.CurrentStreak, stats.BestStreak));
        Assert.Equal([0, 0, 0, 0], stats.Tries);
    }

    [Fact]
    public void Win_percent_is_rounded()
    {
        var stats = GameStats.From([Day(0), Day(1), Day(2, solved: false)], Today, 4);

        Assert.Equal((3, 67), (stats.Played, stats.WinPercent));
    }

    [Fact]
    public void The_current_streak_ends_today()
    {
        var stats = GameStats.From([Day(0), Day(1), Day(2), Day(4)], Today, 4);

        Assert.Equal((3, 3), (stats.CurrentStreak, stats.BestStreak));
    }

    [Fact]
    public void Today_not_played_yet_keeps_yesterdays_streak()
    {
        var stats = GameStats.From([Day(1), Day(2)], Today, 4);

        Assert.Equal(2, stats.CurrentStreak);
    }

    [Fact]
    public void A_missed_or_lost_day_ends_the_streak()
    {
        Assert.Equal(0, GameStats.From([Day(2), Day(3)], Today, 4).CurrentStreak);
        Assert.Equal(0, GameStats.From([Day(0, solved: false), Day(1)], Today, 4).CurrentStreak);
    }

    [Fact]
    public void The_best_streak_can_be_in_the_past()
    {
        var stats = GameStats.From([Day(0), Day(5), Day(6), Day(7), Day(8, solved: false), Day(9)], Today, 4);

        Assert.Equal((1, 3), (stats.CurrentStreak, stats.BestStreak));
    }

    [Fact]
    public void Tries_count_only_solved_days()
    {
        var stats = GameStats.From([Day(0, tries: 1), Day(1, tries: 3), Day(2, tries: 3), Day(3, solved: false, tries: 4)], Today, 4);

        Assert.Equal([1, 0, 2, 0], stats.Tries);
    }

    [Fact]
    public void A_game_without_a_clock_has_no_times()
    {
        Assert.Null(GameStats.From([Day(0)], Today, 4).Times);
    }

    [Fact]
    public void A_timed_game_has_the_best_and_average_time_of_each_label()
    {
        GameDay Timed(int daysAgo, int seconds, string label, bool solved = true) => new(Today.AddDays(-daysAgo), solved, 1, seconds, label);

        var stats = GameStats.From(
            [Timed(0, 100, "7 × 7"), Timed(1, 61, "6 × 6"), Timed(2, 200, "7 × 7"), Timed(3, 151, "7 × 7"), Timed(4, 10, "6 × 6", solved: false), new GameDay(Today.AddDays(-5), true, 1)],
            Today,
            1,
            timed: true);

        Assert.Equal([new GameTime("6 × 6", 1, 61, 61), new GameTime("7 × 7", 3, 100, 150)], stats.Times);
    }
}
