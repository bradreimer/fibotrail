using Fibotrail.Server.Immich;
using Fibotrail.Server.Web;
using Microsoft.AspNetCore.Antiforgery;

namespace Fibotrail.Server.Auth;

/// <summary>
/// Every request that changes state must carry a valid anti-forgery token. A request without one
/// gets 400: JSON under <c>/api</c>, and the expired-page screen everywhere else.
/// </summary>
public sealed class AntiforgeryMiddleware(RequestDelegate next)
{
    public const string OutOfDate = "This page is out of date. Reload it, then try again.";

    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(antiforgery);

        // A request that matched no route gets its 404 from the fallback, not a 400 from here.
        if (!ReadsOnly(context.Request.Method) && context.GetEndpoint() is not null
            && !await antiforgery.IsRequestValidAsync(context))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                await context.Response.WriteAsJsonAsync(new ProblemDetail(OutOfDate));
            }
            else
            {
                await context.Response.WriteHtmlAsync(Pages.Expired());
            }

            return;
        }

        await next(context);
    }

    private static bool ReadsOnly(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
}
