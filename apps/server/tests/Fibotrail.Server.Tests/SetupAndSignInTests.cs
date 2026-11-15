using System.Net;
using System.Text;
using Fibotrail.Server.Auth;
using Fibotrail.Server.Web;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fibotrail.Server.Tests;

public class FirstRunTests
{
    [Fact]
    public async Task With_no_password_every_page_leads_to_the_setup_screen()
    {
        await using var app = await TestApp.StartAsync();
        var browser = app.NewBrowser();

        var home = await browser.GetAsync("/");
        var signIn = await browser.GetAsync("/sign-in");
        var setup = await browser.GetAsync("/setup");

        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.Equal("/sign-in", home.Headers.Location!.OriginalString);
        Assert.Equal("/setup", signIn.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        Assert.Contains("Set your password", await setup.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("elevenchars", "elevenchars", PasswordPolicy.TooShort)]
    [InlineData("twelve chars", "twelve charz", PasswordPolicy.Mismatch)]
    public async Task A_rejected_password_stores_nothing_and_says_why(string password, string confirmation, string reason)
    {
        await using var app = await TestApp.StartAsync();
        var browser = app.NewBrowser();
        await browser.GetAsync("/setup");

        var response = await browser.PostAsync("/setup", new() { ["password"] = password, ["confirmation"] = confirmation });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(reason, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), StringComparison.Ordinal);
        Assert.False(await app.WithServicesAsync(services => services.GetRequiredService<AuthService>().IsPasswordSetAsync()));
    }

    [Fact]
    public async Task Setup_stores_the_password_and_signs_you_in()
    {
        await using var app = await TestApp.StartAsync();
        var browser = app.NewBrowser();
        await browser.GetAsync("/setup");

        var response = await browser.PostAsync("/setup", new() { ["password"] = TestApp.Password, ["confirmation"] = TestApp.Password });
        var home = await browser.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
    }

    [Fact]
    public async Task The_setup_route_stops_working_once_a_password_is_stored()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();
        var stranger = app.NewBrowser();

        var page = await stranger.GetAsync("/setup");
        await stranger.GetAsync("/sign-in");
        var post = await stranger.PostAsync("/setup", new() { ["password"] = "another password", ["confirmation"] = "another password" });

        Assert.Equal("/sign-in", page.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.True(await app.WithServicesAsync(services => services.GetRequiredService<AuthService>().VerifyPasswordAsync(TestApp.Password)));
    }

    [Fact]
    public async Task The_data_folder_holds_no_plain_text_password()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();
        var hash = await app.WithDbAsync(db => db.Credentials.Select(credential => credential.PasswordHash).SingleAsync());

        Assert.StartsWith("$argon2id$", hash, StringComparison.Ordinal);

        // Close the connections first, so SQLite folds the WAL file into the database file.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var files = Directory.GetFiles(app.Settings.DataDirectory, "*", SearchOption.AllDirectories);
        Assert.Contains(app.Settings.DatabasePath, files);
        foreach (var file in files)
        {
            Assert.DoesNotContain(TestApp.Password, Encoding.Latin1.GetString(await File.ReadAllBytesAsync(file)), StringComparison.Ordinal);
        }
    }
}

public class SignInTests
{
    [Fact]
    public async Task With_a_password_and_no_session_every_page_leads_to_the_sign_in_screen()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();
        var browser = app.NewBrowser();

        var home = await browser.GetAsync("/");
        var signIn = await browser.GetAsync("/sign-in");
        var html = await signIn.Content.ReadAsStringAsync();

