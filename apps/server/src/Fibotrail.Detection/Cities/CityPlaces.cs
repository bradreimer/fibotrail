using Fibotrail.Detection.Parks;

namespace Fibotrail.Detection.Cities;

/// <summary>A stop of an added trip, with the trip it belongs to.</summary>
/// <param name="Name">The place name stored with the stop, from Immich, or null.</param>
public sealed record PlaceStop(int TripId, string TripName, DateOnly TripStart, DateOnly TripEnd, string? Name, double Latitude, double Longitude, int PhotoCount);

/// <summary>A trip that visited a place.</summary>
public sealed record PlaceTrip(int TripId, string Name, DateOnly StartDate, DateOnly EndDate);

/// <summary>A place you've been: a merged city, or a theme park.</summary>
/// <param name="ParkId">The id of the park when this is a park place, otherwise null.</param>
/// <param name="Trips">The distinct added trips with a stop in the place, earliest first.</param>
public sealed record VisitedPlace(string Name, double Latitude, double Longitude, string? ParkId, DateOnly FirstVisit, IReadOnlyList<PlaceTrip> Trips)
{
    public bool IsPark => ParkId is not null;
}

/// <summary>
/// Works out the places of added trips. Stops within a radius merge into one city, named by the stop
/// with the most photos. A stop inside an open park is a place of its own, never merged into a city.
/// Pure: the caller passes the stops of added trips only. See ADR-015.
/// </summary>
public static class CityPlaces
{
    /// <summary>How near two stops must be to merge into one city. A first guess, to be checked on real data.</summary>
    public const double MergeRadiusKm = 25;

    private const string Unnamed = "Unnamed place";

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
            // The stop with the most photos names the place. The earliest wins a tie.
            var main = group.Select((stop, index) => (Stop: stop, Index: index))
                .OrderByDescending(item => item.Stop.PhotoCount).ThenBy(item => item.Index)
                .First().Stop;
            var name = !string.IsNullOrWhiteSpace(main.Name)
                ? main.Name!
                : group.Select(stop => stop.Name).FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate)) ?? Unnamed;
            places.Add(Build(name, main, group, null));
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
        return new VisitedPlace(name, at.Latitude, at.Longitude, parkId, trips[0].StartDate, trips);
    }
}
