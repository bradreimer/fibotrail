namespace Fibotrail.Games;

/// <summary>A mark on one position of a try.</summary>
public enum Mark
{
    /// <summary>In its place. 🟩</summary>
    Right,

    /// <summary>Close to its place, by the game's own rule. 🟨</summary>
    Close,

    /// <summary>Not close. ⬜</summary>
    Wrong,

    /// <summary>Out of place, and it goes earlier in the order. ⬆️</summary>
    Earlier,

    /// <summary>Out of place, and it goes later in the order. ⬇️</summary>
    Later,
}

/// <summary>One played day of a game, as the stats read it.</summary>
/// <param name="Seconds">How long a timed game took, or null for a game without a clock.</param>
/// <param name="TimeLabel">What the time stats are split by, such as a board size, or null for no split.</param>
public sealed record GameDay(DateOnly Date, bool Solved, int Tries, int? Seconds = null, string? TimeLabel = null);

/// <summary>The times of a timed game's solved days with one label. See docs/specs/games.md#timed-games.</summary>
/// <param name="Label">What these days share, such as "7 × 7", or an empty text when the game doesn't split its times.</param>
/// <param name="Days">Solved days with a time.</param>
/// <param name="Best">The shortest time, in seconds.</param>
/// <param name="Average">The mean time, in whole seconds.</param>
public sealed record GameTime(string Label, int Days, int Best, int Average);

/// <summary>A game's stats, for the stats panel and the game's card. See docs/specs/games.md.</summary>
/// <param name="Played">Days with a result.</param>
/// <param name="WinPercent">Solved days out of played days, as a whole percentage.</param>
/// <param name="CurrentStreak">Solved days in a row that end today, or yesterday when today isn't played.</param>
/// <param name="BestStreak">The longest run of solved days in a row.</param>
/// <param name="Tries">For 1 try, 2 tries, and so on, how many solved days used that many.</param>
/// <param name="Times">For a timed game, the best and average time of each label, in label order. Null for other games.</param>
public sealed record GameStats(int Played, int WinPercent, int CurrentStreak, int BestStreak, IReadOnlyList<int> Tries, IReadOnlyList<GameTime>? Times = null)
{
    /// <summary>Works out the stats from a game's played days.</summary>
    /// <param name="timed">True for a game played against a clock, which adds <see cref="Times"/>.</param>
    public static GameStats From(IEnumerable<GameDay> days, DateOnly today, int maxTries, bool timed = false)
    {
        ArgumentNullException.ThrowIfNull(days);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTries);

        var byDate = days.GroupBy(day => day.Date).ToDictionary(group => group.Key, group => group.First());
        var played = byDate.Count;
        var wins = byDate.Values.Count(day => day.Solved);
        var tries = new int[maxTries];
        foreach (var day in byDate.Values.Where(day => day.Solved && day.Tries >= 1 && day.Tries <= maxTries))
        {
            tries[day.Tries - 1]++;
        }

        var best = 0;
        var run = 0;
        DateOnly? previous = null;
        foreach (var day in byDate.Values.OrderBy(day => day.Date))
        {
            run = day.Solved ? (previous is { } before && before.AddDays(1) == day.Date ? run + 1 : 1) : 0;
            best = Math.Max(best, run);
            previous = day.Solved ? day.Date : null;
        }

        // Today not played yet doesn't break the streak; it ends yesterday.
        var current = 0;
        var cursor = byDate.ContainsKey(today) ? today : today.AddDays(-1);
        while (byDate.TryGetValue(cursor, out var day) && day.Solved)
        {
            current++;
            cursor = cursor.AddDays(-1);
        }

        var percent = played == 0 ? 0 : (int)Math.Round(100.0 * wins / played, MidpointRounding.AwayFromZero);
        IReadOnlyList<GameTime>? times = timed
            ? [.. byDate.Values
                .Where(day => day.Solved && day.Seconds is not null)
                .GroupBy(day => day.TimeLabel ?? string.Empty)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new GameTime(
                    group.Key,
                    group.Count(),
                    group.Min(day => day.Seconds!.Value),
                    (int)Math.Round(group.Average(day => day.Seconds!.Value), MidpointRounding.AwayFromZero)))]
            : null;
        return new GameStats(played, percent, current, best, tries, times);
    }
}
