namespace Fibotrail.Server.Immich;

/// <summary>The Immich work the server does once it has started.</summary>
public static partial class ImmichStartup
{
    /// <summary>
    /// Checks the Immich connection and writes the result to the log, then continues a scan that
    /// the last shutdown interrupted. The server starts either way: Settings shows the problem.
    /// </summary>
    public static async Task StartImmichAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        await using var scope = app.Services.CreateAsyncScope();
        var status = await scope.ServiceProvider.GetRequiredService<ImmichConnection>().CheckAsync(CancellationToken.None);
        if (status.Connected)
        {
            LogConnected(app.Logger, status.Version, status.Warning ?? "The version is one that Fibotrail was tested with.");
        }
        else
        {
            LogNotConnected(app.Logger, status.Message);
        }

        if (await app.Services.GetRequiredService<ScanService>().ResumeInterruptedAsync(CancellationToken.None))
        {
            LogResumed(app.Logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to Immich {Version}. {Note}")]
    private static partial void LogConnected(ILogger logger, string? version, string note);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message}")]
    private static partial void LogNotConnected(ILogger logger, string? message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Continuing the scan that was interrupted.")]
    private static partial void LogResumed(ILogger logger);
}
