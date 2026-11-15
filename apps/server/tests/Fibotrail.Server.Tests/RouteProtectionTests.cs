using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fibotrail.Server.Tests;

public partial class RouteProtectionTests
{
    /// <summary>
    /// Every route with its methods. A parameter such as {id} is filled with "1", so the address
    /// can be requested. The web app fallback has no method list, and has its own tests.
    /// </summary>
    private static List<(string Method, string Path)> Routes(TestApp app) =>
        [.. app.App.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (method, RouteParameter().Replace(endpoint.RoutePattern.RawText!, "1"))))];

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex RouteParameter();

    [Fact]
    public async Task Every_route_except_the_public_ones_answers_401_with_no_session()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();
        var guarded = Routes(app).Where(route => !ServerApp.PublicRoutes.Contains(route.Path)).ToList();

        // The list must hold the routes this test is about, or the test proves nothing.
        Assert.Contains(("GET", "/api/session"), guarded);
        Assert.Contains(("GET", "/openapi/1.json"), guarded);
        Assert.Contains(("POST", "/sign-out"), guarded);

        foreach (var (method, path) in guarded)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            var response = await app.NewBrowser().SendAsync(request);

            Assert.True(HttpStatusCode.Unauthorized == response.StatusCode, $"{method} {path} answered {(int)response.StatusCode} with no session.");
        }
    }

    [Fact]
    public async Task The_public_routes_are_exactly_healthz_setup_and_sign_in()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();

        foreach (var (method, path) in Routes(app).Where(route => route.Method == "GET"))
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            var response = await app.NewBrowser().SendAsync(request);
            var open = response.StatusCode != HttpStatusCode.Unauthorized;

            Assert.True(open == ServerApp.PublicRoutes.Contains(path), $"{method} {path} answered {(int)response.StatusCode}.");
        }
    }

    [Fact]
    public async Task An_api_request_gets_401_and_never_a_redirect_even_when_it_accepts_html()
    {
        await using var app = await TestApp.StartAsync();
        await app.SignedInBrowserAsync();

        var response = await app.NewBrowser().GetAsync("/api/session", asPage: true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
