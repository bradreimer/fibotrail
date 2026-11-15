using System.Text.Encodings.Web;

namespace Fibotrail.Server.Web;

/// <summary>
/// The setup and sign-in screens, as plain HTML with no script. They're served before a session
/// exists, so they don't depend on the web app in apps/web.
/// </summary>
public static class Pages
{
    public const string WrongPassword = "That password isn't right.";

    public const string ResetCommand = "docker exec -it fibotrail fibotrail reset-password";

    public const string NeedsHttps =
        "Sign-in needs HTTPS. Open Fibotrail through your reverse proxy, at an address that starts with https://.";

    public static string Setup(string csrfField, string? problem, bool insecure) => Layout(
        "Set your password",
        $"""
        <h1>Set your password</h1>
        <p>Your travel history is personal. Choose a password before anything is shown.</p>
        {Notice(insecure ? NeedsHttps : null)}
        {Problem(problem)}
        <form method="post" action="/setup">
          {csrfField}
          <input type="text" name="username" value="fibotrail" autocomplete="username" hidden>
          <label for="password">Password</label>
          <input id="password" name="password" type="password" autocomplete="new-password" minlength="12" required aria-describedby="password-hint">
          <p id="password-hint" class="hint">At least 12 characters. A few words in a row work well.</p>
          <label for="confirmation">Type it again</label>
          <input id="confirmation" name="confirmation" type="password" autocomplete="new-password" minlength="12" required>
          <button type="submit">Set password</button>
        </form>
        """);

    public static string SignIn(string csrfField, string? problem, bool insecure) => Layout(
        "Sign in",
        $"""
        <h1>Sign in</h1>
        {Notice(insecure ? NeedsHttps : null)}
        {Problem(problem)}
        <form method="post" action="/sign-in">
          {csrfField}
          <input type="text" name="username" value="fibotrail" autocomplete="username" hidden>
          <label for="password">Password</label>
          <input id="password" name="password" type="password" autocomplete="current-password" required autofocus>
          <button type="submit">Sign in</button>
        </form>
        <details>
          <summary>Forgot your password?</summary>
          <p>Run this command on the computer that runs Fibotrail. It asks for a new password and keeps your trips.</p>
          <pre><code>{Encode(ResetCommand)}</code></pre>
        </details>
        """);

    public static string NotBuilt() => Layout(
        "Fibotrail",
        """
        <h1>The app isn't built yet</h1>
        <p>The server is running, but it has no web app to show. Run <code>npm run build</code> in <code>apps/web</code>, or use the Docker image, which includes it.</p>
        """);

    public static string Expired() => Layout(
        "Try again",
        """
        <h1>This page is out of date</h1>
        <p>Go back, reload the page, and try again.</p>
        <p><a href="/">Back to Fibotrail</a></p>
        """);

    private static string Layout(string title, string body) =>
        $"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
          <meta name="robots" content="noindex">
          <title>{Encode(title)} · Fibotrail</title>
          <link rel="stylesheet" href="/auth.css">
        </head>
        <body>
          <main>
            <p class="brand">Fibotrail</p>
        {body}
          </main>
        </body>
        </html>
        """;

    private static string Problem(string? problem) =>
        problem is null ? string.Empty : $"<p class=\"problem\" role=\"alert\">{Encode(problem)}</p>";

    private static string Notice(string? notice) =>
        notice is null ? string.Empty : $"<p class=\"notice\" role=\"status\">{Encode(notice)}</p>";

    private static string Encode(string text) => HtmlEncoder.Default.Encode(text);
}
