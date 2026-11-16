namespace Fibotrail.Places;

/// <summary>An added trip's stops, as the life map reads them.</summary>
/// <param name="Stops">The stops of the trip, as stored with it. Photos on the way aren't among them.</param>
public sealed record TripStops(int TripId, string Name, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<(double Latitude, double Longitude)> Stops);

/// <summary>A trip that visited a country.</summary>
public sealed record VisitedTrip(int TripId, string Name, DateOnly StartDate, DateOnly EndDate);

/// <summary>A country that at least one added trip visited.</summary>
/// <param name="FirstVisit">The start date of the earliest trip that has a stop in the country.</param>
/// <param name="Trips">The trips with a stop in the country, earliest first.</param>
public sealed record VisitedCountry(Country Country, DateOnly FirstVisit, IReadOnlyList<VisitedTrip> Trips);

/// <summary>
/// Which countries the trips visited. A country is visited when a trip has a stop in it. A trip with
/// stops in two countries counts for both. Pure: the caller passes the added trips only, and the
/// finder. See docs/specs/life-map.md, "What counts as visited", and ADR-011.
/// </summary>
public static class VisitedCountries
{
    /// <summary>The visited countries, the earliest first visit first.</summary>
    public static IReadOnlyList<VisitedCountry> Find(IEnumerable<TripStops> trips, CountryFinder finder, double nearestKm = CountryFinder.DefaultNearestKm)
    {
        ArgumentNullException.ThrowIfNull(trips);
        ArgumentNullException.ThrowIfNull(finder);

        var byCountry = new Dictionary<string, (Country Country, List<VisitedTrip> Trips)>(StringComparer.Ordinal);
        foreach (var trip in trips)
        {
            // A trip counts once for a country, however many of its stops are there.
            var countries = trip.Stops
                .Select(stop => finder.Find(stop.Latitude, stop.Longitude, nearestKm)?.Country)
                .OfType<Country>()
                .DistinctBy(country => country.Key);
            foreach (var country in countries)
            {
                if (!byCountry.TryGetValue(country.Key, out var entry))
                {
                    entry = (country, []);
                    byCountry[country.Key] = entry;
                }

                entry.Trips.Add(new VisitedTrip(trip.TripId, trip.Name, trip.StartDate, trip.EndDate));
            }
        }

        return [.. byCountry.Values
            .Select(entry =>
            {
                var ordered = entry.Trips.OrderBy(trip => trip.StartDate).ThenBy(trip => trip.TripId).ToList();
                return new VisitedCountry(entry.Country, ordered[0].StartDate, ordered);
            })
            .OrderBy(visited => visited.FirstVisit)
            .ThenBy(visited => visited.Country.Name, StringComparer.Ordinal)];
    }
}
