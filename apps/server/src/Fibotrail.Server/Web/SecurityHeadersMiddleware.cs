namespace Fibotrail.Server.Web;

/// <summary>
/// Adds the browser security headers to every response: a content security policy that allows
/// this server only, no content-type sniffing, and no referrer.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string ContentSecurityPolicy =
        "default-src 'self'; frame-ancestors 'none'; form-action 'self'; base-uri 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        return next(context);
    }
}
