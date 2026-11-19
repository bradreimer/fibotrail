using Fibotrail.Server.Collections;
using Fibotrail.Server.Auth;
using Fibotrail.Server.Cli;
using Fibotrail.Server.Data;
using Fibotrail.Server.Hosting;
using Fibotrail.Server.Immich;
using Fibotrail.Server.Mcp;
using Fibotrail.Server.Trips;

namespace Fibotrail.Server;

/// <summary>Decides what one run of the program does: serve, or run a command.</summary>
public static class EntryPoint
{
    /// <param name="args">The command line.</param>
    /// <param name="error">Where a startup problem is written. In the container, this is the log.</param>
    /// <param name="configure">Lets a test replace the configuration sources.</param>
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter error,
        Action<ConfigurationManager>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(error);

        // A first argument without a dash is a command. Anything else is a server option.
        var command = args.Length > 0 && !args[0].StartsWith('-') ? args[0] : null;
        var builder = WebApplication.CreateBuilder(command is null ? args : []);
        configure?.Invoke(builder.Configuration);

        switch (command)
        {
            case null:
                return await ServeAsync(builder, error);
            case ResetPasswordCommand.Name:
                return await ResetPasswordAsync(builder.Configuration, args[1..]);
            case ReplayCommand.Name:
                return await ReplayAsync(builder.Configuration, args[1..]);
            case StopsCommand.Name:
                return await StopsAsync(builder.Configuration, args[1..]);
            case PlacesCommand.Name:
                return await PlacesAsync(builder.Configuration, args[1..]);
            case RescanCommand.Name:
                return await RescanAsync(builder.Configuration, error, args[1..]);
            case JoinCommand.Name:
                return await JoinAsync(builder.Configuration, args[1..]);
            case TripCommand.Name:
                return await TripAsync(builder.Configuration, args[1..]);
            case HealthCheckCommand.Name:
                var port = builder.Configuration["ASPNETCORE_HTTP_PORTS"]?.Split(';')[0] ?? "8080";
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    return await HealthCheckCommand.RunAsync(client, new Uri($"http://127.0.0.1:{port}/healthz"));
                }

