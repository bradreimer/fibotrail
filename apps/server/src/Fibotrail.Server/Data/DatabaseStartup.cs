using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Data;

/// <summary>Brings the database file up to date when the server starts.</summary>
public static partial class DatabaseStartup
{
    /// <summary>Brings the server's database up to date, and writes to the log when it saved a copy first.</summary>
    public static async Task PrepareDatabaseAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FibotrailDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ServerSettings>();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var backup = await PrepareAsync(db, settings, time);
        if (backup is not null)
        {
            LogBackup(app.Logger, backup);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved a copy of the database at {Path} before updating it.")]
    private static partial void LogBackup(ILogger logger, string path);

    /// <summary>
    /// Applies pending migrations and turns on WAL mode. Before a migration changes a database
    /// that already has data, it saves a copy of the file next to it.
    /// </summary>
    /// <returns>The path of the copy, or null when no copy was needed.</returns>
    public static async Task<string?> PrepareAsync(
        FibotrailDbContext db,
        ServerSettings settings,
        TimeProvider time,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(settings.DataDirectory);
        var backupPath = await SaveCopyIfMigrationPendingAsync(db, settings, time, cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        return backupPath;
    }

    /// <summary>
    /// Saves a copy of the database when a migration is about to change one that was migrated
    /// before. A new database needs no copy.
    /// </summary>
    /// <returns>The path of the copy, or null when no copy was needed.</returns>
    public static async Task<string?> SaveCopyIfMigrationPendingAsync(
        FibotrailDbContext db,
        ServerSettings settings,
        TimeProvider time,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settings.DatabasePath))
        {
            return null;
        }

        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
        return pending.Any() && applied.Any() ? Backup(settings, time) : null;
    }

    private static string Backup(ServerSettings settings, TimeProvider time)
    {
        var stamp = time.GetUtcNow().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var path = Path.Combine(settings.DataDirectory, $"fibotrail-backup-{stamp}.db");

        // The SQLite backup API copies a consistent snapshot, including what is still in the WAL file.
        using var source = new SqliteConnection(settings.ConnectionString);
        using var target = new SqliteConnection($"Data Source={path};Pooling=False");
        source.Open();
        target.Open();
        source.BackupDatabase(target);
        return path;
    }
}
