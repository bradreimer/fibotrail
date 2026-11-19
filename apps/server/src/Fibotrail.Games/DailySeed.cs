using System.Globalization;
using System.Text;

namespace Fibotrail.Games;

/// <summary>
/// The seed of a game's daily puzzle. The same game and date always give the same seed, on every
/// machine and run, so the same day gives the same puzzle. It's a 64-bit FNV-1a hash of
/// "game:yyyy-MM-dd". <see cref="string.GetHashCode()"/> changes every run, so it isn't used.
/// </summary>
public static class DailySeed
{
    private const ulong OffsetBasis = 14695981039346656037;
    private const ulong Prime = 1099511628211;

    /// <summary>The first day of games, puzzle #1.</summary>
    public static readonly DateOnly FirstDay = new(2026, 11, 19);

    public static ulong For(string game, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(game);
        var hash = OffsetBasis;
        foreach (var value in Encoding.UTF8.GetBytes($"{game}:{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"))
        {
            hash ^= value;
            hash *= Prime;
        }

        return hash;
    }

    /// <summary>The puzzle number: 1 on <see cref="FirstDay"/>, 2 the day after, and so on.</summary>
    public static int PuzzleNumber(DateOnly date) => date.DayNumber - FirstDay.DayNumber + 1;
}

/// <summary>
/// A small random number generator with a fixed algorithm, SplitMix64, so a seed gives the same
/// numbers on every .NET version.
/// </summary>
public sealed class SeededRandom(ulong seed)
{
    private ulong _state = seed;

    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
        return z ^ (z >> 31);
    }

    /// <summary>A number from 0 up to, but not including, <paramref name="count"/>.</summary>
    public int Next(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        return (int)(NextUInt64() % (ulong)count);
    }

    /// <summary>A shuffled copy, by Fisher–Yates.</summary>
    public List<T> Shuffle<T>(IEnumerable<T> items)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
}
