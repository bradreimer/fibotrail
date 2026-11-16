using Fibotrail.Places;

namespace Fibotrail.Stats.Views;

/// <summary>A record that points at a trip.</summary>
public sealed record TripRecord(TripRef Trip, double Value);

/// <summary>A record that points at a country.</summary>
public sealed record CountryRecord(string CountryId, string Name, int Trips, int Days);

/// <summary>The longest time between the end of one trip and the start of the next.</summary>
public sealed record GapRecord(int Days, TripRef After, TripRef Before);

/// <summary>The "Records" view. Any record is null when there isn't enough data for it.</summary>
public sealed record RecordsResult(
    TripRecord? LongestTrip,
    TripRecord? FarthestTrip,
    TripRecord? MostCountriesTrip,
    CountryRecord? MostVisitedCountry,
    GapRecord? LongestGap);

public static class Records
{
    public static RecordsResult Compute(StatsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        var trips = library.Trips;

        var longest = trips.OrderByDescending(trip => trip.Days).ThenBy(trip => trip.StartDate).FirstOrDefault();
        var farthest = trips.OrderByDescending(trip => trip.FarthestFromHomeKm).ThenBy(trip => trip.StartDate).FirstOrDefault();

        // Countries per trip, from the same visited list every other screen reads.
        var perTrip = library.Countries.SelectMany(country => country.Trips).GroupBy(trip => trip.TripId)
            .Select(group => (TripId: group.Key, Count: group.Count()))
            .OrderByDescending(item => item.Count).ThenBy(item => item.TripId)
            .FirstOrDefault();
        var mostCountries = perTrip.Count == 0 ? null : trips.First(trip => trip.Id == perTrip.TripId);

        // Countries are already ordered by days, then trips. The record goes by trips, then days.
        var mostVisited = library.Countries
            .OrderByDescending(country => country.Trips.Count).ThenByDescending(country => country.Days)
            .ThenBy(country => country.Country.Name, StringComparer.Ordinal)
            .FirstOrDefault();

        GapRecord? gap = null;
        for (var i = 1; i < trips.Count; i++)
        {
            var days = trips[i].StartDate.DayNumber - trips[i - 1].EndDate.DayNumber - 1;
            if (days > 0 && (gap is null || days > gap.Days)) gap = new GapRecord(days, TripsPerYear.ToRef(trips[i - 1]), TripsPerYear.ToRef(trips[i]));
        }

        return new RecordsResult(
            longest is null ? null : new TripRecord(TripsPerYear.ToRef(longest), longest.Days),
            farthest is null || farthest.FarthestFromHomeKm <= 0 ? null : new TripRecord(TripsPerYear.ToRef(farthest), farthest.FarthestFromHomeKm),
            mostCountries is null ? null : new TripRecord(TripsPerYear.ToRef(mostCountries), perTrip.Count),
            mostVisited is null ? null : new CountryRecord(mostVisited.Country.Key, mostVisited.Country.Name, mostVisited.Trips.Count, mostVisited.Days),
            gap);
    }
}
