using Fibotrail.Detection;

namespace Fibotrail.Stats.Views.DistanceRings;

/// <summary>An added trip placed by how far and in which direction its farthest stop is from home.</summary>
/// <param name="DistanceKm">The great-circle distance from home to the trip's farthest stop.</param>
/// <param name="BearingDegrees">The initial bearing from home to that stop, 0 for north, clockwise.</param>
public sealed record RingTrip(TripRef Trip, double DistanceKm, double BearingDegrees);

/// <summary>
/// The "Distance rings" view. It returns distances and bearings only, never the home coordinates.
/// A trip with no stop is left out. With no home location, no trip can be placed. See
/// docs/specs/distance-rings.md.
/// </summary>
public static class DistanceRingsView
{
    public static IReadOnlyList<RingTrip> Compute(StatsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (library.Home is not { } home) return [];

        return [.. library.Trips
            .Where(trip => trip.Stops.Count > 0)
            .Select(trip =>
            {
                var farthest = trip.Stops
                    .Select(stop => new GeoPoint(stop.Latitude, stop.Longitude))
                    .MaxBy(stop => Geo.DistanceKm(home, stop));
                return new RingTrip(trip.ToRef(), Math.Round(Geo.DistanceKm(home, farthest), 1), Math.Round(Geo.BearingDegrees(home, farthest), 1));
            })
            .OrderBy(ring => ring.DistanceKm)
            .ThenBy(ring => ring.Trip.Id)];
    }
}
