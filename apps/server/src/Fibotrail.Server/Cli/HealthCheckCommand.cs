namespace Fibotrail.Server.Cli;

/// <summary>
/// <c>fibotrail healthcheck</c>. The Docker health check runs it inside the container, because the
/// image has no curl or wget.
/// </summary>
public static class HealthCheckCommand
{
    public const string Name = "healthcheck";

    public static async Task<int> RunAsync(HttpClient client, Uri address, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.GetAsync(address, cancellationToken);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
