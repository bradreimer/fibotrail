using Fibotrail.Immich;

namespace Fibotrail.Server.Immich;

/// <summary>The result of a connection check, as Settings shows it.</summary>
/// <param name="Connected">True when the address and the key both work.</param>
/// <param name="Version">The Immich version, when Immich answered.</param>
/// <param name="Message">What is wrong and what to do, when not connected.</param>
/// <param name="Warning">A note that doesn't stop scanning, such as an untested Immich version.</param>
public sealed record ImmichStatus(bool Connected, string? Version, string? Message, string? Warning);

/// <summary>What sends requests to Immich. Only a test registers one, to stand in for Immich.</summary>
public sealed record ImmichTransport(HttpMessageHandler Handler);

/// <summary>Checks that Immich answers and that the key works.</summary>
public sealed class ImmichConnection(IImmichClient immich)
{
    public const string CantReachMessage = "Can't reach Immich. Check IMMICH_URL and IMMICH_API_KEY in your .env file.";

    /// <summary>The Immich versions that Fibotrail was tested with: 3.4.x. See docs/knowledge/immich-api-findings.md.</summary>
    public static bool IsTested(ImmichVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return version is { Major: 3, Minor: 4 };
    }

    /// <summary>
    /// Makes two requests: the version, and a search for one asset. Immich answers the version
    /// without a key, so only the search shows that the key works. Neither is retried, so Settings
    /// gets its answer within two timeouts.
    /// </summary>
    public async Task<ImmichStatus> CheckAsync(CancellationToken cancellationToken)
    {
        ImmichVersion? version = null;
        try
        {
            version = await immich.GetVersionAsync(cancellationToken, retry: false);
            await immich.SearchAsync(null, null, 1, cancellationToken, retry: false);
        }
        catch (ImmichException)
        {
            return new ImmichStatus(false, version?.ToString(), CantReachMessage, null);
        }

        var warning = IsTested(version)
            ? null
            : $"Fibotrail was tested with Immich 3.4. This server runs {version}, so a scan might miss some details.";
        return new ImmichStatus(true, version.ToString(), null, warning);
    }
}
