using System.Net;
using Fibotrail.Server.Auth;
using Fibotrail.Server.Cli;
using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fibotrail.Server.Tests;

public class SignInDelayTests
{
    private static async Task<HttpResponseMessage> FailAsync(Browser browser)
    {
        await browser.GetAsync("/sign-in");
        return await browser.PostAsync("/sign-in", new() { ["password"] = "not the password" });
    }

    [Fact]
    public async Task The_sixth_failed_attempt_in_15_minutes_waits_30_seconds()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();
        var browser = app.NewBrowser();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await FailAsync(browser);
            app.Time.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.Empty(app.Delay.Waits);

        var sixth = await FailAsync(browser);

        Assert.Equal([TimeSpan.FromSeconds(30)], app.Delay.Waits);
        Assert.Equal(HttpStatusCode.Unauthorized, sixth.StatusCode);
    }

    [Fact]
    public async Task The_right_password_is_also_delayed_and_then_accepted()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();
        var browser = app.NewBrowser();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await FailAsync(browser);
        }

        await browser.GetAsync("/sign-in");
        var response = await browser.PostAsync("/sign-in", new() { ["password"] = TestApp.Password });

        Assert.Single(app.Delay.Waits);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(0, await app.WithDbAsync(db => db.SignInFailures.CountAsync()));
    }

    [Fact]
    public async Task Failures_older_than_15_minutes_no_longer_count()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();
        var browser = app.NewBrowser();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await FailAsync(browser);
        }

        app.Time.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));
        await FailAsync(browser);

        Assert.Empty(app.Delay.Waits);
    }

    [Fact]
    public async Task The_count_is_kept_for_each_address()
    {
        await using var app = await TestApp.StartAsync();

        var (first, second) = await app.WithServicesAsync(async services =>
        {
            var throttle = services.GetRequiredService<SignInThrottle>();
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                await throttle.RecordFailureAsync("192.0.2.1");
            }

            return (await throttle.WaitIfNeededAsync("192.0.2.1"), await throttle.WaitIfNeededAsync("192.0.2.2"));
        });

        Assert.True(first);
        Assert.False(second);
    }
}

public class ResetPasswordTests
{
    private const string NewPassword = "a brand new password";

    /// <summary>Runs the command the way the image does: in its own scope, against the same database file.</summary>
    private static async Task<int> RunAsync(TestApp app, ScriptedConsole console, params string[] arguments)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(app.Time);
        services.AddFibotrailCore(app.Settings, TestApp.FastCost);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        return await ResetPasswordCommand.RunAsync(arguments, console, scope.ServiceProvider.GetRequiredService<AuthService>(), app.Settings);
    }

    private static async Task<HttpStatusCode> SignInAsync(TestApp app, string password)
    {
        var browser = app.NewBrowser();
        await browser.GetAsync("/sign-in");
        return (await browser.PostAsync("/sign-in", new() { ["password"] = password })).StatusCode;
    }

    [Fact]
    public async Task After_a_reset_the_old_password_fails_the_new_one_works_and_browsers_sign_in_again()
    {
        await using var app = await TestApp.StartAsync();
        var signedIn = await app.SignedInBrowserAsync();
        var console = new ScriptedConsole(NewPassword, NewPassword);

        var exitCode = await RunAsync(app, console);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, console.Prompts.Count);
        Assert.Equal(HttpStatusCode.Unauthorized, (await signedIn.GetAsync("/api/session", asPage: false)).StatusCode);
        Assert.Equal("/sign-in", (await signedIn.GetAsync("/")).Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, await SignInAsync(app, TestApp.Password));
        Assert.Equal(HttpStatusCode.Redirect, await SignInAsync(app, NewPassword));
    }

    [Fact]
    public async Task A_reset_clears_the_sign_in_delay()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await SignInAsync(app, "not the password");
        }

        await RunAsync(app, new ScriptedConsole(NewPassword, NewPassword));
        var status = await SignInAsync(app, NewPassword);

        Assert.Empty(app.Delay.Waits);
        Assert.Equal(HttpStatusCode.Redirect, status);
    }

    [Theory]
    [InlineData("elevenchars", "elevenchars", "Use at least 12 characters.")]
    [InlineData("a brand new password", "a brand new passw0rd", "Those two passwords don't match.")]
    public async Task A_rejected_password_changes_nothing_and_says_why(string password, string confirmation, string reason)
    {
        await using var app = await TestApp.StartAsync();
        var signedIn = await app.SignedInBrowserAsync();
        var console = new ScriptedConsole(password, confirmation);

        var exitCode = await RunAsync(app, console);

        Assert.Equal(1, exitCode);
        Assert.Equal($"Nothing changed. {reason}", Assert.Single(console.Lines));
        Assert.Equal(HttpStatusCode.OK, (await signedIn.GetAsync("/api/session", asPage: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, await SignInAsync(app, TestApp.Password));
    }

    [Fact]
    public async Task The_command_never_takes_the_password_as_an_argument()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();
        var console = new ScriptedConsole(NewPassword, NewPassword);

        var exitCode = await RunAsync(app, console, NewPassword);

        Assert.Equal(2, exitCode);
        Assert.Empty(console.Prompts);
        Assert.Equal(HttpStatusCode.Redirect, await SignInAsync(app, TestApp.Password));
    }

    [Fact]
    public async Task A_reset_touches_only_the_password_the_sessions_and_the_delay()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();
        var tablesBefore = await TableNamesAsync(app);

        await RunAsync(app, new ScriptedConsole(NewPassword, NewPassword));

        Assert.Equal(tablesBefore, await TableNamesAsync(app));
        Assert.Equal(1, await app.WithDbAsync(db => db.Credentials.CountAsync()));
        Assert.Equal(0, await app.WithDbAsync(db => db.Sessions.CountAsync()));
    }

    [Fact]
    public async Task With_no_database_the_command_says_to_start_fibotrail_first()
    {
        var folder = Directory.CreateTempSubdirectory("fibotrail-test-").FullName;
        try
        {
            var settings = new ServerSettings(folder);
            var services = new ServiceCollection();
            services.AddFibotrailCore(settings, TestApp.FastCost);
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var console = new ScriptedConsole(NewPassword, NewPassword);

            var exitCode = await ResetPasswordCommand.RunAsync([], console, scope.ServiceProvider.GetRequiredService<AuthService>(), settings);

            Assert.Equal(1, exitCode);
            Assert.Empty(console.Prompts);
            Assert.False(File.Exists(settings.DatabasePath));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static Task<List<string>> TableNamesAsync(TestApp app) =>
        app.WithDbAsync(db => db.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table' ORDER BY name")
            .ToListAsync());
}
