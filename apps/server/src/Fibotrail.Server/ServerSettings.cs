namespace Fibotrail.Server;

/// <summary>
/// The settings the server reads from environment variables. See ADR-005.
/// </summary>
public sealed class ServerSettings
{
    public const string ImmichUrlVariable = "IMMICH_URL";
    public const string ImmichApiKeyVariable = "IMMICH_API_KEY";
    public const string ImmichExternalUrlVariable = "IMMICH_EXTERNAL_URL";
    public const string ImmichTimeoutVariable = "IMMICH_TIMEOUT_SECONDS";
    public const string DataDirectoryVariable = "FIBOTRAIL_DATA_DIR";
    public const int DefaultImmichTimeoutSeconds = 30;
    public const int MaxImmichTimeoutSeconds = 600;
    public const string DefaultDataDirectory = "/data";

    public ServerSettings(
        string dataDirectory,
        Uri? immichUrl = null,
        string? immichApiKey = null,
        Uri? immichExternalUrl = null,
        int immichTimeoutSeconds = DefaultImmichTimeoutSeconds)
    {
        DataDirectory = dataDirectory;
        ImmichUrl = immichUrl;
        ImmichApiKey = immichApiKey;
        ImmichExternalUrl = immichExternalUrl;
        ImmichTimeout = TimeSpan.FromSeconds(immichTimeoutSeconds);
    }

    /// <summary>The one writable folder. It holds the database and the cookie protection keys.</summary>
    public string DataDirectory { get; }

    public Uri? ImmichUrl { get; }

    /// <summary>A secret. Never log it and never send it to the browser.</summary>
    public string? ImmichApiKey { get; }

    /// <summary>The address your browser uses for Immich. Only "Open in Immich" links use it.</summary>
    public Uri? ImmichExternalUrl { get; }

    /// <summary>How long one Immich request may take.</summary>
    public TimeSpan ImmichTimeout { get; }

    /// <summary>The cache of images fetched from Immich. It can be deleted at any time.</summary>
    public string ThumbnailDirectory => Path.Combine(DataDirectory, "thumbnails");

    public string DatabasePath => Path.Combine(DataDirectory, "fibotrail.db");

    public string KeysDirectory => Path.Combine(DataDirectory, "keys");

    public string ConnectionString => $"Data Source={DatabasePath}";

    /// <summary>Leaves out the key, so a logged settings object can't leak it.</summary>
    public override string ToString() => $"ServerSettings {{ DataDirectory = {DataDirectory}, ImmichUrl = {ImmichUrl} }}";

    /// <summary>
    /// Reads the settings. Returns the problems found, each naming its variable, and no settings
    /// when a problem exists.
    /// </summary>
    public static (ServerSettings? Settings, IReadOnlyList<string> Problems) Load(
        IConfiguration configuration,
        bool requireImmich)
    {
        var problems = new List<string>();
        var dataDirectory = configuration[DataDirectoryVariable];
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            dataDirectory = DefaultDataDirectory;
        }

        Uri? immichUrl = null;
        var immichApiKey = configuration[ImmichApiKeyVariable];
        if (requireImmich)
        {
            var url = configuration[ImmichUrlVariable];
            if (string.IsNullOrWhiteSpace(url))
            {
                problems.Add($"{ImmichUrlVariable} is missing. Set it in your .env file.");
            }
            else if (!Uri.TryCreate(url, UriKind.Absolute, out immichUrl)
                || (immichUrl.Scheme != Uri.UriSchemeHttp && immichUrl.Scheme != Uri.UriSchemeHttps))
            {
                problems.Add($"{ImmichUrlVariable} isn't a web address. Use a value such as http://immich_server:2283.");
            }

            if (string.IsNullOrWhiteSpace(immichApiKey))
            {
                problems.Add($"{ImmichApiKeyVariable} is missing. Set it in your .env file.");
            }
        }

        Uri? externalUrl = null;
        var external = configuration[ImmichExternalUrlVariable];
        if (!string.IsNullOrWhiteSpace(external)
            && (!Uri.TryCreate(external, UriKind.Absolute, out externalUrl)
                || (externalUrl.Scheme != Uri.UriSchemeHttp && externalUrl.Scheme != Uri.UriSchemeHttps)))
        {
            problems.Add($"{ImmichExternalUrlVariable} isn't a web address. Use a value such as https://photos.example.com, or leave it empty.");
        }

        var timeoutSeconds = DefaultImmichTimeoutSeconds;
        var timeout = configuration[ImmichTimeoutVariable];
        if (!string.IsNullOrWhiteSpace(timeout)
            && (!int.TryParse(timeout, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out timeoutSeconds)
                || timeoutSeconds < 1
                || timeoutSeconds > MaxImmichTimeoutSeconds))
        {
            problems.Add($"{ImmichTimeoutVariable} must be a whole number from 1 to {MaxImmichTimeoutSeconds}. Use a value such as {DefaultImmichTimeoutSeconds}.");
        }

        return problems.Count > 0
            ? (null, problems)
            : (new ServerSettings(dataDirectory, immichUrl, immichApiKey, externalUrl, timeoutSeconds), problems);
    }
}
