using System.Diagnostics;
using Fibotrail.Detection.Parks;

namespace Fibotrail.Detection.Cities;

/// <summary>The places found at one merge radius.</summary>
/// <param name="Time">How long one run of the grouping took, averaged over several runs.</param>
public sealed record RadiusResult(double RadiusKm, IReadOnlyList<VisitedPlace> Cities, IReadOnlyList<VisitedPlace> Parks, TimeSpan Time);

/// <summary>A city at a larger radius that holds more than one city of the smaller radius.</summary>
public sealed record RadiusMerge(string Into, IReadOnlyList<string> Absorbed);

/// <summary>
/// Measures the city merge rule of ADR-015 at several radii, so the radius can be chosen on a real
/// library. Pure: it reads the stops it is given and changes nothing. See docs/adr/ADR-015.
/// </summary>
public static class CityRadiusReport
{
    public const int DefaultRuns = 5;

    /// <summary>Runs the grouping at each radius, in the order given.</summary>
    public static IReadOnlyList<RadiusResult> Measure(IReadOnlyList<PlaceStop> stops, ParkLookup? parks, IReadOnlyList<double> radii, int runs = DefaultRuns)
    {
        ArgumentNullException.ThrowIfNull(stops);
        ArgumentNullException.ThrowIfNull(radii);
        ArgumentOutOfRangeException.ThrowIfLessThan(runs, 1);

        return [.. radii.Select(radius =>
        {
            // The first run also warms the code, so the time is the average of all of them.
            var started = Stopwatch.GetTimestamp();
            IReadOnlyList<VisitedPlace> places = [];
            for (var run = 0; run < runs; run++)
            {
                places = CityPlaces.Find(stops, parks, radius);
            }

            var time = Stopwatch.GetElapsedTime(started) / runs;
            return new RadiusResult(radius, [.. places.Where(place => !place.IsPark)], [.. places.Where(place => place.IsPark)], time);
        })];
    }

    /// <summary>
    /// The cities of the larger radius that absorbed two or more cities of the smaller one, found by
    /// the stop names. Two stops with the same name count as one name.
    /// </summary>
    public static IReadOnlyList<RadiusMerge> MergesBetween(RadiusResult smaller, RadiusResult larger)
    {
        ArgumentNullException.ThrowIfNull(smaller);
        ArgumentNullException.ThrowIfNull(larger);

        var smallerNames = smaller.Cities.Select(city => city.Name).Distinct(StringComparer.Ordinal).ToList();
        return [.. larger.Cities
            .Select(city =>
            {
                var names = (city.StopNames ?? []).ToHashSet(StringComparer.Ordinal);
                names.Add(city.Name);
                var absorbed = smallerNames.Where(names.Contains).ToList();
                return new RadiusMerge(city.Name, absorbed);
            })
            .Where(merge => merge.Absorbed.Count >= 2)
            .OrderBy(merge => merge.Into, StringComparer.Ordinal)];
    }
}
