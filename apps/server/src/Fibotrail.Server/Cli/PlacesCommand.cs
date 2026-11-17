using System.Globalization;
using Fibotrail.Detection;
using Fibotrail.Detection.Cities;
using Fibotrail.Places;
using Fibotrail.Server.Collections;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Cli;

/// <summary>
/// <c>fibotrail places</c>. It runs the city merge rule of ADR-015 at 10, 25, and 50 km on the
/// stops stored with each added trip, so the radius can be chosen on a real library. It changes
/// nothing. The output holds place names, counts, and times only: no positions, no photo IDs, and no
/// Immich key, so it can be pasted into a conversation.
/// </summary>
public static class PlacesCommand
{
    public const string Name = "places";

    private static readonly double[] Radii = [10, CityPlaces.MergeRadiusKm, 50];

    public static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        IConsoleIo console,
        FibotrailDbContext db,
        ServerSettings settings,
        CollectionCatalog catalog,
        CountryFinder finder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(finder);

        if (arguments.Count > 0)
        {
            console.WriteLine("This command takes no arguments.");
            return 2;
        }

        if (!File.Exists(settings.DatabasePath))
        {
            console.WriteLine($"No database at {settings.DatabasePath}. Start Fibotrail and scan your library, then run this command again.");
            return 1;
        }

        var trips = await db.Trips
            .AsNoTracking()
            .Where(trip => trip.Status == TripStatus.Added)
            .Select(trip => new { trip.Id, trip.Name, trip.StartDate, trip.EndDate, trip.StopsJson })
            .ToListAsync(cancellationToken);

        if (trips.Count == 0)
        {
            console.WriteLine("No trips are added yet. Add trips from the inbox in Fibotrail, then run this command again.");
            return 0;
        }

        var stops = new List<PlaceStop>();
        var unreadable = 0;
        foreach (var trip in trips.OrderBy(trip => trip.StartDate).ThenBy(trip => trip.Id))
        {
            try
            {
                stops.AddRange(DetectionService.ReadStops(trip.StopsJson).Select(stop =>
                    new PlaceStop(trip.Id, trip.Name, trip.StartDate, trip.EndDate, stop.Place, stop.Latitude, stop.Longitude, stop.PhotoCount, finder.Find(stop.Latitude, stop.Longitude)?.Country.Name)));
            }
            catch (System.Text.Json.JsonException)
            {
                unreadable++;
            }
        }

        var results = CityRadiusReport.Measure(stops, catalog.Lookup, Radii);
        console.WriteLine($"Trips: {trips.Count}");
        console.WriteLine($"Stops: {stops.Count}");
        if (unreadable > 0)
        {
            console.WriteLine($"Trips whose stops couldn't be read: {unreadable}");
        }

        console.WriteLine(string.Empty);
        foreach (var result in results)
        {
            console.WriteLine($"{Km(result.RadiusKm)}: {result.Cities.Count} cities, {result.Parks.Count} parks, {result.Time.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)} ms");
        }

        var chosen = results.First(result => result.RadiusKm == CityPlaces.MergeRadiusKm);
        console.WriteLine(string.Empty);
        console.WriteLine($"Cities at {Km(chosen.RadiusKm)}, with the stops merged into each:");
        foreach (var city in chosen.Cities.OrderBy(city => city.Name, StringComparer.Ordinal))
        {
            var names = city.StopNames ?? [];
            console.WriteLine(names.Count > 1 ? $"  {city.Name}: {string.Join(", ", names)}" : $"  {city.Name}");
        }

        for (var index = 1; index < results.Count; index++)
        {
            var merges = CityRadiusReport.MergesBetween(results[index - 1], results[index]);
            console.WriteLine(string.Empty);
            console.WriteLine($"Cities that merge going from {Km(results[index - 1].RadiusKm)} to {Km(results[index].RadiusKm)}:");
            if (merges.Count == 0)
            {
                console.WriteLine("  None.");
            }

            foreach (var merge in merges)
            {
                console.WriteLine($"  {merge.Into}: {string.Join(", ", merge.Absorbed)}");
            }
        }

        return 0;
    }

    private static string Km(double radius) => string.Create(CultureInfo.InvariantCulture, $"{radius:0} km");
}
