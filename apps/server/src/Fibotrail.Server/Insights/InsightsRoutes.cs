using Fibotrail.Places;
using Fibotrail.Server.Collections;
using Fibotrail.Server.Data;
using Fibotrail.Server.Stats;

namespace Fibotrail.Server.Insights;

/// <summary>The totals at the top of the Insights screen.</summary>
/// <param name="OutlineCountries">The number of bundled outlines that have an id, the base of the countries share.</param>
public sealed record InsightsSummary(int Trips, int Countries, int OutlineCountries, int Continents, int Cities, int DaysAway, double DistanceKm);

/// <summary>
/// The Insights screen's totals. The collections below them come from
/// <see cref="CollectionRoutes"/>. See docs/specs/insights.md.
/// </summary>
public static class InsightsRoutes
{
    public static void MapInsightsRoutes(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapGet("/insights/summary", async (FibotrailDbContext db, CountryFinder finder, CountryOutlinesCache outlines, CollectionCatalog catalog, CancellationToken cancellationToken) =>
        {
            var library = await StatsLibraryLoader.LoadAsync(db, finder, outlines, catalog, cancellationToken);
            var totals = library.Totals();
            return TypedResults.Ok(new InsightsSummary(
                totals.Trips,
                totals.Countries,
                library.OutlineCountries,
                totals.Continents,
                library.CityCount,
                totals.DaysAway,
                totals.DistanceKm));
        }).WithName("GetInsightsSummary");
    }
}
