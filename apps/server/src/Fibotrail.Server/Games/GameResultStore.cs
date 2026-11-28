using Fibotrail.Games;
using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Games;

/// <summary>Reads and writes the <c>game_result</c> table: one graded result per game per day.</summary>
public static class GameResultStore
{
    public static Task<GameResult?> FindAsync(FibotrailDbContext db, string game, DateOnly date, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.GameResults.AsNoTracking().FirstOrDefaultAsync(result => result.Game == game && result.PuzzleDate == date, cancellationToken);
    }

    /// <summary>A game's stats, as of the player's today.</summary>
    public static Task<GameStats> StatsAsync(FibotrailDbContext db, string game, int maxTries, DateOnly today, CancellationToken cancellationToken) =>
        StatsAsync(db, game, maxTries, today, null, cancellationToken);

    /// <summary>
    /// A game's stats, as of the player's today. A timed game passes <paramref name="timeLabel"/>,
    /// which reads the label its times are split by from a result's marks.
    /// </summary>
    public static async Task<GameStats> StatsAsync(FibotrailDbContext db, string game, int maxTries, DateOnly today, Func<string, string?>? timeLabel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var rows = await db.GameResults.AsNoTracking()
            .Where(result => result.Game == game)
            .Select(result => new { result.PuzzleDate, result.Solved, result.Tries, result.Seconds, result.Marks })
            .ToListAsync(cancellationToken);
        var days = rows.Select(row => new GameDay(row.PuzzleDate, row.Solved, row.Tries, row.Seconds, timeLabel?.Invoke(row.Marks)));
        return GameStats.From(days, today, maxTries, timed: timeLabel is not null);
    }

    /// <summary>
    /// Stores a day's result, unless that day already has one. Either way it answers with the stored
    /// result, so a second send never changes the first.
    /// </summary>
    public static async Task<GameResult> AddOnceAsync(FibotrailDbContext db, GameResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(result);
        if (await FindAsync(db, result.Game, result.PuzzleDate, cancellationToken) is { } stored)
        {
            return stored;
        }

        db.GameResults.Add(result);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException)
        {
            // Two sends at once: the unique index kept the first.
            db.ChangeTracker.Clear();
            return await FindAsync(db, result.Game, result.PuzzleDate, cancellationToken) ?? throw new InvalidOperationException("The game result wasn't stored.");
        }
    }
}
