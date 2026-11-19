namespace Fibotrail.Server.Web;

/// <summary>Sends a server-rendered HTML screen, such as sign-in or the expired-page notice.</summary>
public static class HtmlResults
{
    public const string ContentType = "text/html; charset=utf-8";

    /// <summary>An HTML screen as a route result.</summary>
    public static IResult Html(string html, int statusCode = StatusCodes.Status200OK) =>
        Results.Content(html, ContentType, statusCode: statusCode);

    /// <summary>Writes an HTML screen straight to the response, for middleware that has no route result.</summary>
    public static Task WriteHtmlAsync(this HttpResponse response, string html)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.ContentType = ContentType;
        return response.WriteAsync(html);
    }
}
