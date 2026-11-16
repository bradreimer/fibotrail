using Fibotrail.Places;

namespace Fibotrail.Stats.Views.TripSpiral;

/// <summary>An added trip on the spiral.</summary>
/// <param name="ContinentId">The continent's route slug, such as "europe", or null when no stop is in a country.</param>
/// <param name="ContinentName">The continent's name, or null.</param>
public sealed record SpiralTrip(TripRef Trip, string? ContinentId, string? ContinentName);

/// <summary>The "Fibonacci spiral of trips" view: every added trip, oldest first. See docs/specs/trip-spiral.md.</summary>
public static class TripSpiralView
{
    public static IReadOnlyList<SpiralTrip> Compute(StatsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        return [.. library.Trips
            .OrderBy(trip => trip.StartDate).ThenBy(trip => trip.Id)
            .Select(trip =>
            {
                var continent = library.ContinentOfTrip(trip.Id);
                return new SpiralTrip(trip.ToRef(), continent is { } c ? Continents.Slug(c) : null, continent is { } n ? Continents.Name(n) : null);
            })];
    }
}
