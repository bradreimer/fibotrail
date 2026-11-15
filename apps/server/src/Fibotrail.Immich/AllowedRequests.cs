using System.Text.RegularExpressions;

namespace Fibotrail.Immich;

/// <summary>
/// The Immich requests that Fibotrail may make. All of them read. Fibotrail never writes to
/// Immich: see ADR-001. The search is a POST, and it's still a read.
/// </summary>
public static partial class AllowedRequests
{
    /// <summary>
    /// The list, in the form the client generator takes. scripts/generate-immich-client.sh has the
    /// same list, and a test fails when the two differ.
    /// </summary>
    public static readonly IReadOnlyList<string> Patterns =
    [
        "/server/version#GET",
        "/search/metadata#POST",
        "/assets/statistics#GET",
        "/assets/{id}/thumbnail#GET",
    ];

    /// <summary>True when the method and the path under /api are on the list.</summary>
    public static bool IsAllowed(HttpMethod method, string apiPath)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(apiPath);
        return Patterns.Any(pattern =>
        {
            var parts = pattern.Split('#');
            return string.Equals(parts[1], method.Method, StringComparison.OrdinalIgnoreCase)
                && Regex.IsMatch(apiPath, "^" + PathParameter().Replace(Regex.Escape(parts[0]), "[0-9a-fA-F-]{36}") + "$");
        });
    }

    // Regex.Escape turns "{id}" into "\{id}".
    [GeneratedRegex(@"\\\{[a-z]+\}")]
    private static partial Regex PathParameter();
}
