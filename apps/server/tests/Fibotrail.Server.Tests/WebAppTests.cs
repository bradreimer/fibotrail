using System.Net;
using System.Text.Json;

namespace Fibotrail.Server.Tests;

public class WebAppTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/trips")]
    [InlineData("/inbox")]
    [InlineData("/settings")]
    [InlineData("/trips/42")]
    public async Task Every_app_address_serves_the_web_app_to_a_signed_in_browser(string path)
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();

        var response = await browser.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(TestApp.ShellHtml, await response.Content.ReadAsStringAsync());
        Assert.Equal("no-cache", response.Headers.CacheControl!.ToString());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/trips")]
    public async Task An_app_address_leads_to_sign_in_with_no_session(string path)
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();

        var page = await app.NewBrowser().GetAsync(path);
        var other = await app.NewBrowser().GetAsync(path, asPage: false);

        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal("/sign-in", page.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
    }

    [Fact]
    public async Task An_unknown_api_address_is_not_found_and_never_the_web_app()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();

        var response = await browser.GetAsync("/api/nothing-here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_static_files_of_the_web_app_need_no_session()
    {
        await using var app = await TestApp.StartAsync();

        var script = await app.NewBrowser().GetAsync("/assets/app-abc123.js", asPage: false);
        var worker = await app.NewBrowser().GetAsync("/sw.js", asPage: false);

        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal(HttpStatusCode.OK, worker.StatusCode);
    }

    [Fact]
    public async Task Hashed_files_are_kept_for_a_year_and_the_service_worker_is_always_checked()
    {
        await using var app = await TestApp.StartAsync();

        var script = await app.NewBrowser().GetAsync("/assets/app-abc123.js", asPage: false);
        var worker = await app.NewBrowser().GetAsync("/sw.js", asPage: false);

        Assert.Equal("public, max-age=31536000, immutable", script.Headers.CacheControl!.ToString());
        Assert.Equal("no-cache", worker.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task The_files_of_the_web_app_are_sent_compressed_and_api_answers_are_not()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        using var script = new HttpRequestMessage(HttpMethod.Get, "/assets/app-abc123.js");
        script.Headers.AcceptEncoding.ParseAdd("br");
        using var session = new HttpRequestMessage(HttpMethod.Get, "/api/session");
        session.Headers.AcceptEncoding.ParseAdd("br");

        var compressed = await browser.SendAsync(script);
        var plain = await browser.SendAsync(session);

        Assert.Equal(["br"], compressed.Content.Headers.ContentEncoding);
        Assert.Empty(plain.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task The_session_route_gives_the_web_app_a_token_that_sign_out_accepts()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();

        var session = await browser.GetJsonAsync("/api/session");
        var signOut = await browser.PostAsync("/sign-out", []);

        Assert.False(string.IsNullOrEmpty(session.GetProperty("csrfToken").GetString()));
        Assert.Equal("/sign-in", signOut.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task The_content_security_policy_allows_files_from_fibotrail_only()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();

        var response = await browser.GetAsync("/");
        var policy = string.Join(' ', response.Headers.GetValues("Content-Security-Policy"));

        Assert.Contains("default-src 'self'", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("http", policy, StringComparison.Ordinal);
    }

    /// <summary>
    /// The web app builds its TypeScript types from apps/web/src/api/openapi.json. This test keeps
    /// that file equal to what the server produces. To update the file after an API change, run
    /// the tests once with UPDATE_OPENAPI=1.
    /// </summary>
    [Fact]
    public async Task The_openapi_document_in_the_web_app_matches_the_server()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        var served = await browser.GetJsonAsync("/openapi/v1.json");
        var text = JsonSerializer.Serialize(served, Indented) + "\n";
        var path = Path.Combine(RepositoryRoot(), "apps", "web", "src", "api", "openapi.json");

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") == "1")
        {
            await File.WriteAllTextAsync(path, text);
        }

        Assert.True(
            File.Exists(path) && await File.ReadAllTextAsync(path) == text,
            "apps/web/src/api/openapi.json is out of date. Run the server tests with UPDATE_OPENAPI=1, then \"npm run api:types\" in apps/web.");
    }

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "CLAUDE.md")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("The repository root wasn't found.");
    }
}
