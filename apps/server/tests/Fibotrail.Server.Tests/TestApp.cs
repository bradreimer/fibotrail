using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fibotrail.Immich;
using Fibotrail.Immich.Tests;
using Fibotrail.Server.Auth;
using Fibotrail.Server.Cli;
using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
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

    private TestApp(WebApplication app, ServerSettings settings, FakeTime time, FakeDelay delay, FakeImmichServer immich)
    {
        Immich = immich;
        App = app;
        Settings = settings;
        Time = time;
        Delay = delay;
    }

    public WebApplication App { get; }

    public ServerSettings Settings { get; }

    public FakeTime Time { get; }

    public FakeDelay Delay { get; }

    /// <summary>The stand-in for Immich. A test fills its library and reads the requests it got.</summary>
    public FakeImmichServer Immich { get; }

    public ScanService Scans => App.Services.GetRequiredService<ScanService>();

    /// <summary>Runs one scan to its end, the way Scan now does.</summary>
    public async Task ScanAsync()
    {
        Assert.True(Scans.TryStart(), "A scan was already running.");
        await Scans.Current;
    }

    public const string ShellHtml = "<!doctype html><title>Fibotrail</title><div id=\"root\"></div>";

    /// <param name="folder">An existing data folder, to start the server again on the same data.</param>
    /// <param name="immich">An existing stand-in for Immich, to keep its library across a restart.</param>
    /// <param name="immichTimeout">How long one Immich request may take. Short, so a test of a hung Immich is fast.</param>
    /// <param name="logging">Receives every log line, at every level.</param>
    /// <param name="externalUrl">IMMICH_EXTERNAL_URL, or null for an empty one.</param>
    public static async Task<TestApp> StartAsync(string? folder = null, FakeImmichServer? immich = null, TimeSpan? immichTimeout = null, ILoggerProvider? logging = null, string? externalUrl = "https://photos.example.com")
    {
        folder ??= Directory.CreateTempSubdirectory("fibotrail-test-").FullName;
        immich ??= new FakeImmichServer();
        var settings = new ServerSettings(folder, new Uri("http://immich.invalid:2283"), FakeImmichServer.Key, externalUrl is null ? null : new Uri(externalUrl));
        var time = new FakeTime();
        var delay = new FakeDelay();

        // A stand-in for the built web app, so the tests don't need apps/web to be built.
        var webRoot = Directory.CreateDirectory(Path.Combine(folder, "wwwroot")).FullName;
        Directory.CreateDirectory(Path.Combine(webRoot, "assets"));
        await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), ShellHtml);
        await File.WriteAllTextAsync(Path.Combine(webRoot, "assets", "app-abc123.js"), "// app");
        await File.WriteAllTextAsync(Path.Combine(webRoot, "sw.js"), "// worker");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", WebRootPath = webRoot });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        if (logging is not null)
        {
            builder.Logging.AddProvider(logging).SetMinimumLevel(LogLevel.Trace);
        }

        builder.Services.AddSingleton<TimeProvider>(time);
        builder.Services.AddSingleton<ISignInDelay>(delay);
        builder.Services.AddSingleton(new ImmichTransport(immich));
        builder.Services.AddSingleton(new ImmichOptions(settings.ImmichUrl!, settings.ImmichApiKey!, immichTimeout ?? TimeSpan.FromSeconds(5))
        {
            // No test waits between tries.
            RetryDelays = [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero],
        });

        var app = ServerApp.Create(builder, settings, FastCost);
        await ServerApp.PrepareDatabaseAsync(app);
        await app.StartAsync();
        return new TestApp(app, settings, time, delay, immich);
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

    /// <summary>Stops the server and keeps its data folder, as a container restart does.</summary>
    public async Task StopAsync()
    {
        await App.StopAsync();
        await App.DisposeAsync();
        SqliteConnection.ClearAllPools();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (Directory.Exists(Settings.DataDirectory))
        {
            Directory.Delete(Settings.DataDirectory, recursive: true);
        }
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

    /// <summary>Reads an API route and returns its JSON answer.</summary>
    public async Task<JsonElement> GetJsonAsync(string path)
    {
        var response = await GetAsync(path, asPage: false);
        Assert.True(response.IsSuccessStatusCode, $"GET {path} answered {(int)response.StatusCode}.");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    /// <summary>Sends a change to an API route the way the web app does, with the token in a header.</summary>
    public Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (_csrfToken is not null)
        {
            request.Headers.Add("X-CSRF-Token", _csrfToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return SendAsync(request);
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
        else if (request.RequestUri.AbsolutePath == "/api/session" && response.IsSuccessStatusCode)
        {
            // The web app reads its token here, and so does this browser.
            using var session = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            _csrfToken = session.RootElement.GetProperty("csrfToken").GetString();
        }

        return response;
    }

    [GeneratedRegex("name=\"csrf\" value=\"([^\"]+)\"")]
    private static partial Regex CsrfField();
}

/// <summary>A terminal for tests: it answers prompts from a list and keeps what was written.</summary>
public sealed class ScriptedConsole(params string?[] answers) : IConsoleIo
{
    private readonly Queue<string?> _answers = new(answers);

    public List<string> Prompts { get; } = [];

    public List<string> Lines { get; } = [];

    public void WriteLine(string text) => Lines.Add(text);

    public string? ReadSecret(string prompt)
    {
        Prompts.Add(prompt);
        return _answers.Count > 0 ? _answers.Dequeue() : null;
    }
}