            default:
                await error.WriteLineAsync($"Fibotrail doesn't know the command \"{command}\". The commands are: {ResetPasswordCommand.Name}, {ReplayCommand.Name}, {StopsCommand.Name}, {PlacesCommand.Name}, {RescanCommand.Name}, {JoinCommand.Name}, {TripCommand.Name}, {HealthCheckCommand.Name}.");
                return 2;
        }
    }

    private static async Task<int> ServeAsync(WebApplicationBuilder builder, TextWriter error)
    {
        var (settings, problems) = ServerSettings.Load(builder.Configuration, requireImmich: true);
        if (settings is null)
        {
            foreach (var problem in problems)
            {
                await error.WriteLineAsync($"Fibotrail can't start: {problem}");
            }

            return 1;
        }

        var folderProblem = DataFolderCheck.Verify(settings);
        if (folderProblem is not null)
        {
            await error.WriteLineAsync(folderProblem);
            return 1;
        }

        // A bad collection file stops the app with the file's name. It is never skipped silently.
        try
        {
            builder.Services.AddSingleton(CollectionFiles.Load(CollectionFiles.DefaultDirectory));
        }
        catch (CollectionFileException exception)
        {
            await error.WriteLineAsync($"Fibotrail can't start: {exception.Message}");
            return 1;
        }

        var app = ServerApp.Create(builder, settings);
        await app.PrepareDatabaseAsync();
        await app.StartAsync();
        app.LogMcpState();
        await app.StartImmichAsync();
        await app.WaitForShutdownAsync();
        return 0;
    }

    private static async Task<int> ResetPasswordAsync(IConfiguration configuration, string[] arguments)
    {
        // The command needs the database only, so the Immich variables aren't required here.
        var (settings, _) = ServerSettings.Load(configuration, requireImmich: false);
        var services = new ServiceCollection();
        services.AddFibotrailCore(settings!);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        return await ResetPasswordCommand.RunAsync(
            arguments,
            new SystemConsoleIo(),
            scope.ServiceProvider.GetRequiredService<AuthService>(),
            settings!);
    }

    private static async Task<int> ReplayAsync(IConfiguration configuration, string[] arguments)
    {
        // The command reads the database only, so the Immich variables aren't required here.
        var (settings, _) = ServerSettings.Load(configuration, requireImmich: false);
        var services = new ServiceCollection();
        services.AddFibotrailCore(settings!);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        return await ReplayCommand.RunAsync(
            arguments,
            new SystemConsoleIo(),
            scope.ServiceProvider.GetRequiredService<FibotrailDbContext>(),
            scope.ServiceProvider.GetRequiredService<HomeLocation>(),
            settings!);
    }

    private static async Task<int> StopsAsync(IConfiguration configuration, string[] arguments)
    {
        // The command reads the database only, so the Immich variables aren't required here.
        var (settings, _) = ServerSettings.Load(configuration, requireImmich: false);
        var services = new ServiceCollection();
        services.AddFibotrailCore(settings!);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        return await StopsCommand.RunAsync(
            arguments,
            new SystemConsoleIo(),
            scope.ServiceProvider.GetRequiredService<FibotrailDbContext>(),
            settings!);
    }

    private static async Task<int> RescanAsync(IConfiguration configuration, TextWriter error, string[] arguments)
    {
        // Unlike the other commands, this one reads from Immich, so the Immich variables are required.
        var (settings, problems) = ServerSettings.Load(configuration, requireImmich: true);
        if (settings is null)
        {
            foreach (var problem in problems)
            {
                await error.WriteLineAsync($"Fibotrail can't start: {problem}");
            }

            return 1;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFibotrailCore(settings);
        services.AddFibotrailImmich(settings);
        services.AddSingleton<IHostApplicationLifetime, CommandLifetime>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        // Ctrl+C stops the scan after the page in hand and saves its place.
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stop.Cancel();
        };

        return await RescanCommand.RunAsync(
            arguments,
            new SystemConsoleIo(),
            scope.ServiceProvider.GetRequiredService<FibotrailDbContext>(),
            provider.GetRequiredService<ScanService>(),
            settings,
            stop.Token);
    }

    private static async Task<int> JoinAsync(IConfiguration configuration, string[] arguments)
    {
        // The command reads and writes the database only, so the Immich variables aren't required here.
        var (settings, _) = ServerSettings.Load(configuration, requireImmich: false);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFibotrailCore(settings!);
        services.AddScoped<DetectionService>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        return await JoinCommand.RunAsync(
            arguments,
            new SystemConsoleIo(),
            scope.ServiceProvider.GetRequiredService<FibotrailDbContext>(),
            scope.ServiceProvider.GetRequiredService<DetectionService>(),
            settings!,
            provider.GetRequiredService<TimeProvider>());
    }

    private static async Task<int> TripAsync(IConfiguration configuration, string[] arguments)
    {
        // The command reads and writes the database only, so the Immich variables aren't required here.
        var (settings, _) = ServerSettings.Load(configuration, requireImmich: false);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFibotrailCore(settings!);
        services.AddScoped<DetectionService>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        return await TripCommand.RunAsync(
            arguments,
            new SystemConsoleIo(),
            scope.ServiceProvider.GetRequiredService<FibotrailDbContext>(),
            scope.ServiceProvider.GetRequiredService<DetectionService>(),
            settings!,
            provider.GetRequiredService<TimeProvider>());
    }

    private static async Task<int> PlacesAsync(IConfiguration configuration, string[] arguments)
    {
        var (settings, _) = ServerSettings.Load(configuration, requireImmich: false);
        var services = new ServiceCollection();
        services.AddFibotrailCore(settings!);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var catalog = CollectionFiles.Load(CollectionFiles.DefaultDirectory);
        return await PlacesCommand.RunAsync(
            arguments,
            new SystemConsoleIo(),
            scope.ServiceProvider.GetRequiredService<FibotrailDbContext>(),
            settings!,
            catalog,
            Fibotrail.Places.CountryOutlines.Load());
    }
}
