namespace Fibotrail.Places;

/// <summary>A detail country with the regions that added trips visited.</summary>
/// <param name="Country">The detail country, as a visited country.</param>
/// <param name="Total">How many regions the country has: 13 for Canada and 51 for the USA.</param>
/// <param name="Regions">The visited regions, the earliest first visit first.</param>
public sealed record VisitedDetailCountry(DetailCountry Country, int Total, IReadOnlyList<VisitedCountry> Regions);

/// <summary>
/// Which regions of the detail countries the trips visited. A region is visited when a trip has a
/// stop in it, by the same rule as a country, and the stop must be in the region's own country. A
/// trip with stops in two regions counts for both. Pure: the caller passes the added trips only, and
/// the finders. See docs/specs/life-map.md, "Detail regions", and ADR-012.
/// </summary>
public static class VisitedRegions
{
    /// <summary>
    /// The visited detail countries, in the order the finder lists them. A detail country is included
    /// when a stop is in it, even when none of its stops falls in a region.
    /// </summary>
    public static IReadOnlyList<VisitedDetailCountry> Find(
        IEnumerable<TripStops> trips,
        CountryFinder countries,
        RegionFinder regions,
        double nearestKm = CountryFinder.DefaultNearestKm)
    {
        ArgumentNullException.ThrowIfNull(trips);
        ArgumentNullException.ThrowIfNull(countries);
        ArgumentNullException.ThrowIfNull(regions);

        var tripList = trips.ToList();

        // For each detail country, the trips that visited it, and for each region the trips that visited that.
        var visitedCountryIds = new HashSet<string>(StringComparer.Ordinal);
        var byRegion = new Dictionary<string, (Country Region, List<VisitedTrip> Trips)>(StringComparer.Ordinal);
        var regionCountry = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var trip in tripList)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stop in trip.Stops)
            {
                var country = countries.Find(stop.Latitude, stop.Longitude, nearestKm)?.Country;
                if (country?.Id is null || regions.Total(country.Id) == 0)
                {
                    continue;
                }

                visitedCountryIds.Add(country.Id);
                var region = regions.Find(country.Id, stop.Latitude, stop.Longitude, nearestKm)?.Country;
                if (region is null || !seen.Add(region.Key))
                {
                    continue;
                }

                if (!byRegion.TryGetValue(region.Key, out var entry))
                {
                    entry = (region, []);
                    byRegion[region.Key] = entry;
                    regionCountry[region.Key] = country.Id;
                }

                entry.Trips.Add(new VisitedTrip(trip.TripId, trip.Name, trip.StartDate, trip.EndDate));
            }
        }

        return [.. regions.DetailCountries
            .Where(country => visitedCountryIds.Contains(country.Id))
            .Select(country => new VisitedDetailCountry(
                country,
                regions.Total(country.Id),
                [.. byRegion
                    .Where(pair => regionCountry[pair.Key] == country.Id)
                    .Select(pair =>
                    {
                        var ordered = pair.Value.Trips.OrderBy(trip => trip.StartDate).ThenBy(trip => trip.TripId).ToList();
                        return new VisitedCountry(pair.Value.Region, ordered[0].StartDate, ordered);
                    })
                    .OrderBy(visited => visited.FirstVisit)
                    .ThenBy(visited => visited.Country.Name, StringComparer.Ordinal)]))];
    }
}
