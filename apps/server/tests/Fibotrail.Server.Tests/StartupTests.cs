using Fibotrail.Server.Cli;
using Microsoft.AspNetCore.TestHost;
using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Fibotrail.Server.Tests;

public class ServerSettingsTests
{
    private static IConfiguration Configuration(params (string Name, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Name, pair => pair.Value))
            .Build();

    [Fact]
    public void Complete_settings_load()
    {
        var (settings, problems) = ServerSettings.Load(
            Configuration(("IMMICH_URL", "http://immich_server:2283"), ("IMMICH_API_KEY", "abc")),
            requireImmich: true);

        Assert.Empty(problems);
        Assert.Equal(new Uri("http://immich_server:2283"), settings!.ImmichUrl);
        Assert.Equal("/data", settings.DataDirectory);
        Assert.Equal("/data/fibotrail.db", settings.DatabasePath);
    }

    [Theory]
    [InlineData("IMMICH_URL")]
    [InlineData("IMMICH_API_KEY")]
    public void A_missing_required_variable_is_named(string missing)
    {
        var values = new[] { ("IMMICH_URL", "http://immich_server:2283"), ("IMMICH_API_KEY", "abc") }
            .Where(pair => pair.Item1 != missing)
            .Select(pair => (pair.Item1, (string?)pair.Item2))
            .ToArray();

        var (settings, problems) = ServerSettings.Load(Configuration(values), requireImmich: true);

        Assert.Null(settings);
        Assert.Contains(missing, Assert.Single(problems), StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_key_counts_as_missing()
    {
        var (settings, problems) = ServerSettings.Load(
            Configuration(("IMMICH_URL", "http://immich_server:2283"), ("IMMICH_API_KEY", " ")),
            requireImmich: true);

        Assert.Null(settings);
        Assert.Contains("IMMICH_API_KEY", Assert.Single(problems), StringComparison.Ordinal);
    }

    [Fact]
    public void The_optional_immich_settings_have_defaults()
    {
        var (settings, _) = ServerSettings.Load(
            Configuration(("IMMICH_URL", "http://immich_server:2283"), ("IMMICH_API_KEY", "abc")),
            requireImmich: true);

        Assert.Equal(TimeSpan.FromSeconds(30), settings!.ImmichTimeout);
        Assert.Null(settings.ImmichExternalUrl);
        Assert.Equal("/data/thumbnails", settings.ThumbnailDirectory);
    }

    [Fact]
    public void The_optional_immich_settings_are_read()
    {
        var (settings, problems) = ServerSettings.Load(
            Configuration(
                ("IMMICH_URL", "http://immich_server:2283"),
                ("IMMICH_API_KEY", "abc"),
                ("IMMICH_EXTERNAL_URL", "https://photos.example.com"),
                ("IMMICH_TIMEOUT_SECONDS", "120")),
            requireImmich: true);

        Assert.Empty(problems);
        Assert.Equal(TimeSpan.FromSeconds(120), settings!.ImmichTimeout);
        Assert.Equal(new Uri("https://photos.example.com"), settings.ImmichExternalUrl);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("601")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("thirty")]
    public void A_timeout_that_isnt_a_whole_number_from_1_to_600_is_named(string value)
    {
        var (settings, problems) = ServerSettings.Load(
            Configuration(("IMMICH_URL", "http://immich_server:2283"), ("IMMICH_API_KEY", "abc"), ("IMMICH_TIMEOUT_SECONDS", value)),
            requireImmich: true);

        Assert.Null(settings);
        Assert.Contains("IMMICH_TIMEOUT_SECONDS", Assert.Single(problems), StringComparison.Ordinal);
    }

    [Fact]
    public void An_external_address_that_isnt_a_web_address_is_named()
    {
        var (settings, problems) = ServerSettings.Load(
            Configuration(("IMMICH_URL", "http://immich_server:2283"), ("IMMICH_API_KEY", "abc"), ("IMMICH_EXTERNAL_URL", "photos")),
            requireImmich: true);

        Assert.Null(settings);
        Assert.Contains("IMMICH_EXTERNAL_URL", Assert.Single(problems), StringComparison.Ordinal);
    }

    [Fact]
    public void An_address_that_is_not_a_web_address_is_named()
    {
        var (settings, problems) = ServerSettings.Load(
            Configuration(("IMMICH_URL", "immich_server"), ("IMMICH_API_KEY", "abc")),
            requireImmich: true);

        Assert.Null(settings);
        Assert.Contains("IMMICH_URL", Assert.Single(problems), StringComparison.Ordinal);
    }

    [Fact]
    public void The_reset_command_does_not_need_the_immich_variables()
    {
        var (settings, problems) = ServerSettings.Load(Configuration(("FIBOTRAIL_DATA_DIR", "/tmp/x")), requireImmich: false);

        Assert.Empty(problems);
        Assert.Equal("/tmp/x", settings!.DataDirectory);
    }

    [Fact]
    public void The_text_form_of_the_settings_leaves_out_the_key()
    {
        var settings = new ServerSettings("/data", new Uri("http://immich_server:2283"), "secret-key-value");

        Assert.DoesNotContain("secret-key-value", settings.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_server_exits_and_names_the_variable_when_the_key_is_missing()
    {
        using var error = new StringWriter();

        var exitCode = await EntryPoint.RunAsync([], error, configuration =>
        {
            configuration.Sources.Clear();
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["IMMICH_URL"] = "http://immich_server:2283" });
        });

        Assert.Equal(1, exitCode);
        Assert.Contains("IMMICH_API_KEY", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_data_folder_that_can_be_written_to_passes_the_check()
    {
        var folder = Directory.CreateTempSubdirectory("fibotrail-data-").FullName;
        var settings = new ServerSettings(folder, new Uri("http://immich_server:2283"), "abc");

        Assert.Null(DataFolderCheck.Verify(settings));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
    }

    [Fact]
    public async Task A_data_folder_that_cannot_be_used_stops_the_server_with_one_plain_line()
    {
        // A file where the folder should be fails on every platform and for every user, root included.
        var blocker = Path.Combine(Directory.CreateTempSubdirectory("fibotrail-data-").FullName, "not-a-folder");
        await File.WriteAllTextAsync(blocker, "x");
        using var error = new StringWriter();

        var exitCode = await EntryPoint.RunAsync([], error, configuration =>
        {
            configuration.Sources.Clear();
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IMMICH_URL"] = "http://immich_server:2283",
                ["IMMICH_API_KEY"] = "abc",
                ["FIBOTRAIL_DATA_DIR"] = blocker,
            });
        });

        var text = error.ToString();
        Assert.Equal(1, exitCode);
        Assert.Contains($"Fibotrail can't write to {blocker}", text, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_command_is_refused()
    {
        using var error = new StringWriter();

        var exitCode = await EntryPoint.RunAsync(["make-coffee"], error);

        Assert.Equal(2, exitCode);
        Assert.Contains("make-coffee", error.ToString(), StringComparison.Ordinal);
    }
}

public class DatabaseTests
{
    [Fact]
    public async Task The_database_is_one_file_in_the_data_folder_in_wal_mode()
    {
        await using var app = await TestApp.StartAsync();

        var mode = await app.WithDbAsync(db => db.Database.SqlQueryRaw<string>("PRAGMA journal_mode").ToListAsync());

        Assert.True(File.Exists(app.Settings.DatabasePath));
        Assert.Equal("wal", Assert.Single(mode));
    }

    [Fact]
    public async Task A_new_or_current_database_needs_no_copy()
    {
        await using var app = await TestApp.StartAsync();

        var copy = await app.WithDbAsync(db => DatabaseStartup.PrepareAsync(db, app.Settings, app.Time));

        Assert.Null(copy);
        Assert.Empty(Directory.GetFiles(app.Settings.DataDirectory, "fibotrail-backup-*"));
    }

    [Fact]
    public async Task A_copy_is_saved_before_a_migration_changes_an_existing_database()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();

        // Make the database look like one from an older version: it has a migration history, and
        // the newest migration isn't in it.
        var copy = await app.WithDbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM __EFMigrationsHistory WHERE MigrationId = (SELECT MAX(MigrationId) FROM __EFMigrationsHistory)");
            return await DatabaseStartup.SaveCopyIfMigrationPendingAsync(db, app.Settings, app.Time);
        });

        Assert.NotNull(copy);
        Assert.Equal(Path.Combine(app.Settings.DataDirectory, "fibotrail-backup-20261115T120000Z.db"), copy);
        var options = new DbContextOptionsBuilder<FibotrailDbContext>().UseSqlite($"Data Source={copy};Pooling=False").Options;
        await using var copied = new FibotrailDbContext(options);
        Assert.Equal(1, await copied.Credentials.CountAsync());
    }
}

public class HealthCheckTests
{
    [Fact]
    public async Task Healthz_answers_200_with_no_session()
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.NewBrowser().GetAsync("/healthz", asPage: false);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok dev", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_healthcheck_command_reports_a_healthy_and_an_unhealthy_server()
    {
        await using var app = await TestApp.StartAsync();
        using var client = app.App.GetTestClient();

        Assert.Equal(0, await HealthCheckCommand.RunAsync(client, new Uri("http://localhost/healthz")));
        Assert.Equal(1, await HealthCheckCommand.RunAsync(client, new Uri("http://localhost/no-such-route")));
    }

    [Fact]
    public async Task The_places_command_starts_from_the_real_entry_point_and_reports_a_missing_database()
    {
        // The command builds its own services, so this catches a service that only the web server registers.
        var folder = Directory.CreateTempSubdirectory("fibotrail-places-").FullName;
        using var error = new StringWriter();

        var exitCode = await EntryPoint.RunAsync(["places"], error, configuration =>
        {
            configuration.Sources.Clear();
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["FIBOTRAIL_DATA_DIR"] = folder });
        });

        Assert.Equal(1, exitCode);
        Assert.Empty(error.ToString());
        Directory.Delete(folder, recursive: true);
    }
}
