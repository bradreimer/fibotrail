using System.Globalization;
using System.Text.Json;
using Fibotrail.Detection;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;

namespace Fibotrail.Server.Cli;

/// <summary>
/// <c>fibotrail replay</c>. It runs the detection rules on the scanned photos and compares the
/// result with a list of trips you know you took. It changes nothing in the database.
/// </summary>
public static class ReplayCommand
{
    public const string Name = "replay";
    public const string DefaultFileName = "known-trips.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const string Example = """
        [
          { "name": "Orlando", "start": "2027-03-15", "end": "2027-03-24" }
        ]
        """;

    public static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        IConsoleIo console,
        FibotrailDbContext db,
        HomeLocation home,
        ServerSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(settings);

        if (arguments.Count > 1)
        {
            console.WriteLine("This command takes one argument at most: the path of the list of known trips.");
            return 2;
        }

        var path = arguments.Count == 1 ? arguments[0] : Path.Combine(settings.DataDirectory, DefaultFileName);
        if (!File.Exists(settings.DatabasePath))
        {
            console.WriteLine($"No database at {settings.DatabasePath}. Start Fibotrail and scan your library, then run this command again.");
            return 1;
        }

        if (!File.Exists(path))
        {
            console.WriteLine($"No list of known trips at {path}. Create it with one entry for each trip, like this:");
            console.WriteLine(Example);
            return 1;
        }

        List<KnownTrip>? known;
        try
        {
            known = JsonSerializer.Deserialize<List<KnownTrip>>(await File.ReadAllTextAsync(path, cancellationToken), Json);
        }
        catch (JsonException exception)
        {
            console.WriteLine($"{path} isn't a valid list: {exception.Message} Each entry needs a name, a start, and an end, like this:");
            console.WriteLine(Example);
            return 1;
        }

        if (known is null || known.Count == 0)
        {
            console.WriteLine($"{path} lists no trips.");
            return 1;
        }

        var confirmed = await home.GetAsync(cancellationToken);
        if (confirmed is null)
        {
            console.WriteLine("No home location is confirmed. Open Fibotrail and confirm where home is, then run this command again.");
            return 1;
        }

        var photos = await HomeLocation.LoadPhotosAsync(db, placedOnly: false, cancellationToken);
        var detected = TripDetector.Detect(photos, new GeoPoint(confirmed.Latitude, confirmed.Longitude));
        var result = Replay.Compare(known, detected);

        console.WriteLine($"Photos: {photos.Count}. Home: {confirmed.Name}. Detected trips: {detected.Count}. Known trips: {known.Count}.");
        console.WriteLine(string.Empty);
        console.WriteLine($"Matched: {result.Matched.Count}");
        foreach (var (knownTrip, found) in result.Matched)
        {
            console.WriteLine($"  {knownTrip.Name}, {Dates(knownTrip.Start, knownTrip.End)} -> {Describe(found)}");
        }

        console.WriteLine($"Missed: {result.Missed.Count}");
        foreach (var trip in result.Missed)
        {
            console.WriteLine($"  {trip.Name}, {Dates(trip.Start, trip.End)}");
        }

        console.WriteLine($"Extra: {result.Extra.Count}");
        foreach (var trip in result.Extra)
        {
            console.WriteLine($"  {Describe(trip)}");
        }

        console.WriteLine(string.Empty);
        console.WriteLine(result.PassesGate
            ? $"The gate is met: {result.Matched.Count} of {known.Count} known trips were found. It needs 8 of every 10."
            : $"The gate isn't met: {result.Matched.Count} of {known.Count} known trips were found. It needs 8 of every 10.");
        return result.PassesGate ? 0 : 1;
    }

    private static string Describe(DetectedTrip trip) =>
        $"{trip.Name}, {Dates(trip.StartDate, trip.EndDate)}, {trip.PhotoIds.Count} photos, {trip.Confidence} confidence";

    private static string Dates(DateOnly start, DateOnly end) =>
        $"{start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} to {end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
}
