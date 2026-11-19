using Fibotrail.Server.Auth;
using Fibotrail.Server.Mcp;
using Fibotrail.Server.Web;

namespace Fibotrail.Server.Hosting;

/// <summary>
/// The middleware, in order. The order is part of the security model, so it lives in one place.
/// </summary>
public static class RequestPipeline
{
    public static void UseFibotrailPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseForwardedHeaders();
        app.UseMiddleware<SecurityHeadersMiddleware>();

        // The MCP server answers before the session and anti-forgery checks. It has its own token.
        app.UseMcp();

        // API answers aren't compressed. They're small, and a compressed answer that holds a secret
        // next to text an attacker chose can leak the secret through its length.
        app.UseWhen(
            context => !context.Request.Path.StartsWithSegments("/api"),
            branch => branch.UseResponseCompression());

        // Static files are public, so they're served before the session checks.
        app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = WebApp.SetCacheHeaders });
        app.UseAuthentication();
        app.UseAuthorization();

        // After authorization, so a request with no session gets 401 and not 400.
        app.UseMiddleware<AntiforgeryMiddleware>();
    }
}
