using Fibotrail.Places;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Fibotrail.Stats;
using Fibotrail.Stats.Views;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Stats;

/// <summary>The summary tiles. <c>OutlineCountries</c> is the number of bundled outlines that have an id.</summary>
public sealed record StatsSummary(int Trips, int Countries, int OutlineCountries, int Continents, int DaysAway, double DistanceKm, IReadOnlyList<StatsContinent> ContinentCards);

/// <summary>A continent card.</summary>
/// <param name="Id">The route slug, such as "north-america".</param>
public sealed record StatsContinent(string Id, string Name, int Visited, int Total);

/// <summary>A visited country with its numbers.</summary>
/// <param name="Id">The ISO 3166-1 numeric code, or the name when the outline has none. It is the route key.</param>
public sealed record StatsCountryRow(string Id, string Name, int Trips, int Days);

/// <summary>A continent's page.</summary>
public sealed record StatsContinentPage(string Id, string Name, int Visited, int Total, IReadOnlyList<StatsCountryRow> Countries);

/// <summary>A country's page.</summary>
/// <param name="HasUnpositionedDays">True when a trip in the country has days with no positioned photo, which the days don't count.</param>
public sealed record StatsCountryPage(
    string Id,
    string Name,
    string ContinentId,
    string ContinentName,
    DateOnly FirstVisit,
    int Trips,
    int Days,
    bool HasUnpositionedDays,
    IReadOnlyList<TripRef> TripList);

/// <summary>
/// The Stats screen's data. The host builds the stats library once per request, and every view reads
/// it. See docs/specs/stats-screen.md and ADR-014.
/// </summary>
public static class StatsRoutes
{
    public static void MapStatsRoutes(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var stats = api.MapGroup("/stats");

        stats.MapGet("/summary", async (FibotrailDbContext db, CountryFinder finder, CountryOutlinesCache outlines, CancellationToken cancellationToken) =>
        {
            var library = await LoadAsync(db, finder, outlines, cancellationToken);
            var totals = library.Totals();
            return TypedResults.Ok(new StatsSummary(
                totals.Trips,
                totals.Countries,
                library.OutlineCountries,
                totals.Continents,
                totals.DaysAway,
                totals.DistanceKm,
                [.. library.Progress().Select(progress => new StatsContinent(Continents.Slug(progress.Continent), Continents.Name(progress.Continent), progress.Visited, progress.Total))]));
        }).WithName("GetStatsSummary");

        stats.MapGet("/continents/{continent}", async (string continent, FibotrailDbContext db, CountryFinder finder, CountryOutlinesCache outlines, CancellationToken cancellationToken) =>
        {
            if (Continents.FromSlug(continent) is not { } found) return Results.NotFound();
            var library = await LoadAsync(db, finder, outlines, cancellationToken);
            var countries = library.CountriesOn(found);
            return Results.Ok(new StatsContinentPage(
                Continents.Slug(found),
                Continents.Name(found),
                countries.Count,
                library.ContinentTotals[found],
                [.. countries.Select(Row)]));
        }).WithName("GetStatsContinent").Produces<StatsContinentPage>();

        stats.MapGet("/countries/{id}", async (string id, FibotrailDbContext db, CountryFinder finder, CountryOutlinesCache outlines, CancellationToken cancellationToken) =>
        {
            var library = await LoadAsync(db, finder, outlines, cancellationToken);
            if (library.Country(id) is not { } country) return Results.NotFound();
            var tripIds = country.Trips.Select(trip => trip.TripId).ToHashSet();
            var trips = library.Trips.Where(trip => tripIds.Contains(trip.Id)).OrderByDescending(trip => trip.StartDate).ThenByDescending(trip => trip.Id).ToList();
            return Results.Ok(new StatsCountryPage(
                country.Country.Key,
                country.Country.Name,
                Continents.Slug(country.Continent),
                Continents.Name(country.Continent),
                country.FirstVisit,
                country.Trips.Count,
                country.Days,
                trips.Any(trip => trip.HasUnpositionedDays),
                [.. trips.Select(trip => new TripRef(trip.Id, trip.Name, trip.StartDate, trip.EndDate, trip.Days))]));
        }).WithName("GetStatsCountry").Produces<StatsCountryPage>();

        // The one list of view routes. Adding or removing a view is one line here.
        stats.MapViewRoute("/trips-per-year", "GetStatsTripsPerYear", TripsPerYear.Compute);
        stats.MapViewRoute("/trip-length", "GetStatsTripLength", TripLength.Compute);
        stats.MapViewRoute("/records", "GetStatsRecords", Records.Compute);
    }

    private static void MapViewRoute<T>(this RouteGroupBuilder stats, string pattern, string name, Func<StatsLibrary, T> compute) =>
        stats.MapGet(pattern, async (FibotrailDbContext db, CountryFinder finder, CountryOutlinesCache outlines, CancellationToken cancellationToken) =>
            TypedResults.Ok(compute(await LoadAsync(db, finder, outlines, cancellationToken))))
            .WithName(name);

    private static StatsCountryRow Row(CountryStats country) => new(country.Country.Key, country.Country.Name, country.Trips.Count, country.Days);

    /// <summary>Reads the added trips and their positioned away photos, and builds the library.</summary>
    public static async Task<StatsLibrary> LoadAsync(FibotrailDbContext db, CountryFinder finder, CountryOutlinesCache outlines, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(finder);
        ArgumentNullException.ThrowIfNull(outlines);

        // Only added trips count. A suggested, later, or ignored trip never changes a number.
        var trips = await db.Trips.AsNoTracking().Where(trip => trip.Status == TripStatus.Added).ToListAsync(cancellationToken);
        var ids = trips.Select(trip => trip.Id).ToList();
        var onTheWay = db.OnTheWayPhotos.Select(photo => photo.PhotoId);
        var photos = await (from tripPhoto in db.TripPhotos.AsNoTracking()
                            join photo in db.Photos.AsNoTracking() on tripPhoto.PhotoId equals photo.Id
                            where ids.Contains(tripPhoto.TripId) && photo.Latitude != null && photo.Longitude != null && !onTheWay.Contains(photo.Id)
                            select new { tripPhoto.TripId, photo.LocalTime, Latitude = photo.Latitude!.Value, Longitude = photo.Longitude!.Value })
            .ToListAsync(cancellationToken);
        var byTrip = photos.ToLookup(photo => photo.TripId);

        return StatsLibrary.Build(
            trips.Select(trip => new StatsTrip(
                trip.Id,
                trip.Name,
                trip.StartDate,
                trip.EndDate,
                trip.Days,
                trip.DistanceKm,
                trip.FarthestFromHomeKm,
                [.. DetectionService.ReadStops(trip.StopsJson).Select(stop => (stop.Latitude, stop.Longitude))],
                [.. byTrip[trip.Id].Select(photo => new PhotoPosition(DateOnly.FromDateTime(photo.LocalTime), photo.Latitude, photo.Longitude))])),
            finder,
            outlines.Outlines);
    }
}

/// <summary>The bundled outlines, read once, for the continent totals.</summary>
public sealed class CountryOutlinesCache
{
    public CountryOutlinesCache() => Outlines = CountryOutlines.Read();

    public IReadOnlyList<CountryOutline> Outlines { get; }
}
