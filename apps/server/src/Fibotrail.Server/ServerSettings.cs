namespace Fibotrail.Server;

/// <summary>
/// The settings the server reads from environment variables. See ADR-005.
/// </summary>
public sealed class ServerSettings
{
    public const string ImmichUrlVariable = "IMMICH_URL";
    public const string ImmichApiKeyVariable = "IMMICH_API_KEY";
    public const string DataDirectoryVariable = "FIBOTRAIL_DATA_DIR";
    public const string DefaultDataDirectory = "/data";

    public ServerSettings(string dataDirectory, Uri? immichUrl = null, string? immichApiKey = null)
    {
        DataDirectory = dataDirectory;
        ImmichUrl = immichUrl;
        ImmichApiKey = immichApiKey;
    }

    /// <summary>The one writable folder. It holds the database and the cookie protection keys.</summary>
    public string DataDirectory { get; }

    public Uri? ImmichUrl { get; }

    /// <summary>A secret. Never log it and never send it to the browser.</summary>
    public string? ImmichApiKey { get; }

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

        return problems.Count > 0
            ? (null, problems)
            : (new ServerSettings(dataDirectory, immichUrl, immichApiKey), problems);
    }
}
