using System.Net;
using Fibotrail.Server.Web;

namespace Fibotrail.Server.Tests;

/// <summary>
/// The middleware order is part of the security model. These tests fail when a change to
/// Hosting/RequestPipeline.cs moves a step past one it must come before.
/// </summary>
public class PipelineTests
{
    [Theory]
    [InlineData("/healthz")]
    [InlineData("/mcp")]
    [InlineData("/assets/app-abc123.js")]
    [InlineData("/api/session")]
    [InlineData("/sign-in")]
    public async Task Every_response_carries_the_security_headers(string path)
    {
        await using var app = await TestApp.StartAsync(mcpToken: "a-token-for-the-pipeline-test-0123456789");

        var response = await app.NewBrowser().GetAsync(path, asPage: false);

        Assert.Equal(SecurityHeadersMiddleware.ContentSecurityPolicy, string.Join(' ', response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("nosniff", string.Join(' ', response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", string.Join(' ', response.Headers.GetValues("Referrer-Policy")));
    }

    [Fact]
    public async Task Mcp_answers_with_its_own_token_check_before_the_session_check()
    {
        await using var app = await TestApp.StartAsync(mcpToken: "a-token-for-the-pipeline-test-0123456789");

        var response = await app.NewBrowser().GetAsync("/mcp", asPage: false);

        // The session check would redirect or answer 401 without a Bearer challenge.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_post_with_no_session_gets_401_before_the_anti_forgery_check()
    {
        await using var app = await TestApp.StartAsync();
        var browser = app.NewBrowser();
        browser.ForgetCsrfToken();

        var response = await browser.SendJsonAsync(HttpMethod.Post, "/api/scan");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