        Assert.Equal("/sign-in", home.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.Contains("Forgot your password?", html, StringComparison.Ordinal);
        Assert.Contains(Pages.ResetCommand, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_wrong_password_says_only_that_it_is_not_right()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();
        var browser = app.NewBrowser();
        await browser.GetAsync("/sign-in");

        var response = await browser.PostAsync("/sign-in", new() { ["password"] = "not the password" });
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("That password isn't right.", html, StringComparison.Ordinal);
        Assert.False(browser.Cookies.ContainsKey(SessionCookie.Name));
    }

    [Fact]
    public async Task The_right_password_starts_a_session_with_a_locked_down_cookie()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();
        var browser = app.NewBrowser();
        await browser.GetAsync("/sign-in");

        var response = await browser.PostAsync("/sign-in", new() { ["password"] = TestApp.Password });
        var cookie = Assert.Single(browser.LastSetCookies, line => line.StartsWith(SessionCookie.Name, StringComparison.Ordinal));
        var session = await browser.GetAsync("/api/session", asPage: false);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=2592000", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }

    [Fact]
    public async Task The_database_holds_a_hash_of_the_session_token_and_not_the_token()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();

        var stored = await app.WithDbAsync(db => db.Sessions.Select(session => session.TokenHash).SingleAsync());

        Assert.NotEqual(browser.Cookies[SessionCookie.Name], stored);
    }

    [Fact]
    public async Task Sign_out_ends_the_session_on_the_server_and_clears_the_cookie()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        var token = browser.Cookies[SessionCookie.Name];
        await browser.GetAsync("/");

        var response = await browser.PostAsync("/sign-out", []);
        var copied = app.NewBrowser();
        copied.SetCookie(SessionCookie.Name, token);

        Assert.Equal("/sign-in", response.Headers.Location!.OriginalString);
        Assert.False(browser.Cookies.ContainsKey(SessionCookie.Name));
        Assert.Equal(HttpStatusCode.Unauthorized, (await copied.GetAsync("/api/session", asPage: false)).StatusCode);
        Assert.Equal(0, await app.WithDbAsync(db => db.Sessions.CountAsync()));
    }

    [Fact]
    public async Task A_session_lasts_30_days_from_its_last_use()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();

        app.Time.Advance(TimeSpan.FromDays(29));
        var day29 = await browser.GetAsync("/api/session", asPage: false);
        var renewed = browser.LastSetCookies.Any(line => line.StartsWith(SessionCookie.Name, StringComparison.Ordinal));
        app.Time.Advance(TimeSpan.FromDays(29));
        var day58 = await browser.GetAsync("/api/session", asPage: false);
        app.Time.Advance(TimeSpan.FromDays(31));
        var day89 = await browser.GetAsync("/api/session", asPage: false);

        Assert.Equal(HttpStatusCode.OK, day29.StatusCode);
        Assert.True(renewed, "A use after a day sends the cookie again, so its 30 days restart.");
        Assert.Equal(HttpStatusCode.OK, day58.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, day89.StatusCode);
    }

    [Fact]
    public async Task A_made_up_session_cookie_is_rejected()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();
        var browser = app.NewBrowser();
        browser.SetCookie(SessionCookie.Name, new string('A', 64));

        var response = await browser.GetAsync("/api/session", asPage: false);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_request_that_changes_state_without_an_anti_forgery_token_is_refused()
    {
        await using var app = await TestApp.StartAsync();
        var browser = await app.SignedInBrowserAsync();
        await browser.GetAsync("/");
        browser.ForgetCsrfToken();

        var signOut = await browser.PostAsync("/sign-out", []);
        var stranger = app.NewBrowser();
        var signIn = await stranger.PostAsync("/sign-in", new() { ["password"] = TestApp.Password });

        Assert.Equal(HttpStatusCode.BadRequest, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, signIn.StatusCode);
        Assert.Equal(1, await app.WithDbAsync(db => db.Sessions.CountAsync()));
    }

    [Fact]
    public async Task Over_plain_http_on_another_host_the_screen_says_sign_in_needs_https()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();
        using var client = app.App.GetTestClient();

        var remote = await client.GetStringAsync(new Uri("http://nas.example:11235/sign-in"));
        var local = await client.GetStringAsync(new Uri("http://localhost:11235/sign-in"));
        using var proxied = new HttpRequestMessage(HttpMethod.Get, new Uri("https://trips.example/sign-in"));
        var secure = await (await client.SendAsync(proxied)).Content.ReadAsStringAsync();

        Assert.Contains("Sign-in needs HTTPS", remote, StringComparison.Ordinal);
        Assert.DoesNotContain("Sign-in needs HTTPS", local, StringComparison.Ordinal);
        Assert.DoesNotContain("Sign-in needs HTTPS", secure, StringComparison.Ordinal);
    }
}
