using Fibotrail.Detection.Parks;

namespace Fibotrail.Detection.Cities;

/// <summary>A stop of an added trip, with the trip it belongs to.</summary>
/// <param name="Name">The place name stored with the stop, from Immich, or null.</param>
/// <param name="Country">The name of the country the stop is in, or null when not known. Used to spot a "city" that is only the country's name.</param>
public sealed record PlaceStop(int TripId, string TripName, DateOnly TripStart, DateOnly TripEnd, string? Name, double Latitude, double Longitude, int PhotoCount, string? Country = null)
{
    /// <summary>
    /// True when the name can name a city. Immich leaves the city empty in open country and then
    /// gives the country, such as "Canada" or "United States", so a blank name, or a name that is the
    /// country's name or its first words ("United States" for "United States of America"), doesn't count.
    /// </summary>
    public bool HasCityName =>
        !string.IsNullOrWhiteSpace(Name)
        && (string.IsNullOrWhiteSpace(Country)
            || !(Country.Equals(Name.Trim(), StringComparison.OrdinalIgnoreCase)
                || Country.StartsWith(Name.Trim() + " ", StringComparison.OrdinalIgnoreCase)));
}

/// <summary>A trip that visited a place.</summary>
public sealed record PlaceTrip(int TripId, string Name, DateOnly StartDate, DateOnly EndDate);

/// <summary>A place you've been: a merged city, or a theme park.</summary>
/// <param name="ParkId">The id of the park when this is a park place, otherwise null.</param>
/// <param name="Trips">The distinct added trips with a stop in the place, earliest first.</param>
/// <param name="StopNames">The names of the stops that make up the place, most photos first. Used by the radius report.</param>
public sealed record VisitedPlace(string Name, double Latitude, double Longitude, string? ParkId, DateOnly FirstVisit, IReadOnlyList<PlaceTrip> Trips, IReadOnlyList<string>? StopNames = null)
{
    public bool IsPark => ParkId is not null;
}

/// <summary>
/// Works out the places of added trips. Stops within a radius merge into one city, named by the stop
/// name found on the most trips, then the most photos. A group with no stop that has a city name,
/// such as a stop in open country, isn't a city. A stop inside an open park is a place of its own, never merged into a city.
/// Pure: the caller passes the stops of added trips only. See ADR-015.
/// </summary>
public static class CityPlaces
{
    /// <summary>How near two stops must be to merge into one city. A first guess, to be checked on real data.</summary>
    public const double MergeRadiusKm = 25;

    public static IReadOnlyList<VisitedPlace> Find(IEnumerable<PlaceStop> stops, ParkLookup? parks = null, double radiusKm = MergeRadiusKm)
    {
        ArgumentNullException.ThrowIfNull(stops);

        // Oldest trip first, so the same input always gives the same groups.
        var ordered = stops.Select((stop, index) => (Stop: stop, Index: index))
            .OrderBy(item => item.Stop.TripStart).ThenBy(item => item.Stop.TripId).ThenBy(item => item.Index)
            .Select(item => item.Stop)
            .ToList();

        var places = new List<VisitedPlace>();
        var cityStops = new List<PlaceStop>();
        var parkStops = new Dictionary<string, (ParkPlace Park, List<PlaceStop> Stops)>(StringComparer.Ordinal);
        foreach (var stop in ordered)
        {
            if (parks?.Find(stop.Latitude, stop.Longitude) is { } park)
            {
                if (!parkStops.TryGetValue(park.Id, out var entry)) parkStops[park.Id] = entry = (park, []);
                entry.Stops.Add(stop);
            }
            else
            {
                cityStops.Add(stop);
            }
        }

        foreach (var (park, members) in parkStops.Values)
        {
            places.Add(Build(park.Name, members[0], members, park.Id));
        }

        foreach (var group in Geo.Group(cityStops, stop => new GeoPoint(stop.Latitude, stop.Longitude), radiusKm))
        {
            // Stops with no city name still join a nearby city, but they never name one.
            var named = group.Where(stop => stop.HasCityName).ToList();
            if (named.Count == 0) continue;

            // The name found on the most distinct trips wins, because one long stay shouldn't
            // outvote a name you keep coming back to. Then the most photos, then the earliest.
            var best = named
                .Select((stop, index) => (Stop: stop, Index: index))
                .GroupBy(item => item.Stop.Name!.Trim(), StringComparer.Ordinal)
                .OrderByDescending(names => names.Select(item => item.Stop.TripId).Distinct().Count())
                .ThenByDescending(names => names.Sum(item => item.Stop.PhotoCount))
                .ThenBy(names => names.Min(item => item.Index))
                .First();
            var main = best.OrderByDescending(item => item.Stop.PhotoCount).ThenBy(item => item.Index).First().Stop;
            places.Add(Build(best.Key, main, group, null));
        }

        return [.. places.OrderBy(place => place.FirstVisit).ThenBy(place => place.Name, StringComparer.Ordinal)];
    }

    private static VisitedPlace Build(string name, PlaceStop at, IEnumerable<PlaceStop> members, string? parkId)
    {
        var trips = members
            .GroupBy(stop => stop.TripId)
            .Select(group => group.First())
            .OrderBy(stop => stop.TripStart).ThenBy(stop => stop.TripId)
            .Select(stop => new PlaceTrip(stop.TripId, stop.TripName, stop.TripStart, stop.TripEnd))
            .ToList();
        var memberList = members.ToList();
        var stopNames = memberList
            .Where(stop => stop.HasCityName)
            .GroupBy(stop => stop.Name!.Trim(), StringComparer.Ordinal)
            .OrderByDescending(group => group.Sum(stop => stop.PhotoCount))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .ToList();
        return new VisitedPlace(name, at.Latitude, at.Longitude, parkId, trips[0].StartDate, trips, stopNames);
    }
}
