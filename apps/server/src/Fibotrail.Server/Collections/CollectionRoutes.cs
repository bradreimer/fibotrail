using Fibotrail.Detection.Parks;
using Fibotrail.Server.Data;
using Fibotrail.Server.Stats;
using Fibotrail.Stats;

namespace Fibotrail.Server.Collections;

/// <summary>A collection on the Collections screen.</summary>
/// <param name="Visited">Open places with at least one visiting trip.</param>
/// <param name="Total">Open places. Planned places aren't counted.</param>
public sealed record CollectionSummary(string Id, string Name, string Description, int Visited, int Total, int TotalDays);

/// <summary>A trip that visited a place, with how many of its days were there.</summary>
public sealed record PlaceTrip(int TripId, string Name, int Days);

/// <summary>A place in a collection.</summary>
/// <param name="Status">"open" or "planned".</param>
public sealed record CollectionPlaceInfo(string Id, string Name, string Group, string Status, int Days, int Trips, DateOnly? FirstVisit, IReadOnlyList<PlaceTrip> TripList);

/// <summary>One collection with its places, grouped by resort in the order of the file.</summary>
/// <param name="Attribution">The credit OpenStreetMap requires wherever the boundaries are shown.</param>
public sealed record CollectionDetail(CollectionSummary Summary, IReadOnlyList<CollectionPlaceInfo> Places, string Attribution);

/// <summary>
/// The Collections screens' data. The park days come from the stats library, the same ones the Stats
/// Theme parks view reads. See docs/specs/collections.md and ADR-013.
/// </summary>
public static class CollectionRoutes
{
    public const string Attribution = "Park boundaries © OpenStreetMap contributors";

    public static void MapCollectionRoutes(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapGet("/collections", async (FibotrailDbContext db, Places.CountryFinder finder, CountryOutlinesCache outlines, Trips.HomeLocation home, CollectionCatalog catalog, CancellationToken cancellationToken) =>
        {
            var library = await StatsRoutes.LoadAsync(db, finder, outlines, home, catalog, cancellationToken);
            return TypedResults.Ok(catalog.Collections.Select(collection => Summarize(collection, library)).ToList());
        }).WithName("GetCollections");

        api.MapGet("/collections/{id}", async (string id, FibotrailDbContext db, Places.CountryFinder finder, CountryOutlinesCache outlines, Trips.HomeLocation home, CollectionCatalog catalog, CancellationToken cancellationToken) =>
        {
            if (catalog.Collections.FirstOrDefault(collection => collection.Id == id) is not { } collection) return Results.NotFound();
            var library = await StatsRoutes.LoadAsync(db, finder, outlines, home, catalog, cancellationToken);
            return Results.Ok(Detail(collection, library));
        }).WithName("GetCollection").Produces<CollectionDetail>();
    }

    public static CollectionSummary Summarize(CollectionDefinition collection, StatsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(library);
        var open = collection.Places.Where(place => !place.Planned).Select(place => place.Park.Id).ToHashSet(StringComparer.Ordinal);
        var parks = library.Parks.Where(stats => open.Contains(stats.Place.Id)).ToList();
        return new CollectionSummary(collection.Id, collection.Name, collection.Description, parks.Count, open.Count, parks.Sum(stats => stats.Days));
    }

    public static CollectionDetail Detail(CollectionDefinition collection, StatsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(library);
        var stats = library.Parks.ToDictionary(item => item.Place.Id, StringComparer.Ordinal);
        return new CollectionDetail(
            Summarize(collection, library),
            [.. collection.Places.Select(place =>
            {
                stats.TryGetValue(place.Park.Id, out var found);
                return new CollectionPlaceInfo(
                    place.Park.Id,
                    place.Park.Name,
                    place.Park.Group,
                    place.Planned ? "planned" : "open",
                    place.Planned ? 0 : found?.Days ?? 0,
                    place.Planned ? 0 : found?.Trips.Count ?? 0,
                    place.Planned ? null : found?.FirstVisit,
                    place.Planned ? [] : [.. (found?.Trips ?? []).Select(visit => new PlaceTrip(visit.TripId, visit.Name, visit.Days))]);
            })],
            Attribution);
    }
}
