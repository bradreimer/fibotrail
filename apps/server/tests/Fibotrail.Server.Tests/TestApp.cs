using System.Net;
using System.Text.RegularExpressions;
using Fibotrail.Server.Auth;
using Fibotrail.Server.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fibotrail.Server.Tests;

/// <summary>A clock that a test moves by hand.</summary>
public sealed class FakeTime : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan duration) => _now += duration;
}

/// <summary>Records each wait and returns at once, so no test sleeps.</summary>
public sealed class FakeDelay : ISignInDelay
{
    public List<TimeSpan> Waits { get; } = [];

    public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        Waits.Add(duration);
        return Task.CompletedTask;
    }
}

/// <summary>The real server on an in-memory connection, with its own empty data folder.</summary>
public sealed class TestApp : IAsyncDisposable
{
    public const string Password = "correct horse battery";

    /// <summary>A cheap hash cost, so the tests are fast. Production uses <see cref="Argon2Cost.Default"/>.</summary>
    public static readonly Argon2Cost FastCost = new(64, 1, 1);

    private TestApp(WebApplication app, ServerSettings settings, FakeTime time, FakeDelay delay)
    {
        App = app;
        Settings = settings;
        Time = time;
        Delay = delay;
    }

    public WebApplication App { get; }

    public ServerSettings Settings { get; }

    public FakeTime Time { get; }

    public FakeDelay Delay { get; }

    public static async Task<TestApp> StartAsync()
    {
        var folder = Directory.CreateTempSubdirectory("fibotrail-test-").FullName;
        var settings = new ServerSettings(folder, new Uri("http://immich.invalid:2283"), "test-key-not-real");
        var time = new FakeTime();
        var delay = new FakeDelay();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<TimeProvider>(time);
        builder.Services.AddSingleton<ISignInDelay>(delay);

        var app = ServerApp.Create(builder, settings, FastCost);
        await ServerApp.PrepareDatabaseAsync(app);
        await app.StartAsync();
        return new TestApp(app, settings, time, delay);
    }

    public Browser NewBrowser() => new(App.GetTestClient());

    /// <summary>A browser that has set the password and is signed in.</summary>
    public async Task<Browser> SignedInBrowserAsync()
    {
        var browser = NewBrowser();
        if (await WithServicesAsync(services => services.GetRequiredService<AuthService>().IsPasswordSetAsync()))
        {
            await browser.GetAsync("/sign-in");
            await browser.PostAsync("/sign-in", new() { ["password"] = Password });
        }
        else
        {
            await browser.GetAsync("/setup");
            await browser.PostAsync("/setup", new() { ["password"] = Password, ["confirmation"] = Password });
        }

        return browser;
    }

    public async Task<T> WithServicesAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    public Task<T> WithDbAsync<T>(Func<FibotrailDbContext, Task<T>> work) =>
        WithServicesAsync(services => work(services.GetRequiredService<FibotrailDbContext>()));

    public async ValueTask DisposeAsync()
    {
        await App.StopAsync();
        await App.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(Settings.DataDirectory, recursive: true);
    }
}

/// <summary>
/// A small browser: it keeps cookies, sends the anti-forgery token of the last page it loaded, and
/// doesn't follow redirects, so a test can see each answer.
/// </summary>
public sealed partial class Browser(HttpClient client)
{
    private readonly Dictionary<string, string> _cookies = [];
    private string? _csrfToken;

    public IReadOnlyDictionary<string, string> Cookies => _cookies;

    /// <summary>The raw Set-Cookie lines of the last answer.</summary>
    public IReadOnlyList<string> LastSetCookies { get; private set; } = [];

    public void ForgetCsrfToken() => _csrfToken = null;

    public void SetCookie(string name, string value) => _cookies[name] = value;

    public Task<HttpResponseMessage> GetAsync(string path, bool asPage = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (asPage)
        {
            request.Headers.Accept.ParseAdd("text/html");
        }

        return SendAsync(request);
    }

    public Task<HttpResponseMessage> PostAsync(string path, Dictionary<string, string> fields)
    {
        if (_csrfToken is not null)
        {
            fields["csrf"] = _csrfToken;
        }

        return SendAsync(new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) });
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        request.RequestUri = new Uri(new Uri("https://localhost"), request.RequestUri!.OriginalString);
        if (_cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(cookie => $"{cookie.Key}={cookie.Value}")));
        }

        var response = await client.SendAsync(request);
        LastSetCookies = response.Headers.TryGetValues("Set-Cookie", out var lines) ? [.. lines] : [];
        foreach (var line in LastSetCookies)
        {
            var pair = line.Split(';')[0].Split('=', 2);
            if (pair[1].Length == 0)
            {
                _cookies.Remove(pair[0]);
            }
            else
            {
                _cookies[pair[0]] = pair[1];
            }
        }

        if (response.Content.Headers.ContentType?.MediaType == "text/html")
        {
            var match = CsrfField().Match(await response.Content.ReadAsStringAsync());
            if (match.Success)
            {
                _csrfToken = WebUtility.HtmlDecode(match.Groups[1].Value);
            }
        }

        return response;
    }

    [GeneratedRegex("name=\"csrf\" value=\"([^\"]+)\"")]
    private static partial Regex CsrfField();
}
