using System.Net;
using System.Text.Encodings.Web;
using Fibotrail.Server.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Fibotrail.Server.Auth;

/// <summary>
/// The setup, sign-in, and sign-out screens, and the API route that hands the browser its
/// anti-forgery token. Setup and sign-in are public. Everything else needs a session.
/// </summary>
public static class AuthRoutes
{
    /// <summary>Maps <c>/setup</c>, <c>/sign-in</c>, and <c>/sign-out</c>. These are HTML screens, not API routes.</summary>
    public static void MapAuthRoutes(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/setup", ShowSetupAsync).AllowAnonymous();
        app.MapPost("/setup", SetUpAsync).AllowAnonymous();
        app.MapGet("/sign-in", ShowSignInAsync).AllowAnonymous();
        app.MapPost("/sign-in", SignInAsync).AllowAnonymous();
        app.MapPost("/sign-out", SignOutAsync);
    }

    /// <summary>Maps <c>/api/session</c>, which the web app calls for its anti-forgery token.</summary>
    public static void MapSessionRoute(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapGet("/session", GetSession).WithName("GetSession");
    }

    private static async Task<IResult> ShowSetupAsync(HttpContext context, AuthService auth, IAntiforgery antiforgery) =>
        await auth.IsPasswordSetAsync(context.RequestAborted)
            ? Results.Redirect("/sign-in")
            : HtmlResults.Html(Pages.Setup(CsrfField(context, antiforgery), null, IsInsecure(context.Request)));

    private static async Task<IResult> SetUpAsync(HttpContext context, AuthService auth, IAntiforgery antiforgery)
    {
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var change = await auth.SetUpAsync(form["password"], form["confirmation"], context.RequestAborted);
        switch (change.Outcome)
        {
            case SetupOutcome.AlreadySetUp:
                // The setup route stops working once a password is stored.
                return Results.NotFound();
            case SetupOutcome.Rejected:
                return HtmlResults.Html(
                    Pages.Setup(CsrfField(context, antiforgery), change.Problem, IsInsecure(context.Request)),
                    StatusCodes.Status400BadRequest);
            default:
                SessionCookie.Write(context.Response, await auth.CreateSessionAsync(context.RequestAborted));
                return Results.Redirect("/");
        }
    }

    private static async Task<IResult> ShowSignInAsync(HttpContext context, AuthService auth, IAntiforgery antiforgery) =>
        await auth.IsPasswordSetAsync(context.RequestAborted)
            ? HtmlResults.Html(Pages.SignIn(CsrfField(context, antiforgery), null, IsInsecure(context.Request)))
            : Results.Redirect("/setup");

    private static async Task<IResult> SignInAsync(
        HttpContext context, AuthService auth, SignInThrottle throttle, IAntiforgery antiforgery)
    {
        if (!await auth.IsPasswordSetAsync(context.RequestAborted))
        {
            return Results.Redirect("/setup");
        }

        var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await throttle.WaitIfNeededAsync(address, context.RequestAborted);

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (!await auth.VerifyPasswordAsync(form["password"], context.RequestAborted))
        {
            await throttle.RecordFailureAsync(address, context.RequestAborted);
            return HtmlResults.Html(
                Pages.SignIn(CsrfField(context, antiforgery), Pages.WrongPassword, IsInsecure(context.Request)),
                StatusCodes.Status401Unauthorized);
        }

        await throttle.ClearAsync(address, context.RequestAborted);
        SessionCookie.Write(context.Response, await auth.CreateSessionAsync(context.RequestAborted));
        return Results.Redirect("/");
    }

    private static async Task<IResult> SignOutAsync(HttpContext context, AuthService auth)
    {
        await auth.EndSessionAsync(context.Request.Cookies[SessionCookie.Name], context.RequestAborted);
        SessionCookie.Clear(context.Response);
        return Results.Redirect("/sign-in");
    }

    private static Ok<SessionInfo> GetSession(HttpContext context, IAntiforgery antiforgery) =>
        TypedResults.Ok(new SessionInfo(antiforgery.GetAndStoreTokens(context).RequestToken ?? string.Empty));

    private static string CsrfField(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        var value = HtmlEncoder.Default.Encode(tokens.RequestToken ?? string.Empty);
        return $"<input type=\"hidden\" name=\"{tokens.FormFieldName}\" value=\"{value}\">";
    }

    /// <summary>
    /// True when the browser won't keep the Secure session cookie: the request isn't HTTPS and
    /// isn't to this machine. The screen then says so.
    /// </summary>
    private static bool IsInsecure(HttpRequest request) =>
        !request.IsHttps
        && !string.Equals(request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase)
        && !(IPAddress.TryParse(request.Host.Host, out var address) && IPAddress.IsLoopback(address));
}
