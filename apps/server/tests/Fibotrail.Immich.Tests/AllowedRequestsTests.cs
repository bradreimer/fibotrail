using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Fibotrail.Immich.Tests;

/// <summary>Fibotrail never writes to Immich. These tests hold that line. See ADR-001.</summary>
public partial class AllowedRequestsTests
{
    private static readonly Uri BaseUrl = new("http://immich.invalid:2283");

    [Theory]
    [InlineData("GET", "/server/version")]
    [InlineData("POST", "/search/metadata")]
    [InlineData("GET", "/assets/statistics")]
    [InlineData("GET", "/assets/11111111-1111-4111-8111-111111111111/thumbnail")]
    public void The_four_reads_are_allowed(string method, string path)
    {
        Assert.True(AllowedRequests.IsAllowed(new HttpMethod(method), path));
    }

    [Theory]
    [InlineData("DELETE", "/assets")]
    [InlineData("PUT", "/assets")]
    [InlineData("POST", "/assets")]
    [InlineData("PUT", "/assets/11111111-1111-4111-8111-111111111111")]
    [InlineData("GET", "/assets/11111111-1111-4111-8111-111111111111")]
    [InlineData("GET", "/assets/11111111-1111-4111-8111-111111111111/original")]
    [InlineData("POST", "/assets/11111111-1111-4111-8111-111111111111/thumbnail")]
    [InlineData("GET", "/assets/../users/thumbnail")]
    [InlineData("POST", "/albums")]
    [InlineData("POST", "/search/smart")]
    [InlineData("GET", "/search/metadata")]
    [InlineData("POST", "/server/version")]
    [InlineData("GET", "/server/about")]
    [InlineData("POST", "/sync/stream")]
    [InlineData("GET", "/search/metadata/extra")]
    public void Every_other_request_is_refused(string method, string path)
    {
        Assert.False(AllowedRequests.IsAllowed(new HttpMethod(method), path));
    }

    [Theory]
    [InlineData("DELETE", "http://immich.invalid:2283/api/assets")]
    [InlineData("PUT", "http://immich.invalid:2283/api/assets/11111111-1111-4111-8111-111111111111")]
    [InlineData("POST", "http://immich.invalid:2283/api/albums")]
    [InlineData("GET", "http://immich.invalid:2283/server/version")]
    [InlineData("GET", "http://elsewhere.invalid/api/server/version")]
    [InlineData("GET", "https://immich.invalid:2283/api/server/version")]
    public async Task The_guard_stops_a_request_before_it_leaves_and_never_adds_the_key(string method, string address)
    {
        using var immich = new FakeImmichServer();
        using var guard = new ImmichGuardHandler(BaseUrl, FakeImmichServer.Key, immich);
        using var http = new HttpClient(guard);
        using var request = new HttpRequestMessage(new HttpMethod(method), address);

        await Assert.ThrowsAsync<ImmichRequestNotAllowedException>(() => http.SendAsync(request));

        Assert.Empty(immich.Requests);
        Assert.False(request.Headers.Contains(ImmichGuardHandler.ApiKeyHeader));
    }

    [Fact]
    public async Task The_guard_lets_an_allowed_read_through_with_the_key()
    {
        using var immich = new FakeImmichServer();
        using var guard = new ImmichGuardHandler(BaseUrl, FakeImmichServer.Key, immich);
        using var http = new HttpClient(guard);

        var response = await http.GetAsync(new Uri(BaseUrl, "/api/assets/statistics"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(FakeImmichServer.Key, Assert.Single(immich.Requests).ApiKey);
    }

    [Fact]
    public async Task The_guard_works_when_immich_is_served_under_a_path()
    {
        var baseUrl = new Uri("http://proxy.invalid/immich/");
        using var immich = new FakeImmichServer();
        using var guard = new ImmichGuardHandler(baseUrl, FakeImmichServer.Key, immich);
        using var http = new HttpClient(guard);
        using var outside = new HttpRequestMessage(HttpMethod.Get, "http://proxy.invalid/api/server/version");

        await Assert.ThrowsAsync<ImmichRequestNotAllowedException>(() => http.SendAsync(outside));
        await http.GetAsync(new Uri("http://proxy.invalid/immich/api/server/version"));

        Assert.Equal("/immich/api/server/version", Assert.Single(immich.Requests).PathAndQuery);
    }

    [Fact]
    public void The_generated_client_holds_exactly_the_allowed_requests()
    {
        var generated = Path.Combine(RepositoryRoot(), "apps", "server", "src", "Fibotrail.Immich", "Generated");
        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(generated, "kiota-lock.json")));
        var included = settings.RootElement.GetProperty("includePatterns").EnumerateArray().Select(pattern => pattern.GetString()!).ToList();

        Assert.Equal(AllowedRequests.Patterns.Order(), included.Order());

        // Each request builder names its address. No address beyond the list may appear.
        var addresses = Directory.EnumerateFiles(generated, "*RequestBuilder.cs", SearchOption.AllDirectories)
            .SelectMany(file => UrlTemplate().Matches(File.ReadAllText(file)).Select(match => match.Groups[1].Value))
            .Distinct()
            .Where(address => address.Length > 0)
            .ToList();
        var allowedPaths = AllowedRequests.Patterns.Select(pattern => pattern.Split('#')[0]).ToList();
        Assert.All(addresses, address => Assert.Contains(allowedPaths, path => path == address || path.StartsWith(address + "/", StringComparison.Ordinal)));
        Assert.All(allowedPaths, path => Assert.Contains(path, addresses));
    }

    [Fact]
    public void The_generator_script_asks_for_the_same_list()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot(), "scripts", "generate-immich-client.sh"));
        var asked = IncludePath().Matches(script).Select(match => match.Groups[1].Value).ToList();

        Assert.Equal(AllowedRequests.Patterns.Order(), asked.Order());
    }

    [GeneratedRegex("\"\\{\\+baseurl\\}([^\"{?]*(?:\\{id\\}[^\"{?]*)?)")]
    private static partial Regex UrlTemplate();

    [GeneratedRegex("--include-path \"([^\"]+)\"")]
    private static partial Regex IncludePath();

    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "CLAUDE.md")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("The repository root wasn't found.");
    }
}
