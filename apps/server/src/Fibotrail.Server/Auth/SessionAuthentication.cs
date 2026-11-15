using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Fibotrail.Server.Auth;

/// <summary>The session cookie. It's HttpOnly, Secure, and SameSite=Lax.</summary>
public static class SessionCookie
{
    public const string Name = "fibotrail_session";

    public static void Write(HttpResponse response, string token) =>
        response.Cookies.Append(Name, token, Options(AuthService.SessionLifetime));

    public static void Clear(HttpResponse response) => response.Cookies.Delete(Name, Options(null));

    private static CookieOptions Options(TimeSpan? maxAge) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        IsEssential = true,
        MaxAge = maxAge,
    };
}

/// <summary>Turns a valid session cookie into a signed-in request.</summary>
public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AuthService auth)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Session";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(SessionCookie.Name, out var token))
        {
            return AuthenticateResult.NoResult();
        }

        var session = await auth.CheckSessionAsync(token, Context.RequestAborted);
        if (session is null)
        {
            return AuthenticateResult.Fail("The session has ended.");
        }

        if (session.Refreshed)
        {
            // Send the cookie again, so its 30 days count from this use.
            SessionCookie.Write(Response, token!);
        }

        // Fibotrail has one user. The claim gives the anti-forgery token something to bind to.
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "owner")], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>A page request goes to the sign-in screen. Any other request gets 401.</summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var wantsPage = HttpMethods.IsGet(Request.Method)
            && !Request.Path.StartsWithSegments("/api")
            && Request.Headers.Accept.Any(value => value is not null && value.Contains("text/html", StringComparison.OrdinalIgnoreCase));
        if (wantsPage)
        {
            Response.Redirect("/sign-in");
        }
        else
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
        }

        return Task.CompletedTask;
    }
}
