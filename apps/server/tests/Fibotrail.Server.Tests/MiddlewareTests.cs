using System.Text;
using Fibotrail.Server.Auth;
using Fibotrail.Server.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Fibotrail.Server.Tests;

public class MiddlewareTests
{
    [Fact]
    public async Task Security_headers_are_set_and_the_request_continues()
    {
        var context = new DefaultHttpContext();
        var called = false;
        var middleware = new SecurityHeadersMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(called);
        Assert.Equal(SecurityHeadersMiddleware.ContentSecurityPolicy, context.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions.ToString());
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task A_request_that_reads_only_passes_without_a_token(string method)
    {
        var (context, _) = Request(method, "/api/trips", withEndpoint: true);
        var (middleware, next) = Antiforgery();

        await middleware.InvokeAsync(context, new FakeAntiforgery(valid: false));

        Assert.True(next.Called);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task A_post_with_a_valid_token_passes()
    {
        var (context, _) = Request("POST", "/api/trips", withEndpoint: true);
        var (middleware, next) = Antiforgery();

        await middleware.InvokeAsync(context, new FakeAntiforgery(valid: true));

        Assert.True(next.Called);
    }

    [Fact]
    public async Task A_post_to_no_route_passes_so_the_fallback_answers()
    {
        var (context, _) = Request("POST", "/nowhere", withEndpoint: false);
        var (middleware, next) = Antiforgery();

        await middleware.InvokeAsync(context, new FakeAntiforgery(valid: false));

        Assert.True(next.Called);
    }

    [Fact]
    public async Task An_api_post_without_a_token_gets_400_with_json()
    {
        var (context, body) = Request("POST", "/api/trips", withEndpoint: true);
        var (middleware, next) = Antiforgery();

        await middleware.InvokeAsync(context, new FakeAntiforgery(valid: false));

        Assert.False(next.Called);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType, StringComparison.Ordinal);
        Assert.Contains(AntiforgeryMiddleware.OutOfDate, Encoding.UTF8.GetString(body.ToArray()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_screen_post_without_a_token_gets_400_with_the_expired_page()
    {
        var (context, body) = Request("POST", "/sign-in", withEndpoint: true);
        var (middleware, next) = Antiforgery();

        await middleware.InvokeAsync(context, new FakeAntiforgery(valid: false));

        Assert.False(next.Called);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(HtmlResults.ContentType, context.Response.ContentType);
        Assert.Equal(Pages.Expired(), Encoding.UTF8.GetString(body.ToArray()));
    }

    private static (DefaultHttpContext Context, MemoryStream Body) Request(string method, string path, bool withEndpoint)
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = body;
        if (withEndpoint)
        {
            context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, path));
        }

        return (context, body);
    }

    private static (AntiforgeryMiddleware Middleware, NextCall Next) Antiforgery()
    {
        var next = new NextCall();
        return (new AntiforgeryMiddleware(next.InvokeAsync), next);
    }

    private sealed class NextCall
    {
        public bool Called { get; private set; }

        public Task InvokeAsync(HttpContext context)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAntiforgery(bool valid) : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();

        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(valid);

        public void SetCookieTokenAndHeader(HttpContext httpContext) => throw new NotSupportedException();

        public Task ValidateRequestAsync(HttpContext httpContext) => throw new NotSupportedException();
    }
}
