using Microsoft.AspNetCore.StaticFiles;

namespace Fibotrail.Server.Web;

/// <summary>What the browser needs to make a request that changes state.</summary>
/// <param name="CsrfToken">The anti-forgery token. Send it in the X-CSRF-Token header, or the csrf form field.</param>
public sealed record SessionInfo(string CsrfToken);

/// <summary>Serves the built web app from apps/web. The build lands in the web root.</summary>
public static class WebApp
{
    public const string ShellFile = "index.html";

    /// <summary>The page of the web app. Every app address gets the same page.</summary>
    public static IResult Shell(IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var file = environment.WebRootFileProvider.GetFileInfo(ShellFile);
        if (!file.Exists)
        {
            return Results.Content(Pages.NotBuilt(), "text/html; charset=utf-8");
        }

        // The page names the current script files, so the browser must ask for it every time.
        return new ShellResult(file);
    }

    /// <summary>
    /// Files under /assets have a hash in their names, so they never change and can be kept for a
    /// year. Everything else is checked with the server on each use.
    /// </summary>
    public static void SetCacheHeaders(StaticFileResponseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = context.Context.Request.Path;
        context.Context.Response.Headers.CacheControl = path.StartsWithSegments("/assets")
            ? "public, max-age=31536000, immutable"
            : "no-cache";
    }

    private sealed class ShellResult(Microsoft.Extensions.FileProviders.IFileInfo file) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = "text/html; charset=utf-8";
            httpContext.Response.Headers.CacheControl = "no-cache";
            await httpContext.Response.SendFileAsync(file, httpContext.RequestAborted);
        }
    }
}
