namespace Fibotrail.Server.Data;

/// <summary>
/// Checks at startup that the data folder can be written to. A bind mount keeps the owner of the
/// host folder, so a folder owned by root fails for the container's non-root user. Without this
/// check the first sign is a SQLite stack trace that doesn't say what to fix.
/// </summary>
public static class DataFolderCheck
{
    /// <summary>Returns a plain message when the folder can't be written to, or null when it can.</summary>
    public static string? Verify(ServerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var folder = settings.DataDirectory;
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The ASP.NET Core image sets APP_UID. A run from source has none.
            var user = Environment.GetEnvironmentVariable("APP_UID");
            var owner = string.IsNullOrWhiteSpace(user) ? "the user that runs Fibotrail" : $"user {user}";
            var fix = string.IsNullOrWhiteSpace(user) ? string.Empty : $" On the host, run: chown {user}:{user} <the folder you mounted>.";
            var reason = exception is UnauthorizedAccessException ? "Permission denied." : exception.Message;
            return $"Fibotrail can't write to {folder}. {reason} The folder must be writable by {owner}.{fix}";
        }
    }
}
