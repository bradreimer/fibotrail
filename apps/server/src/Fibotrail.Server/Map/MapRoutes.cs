using Fibotrail.Places;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Map;

/// <summary>A trip that visited a country.</summary>
public sealed record MapTripRef(int Id, string Name, DateOnly StartDate, DateOnly EndDate);

/// <summary>A visited country.</summary>
/// <param name="Id">The ISO 3166-1 numeric code that the web app's map features carry. Null for a few territories.</param>
/// <param name="FirstVisit">The start date of the earliest added trip with a stop in the country.</param>
/// <param name="Trips">The added trips with a stop in the country, earliest first.</param>
public sealed record MapCountry(string? Id, string Name, DateOnly FirstVisit, IReadOnlyList<MapTripRef> Trips);

public sealed record MapPoint(double Latitude, double Longitude);

/// <summary>The route of one added trip: its stops, in order.</summary>
public sealed record MapRoute(int TripId, IReadOnlyList<MapPoint> Points);

/// <summary>The numbers of the summary strip.</summary>
/// <param name="DaysAway">The sum of the days of the added trips.</param>
/// <param name="DistanceKm">The sum of the distances of the added trips, as their pages show them.</param>
public sealed record MapTotals(int Countries, int Trips, int DaysAway, double DistanceKm);

/// <summary>Everything the Map screen shows.</summary>
public sealed record MapInfo(IReadOnlyList<MapCountry> Countries, IReadOnlyList<MapRoute> Routes, MapTotals Totals);

/// <summary>
/// The life map's data. The countries are worked out from the stored stops of added trips and the
/// bundled outlines, each time. See docs/specs/life-map.md and ADR-011.
/// </summary>
public static class MapRoutes
{
    public static void MapMapRoutes(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapGet("/map", async (FibotrailDbContext db, CountryFinder finder, CancellationToken cancellationToken) =>
            TypedResults.Ok(await BuildAsync(db, finder, cancellationToken)))
            .WithName("GetMap");
    }

    public static async Task<MapInfo> BuildAsync(FibotrailDbContext db, CountryFinder finder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(finder);

        // Only added trips count. A suggested, later, or ignored trip never fills a country.
        var trips = await db.Trips.AsNoTracking()
            .Where(trip => trip.Status == TripStatus.Added)
            .OrderBy(trip => trip.StartDate)
            .ThenBy(trip => trip.Id)
            .ToListAsync(cancellationToken);

        // The stored stops leave out photos on the way, so a layover country doesn't fill in.
        var stops = trips.ToDictionary(trip => trip.Id, trip => DetectionService.ReadStops(trip.StopsJson));
        var visited = VisitedCountries.Find(
            trips.Select(trip => new TripStops(trip.Id, trip.Name, trip.StartDate, trip.EndDate, [.. stops[trip.Id].Select(stop => (stop.Latitude, stop.Longitude))])),
            finder);

        return new MapInfo(
            [.. visited.Select(country => new MapCountry(
                country.Country.Id,
                country.Country.Name,
                country.FirstVisit,
                [.. country.Trips.Select(trip => new MapTripRef(trip.TripId, trip.Name, trip.StartDate, trip.EndDate))]))],
            [.. trips.Select(trip => new MapRoute(trip.Id, [.. stops[trip.Id].Select(stop => new MapPoint(stop.Latitude, stop.Longitude))]))],
            new MapTotals(visited.Count, trips.Count, trips.Sum(trip => trip.Days), Math.Round(trips.Sum(trip => trip.DistanceKm), 1)));
    }
}
