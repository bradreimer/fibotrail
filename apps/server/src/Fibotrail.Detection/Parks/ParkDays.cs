namespace Fibotrail.Detection.Parks;

/// <summary>A photo of an added trip, reduced to its local day and position. Photos on the way aren't passed in.</summary>
public sealed record ParkPhoto(DateOnly Day, double Latitude, double Longitude);

/// <summary>An added trip and its positioned away photos.</summary>
public sealed record ParkTrip(int TripId, string Name, IReadOnlyList<ParkPhoto> Photos);

/// <summary>A trip that visited a park, with how many of its days were there.</summary>
public sealed record ParkVisit(int TripId, string Name, int Days, DateOnly FirstDay);

/// <summary>A collection of open places, such as "Disney parks". Planned places aren't in it.</summary>
public sealed record ParkSet(string Id, string Name, IReadOnlyList<ParkPlace> Places);

/// <summary>The numbers for one visited park.</summary>
/// <param name="Days">Distinct local days, across all added trips, with at least one photo in the park.</param>
/// <param name="Trips">The trips that visit the park, earliest first.</param>
/// <param name="FirstVisit">The earliest local day with a photo in the park.</param>
public sealed record ParkStats(ParkPlace Place, int Days, IReadOnlyList<ParkVisit> Trips, DateOnly FirstVisit);

/// <summary>
/// Counts park days. A trip visits a park when at least the park's <c>MinPhotos</c> photos of that
/// trip are in it. A park day is a distinct local day with a photo in a park that the trip visits,
/// and a day with photos in two parks counts for both. This one function feeds Collections and the
/// Stats Theme parks view, so they can't disagree. See docs/specs/collections.md and ADR-013.
/// </summary>
public static class ParkDays
{
    /// <summary>The visited parks, most days first. Parks that no trip visits aren't listed.</summary>
    public static IReadOnlyList<ParkStats> Count(IEnumerable<ParkTrip> trips, ParkLookup lookup)
    {
        ArgumentNullException.ThrowIfNull(trips);
        ArgumentNullException.ThrowIfNull(lookup);

        var daysByPark = new Dictionary<string, HashSet<DateOnly>>(StringComparer.Ordinal);
        var visitsByPark = new Dictionary<string, List<ParkVisit>>(StringComparer.Ordinal);
        var places = new Dictionary<string, ParkPlace>(StringComparer.Ordinal);

        foreach (var trip in trips)
        {
            var inPark = trip.Photos
                .Select(photo => (Photo: photo, Place: lookup.Find(photo.Latitude, photo.Longitude)))
                .Where(item => item.Place is not null)
                .GroupBy(item => item.Place!.Id, StringComparer.Ordinal);
            foreach (var group in inPark)
            {
                var place = group.First().Place!;
                if (group.Count() < place.MinPhotos) continue;

                var days = group.Select(item => item.Photo.Day).Distinct().Order().ToList();
                places[place.Id] = place;
                if (!daysByPark.TryGetValue(place.Id, out var all)) daysByPark[place.Id] = all = [];
                all.UnionWith(days);
                if (!visitsByPark.TryGetValue(place.Id, out var visits)) visitsByPark[place.Id] = visits = [];
                visits.Add(new ParkVisit(trip.TripId, trip.Name, days.Count, days[0]));
            }
        }

        return [.. places.Values
            .Select(place => new ParkStats(
                place,
                daysByPark[place.Id].Count,
                [.. visitsByPark[place.Id].OrderBy(visit => visit.FirstDay).ThenBy(visit => visit.TripId)],
                daysByPark[place.Id].Min()))
            .OrderByDescending(stats => stats.Days)
            .ThenBy(stats => stats.Place.Name, StringComparer.Ordinal)];
    }
}
