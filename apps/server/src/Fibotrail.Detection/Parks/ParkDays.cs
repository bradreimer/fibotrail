namespace Fibotrail.Detection.Parks;

/// <summary>A photo of an added trip, reduced to its local day and position. Photos on the way aren't passed in.</summary>
public sealed record ParkPhoto(DateOnly Day, double Latitude, double Longitude);

/// <summary>An added trip and its positioned away photos.</summary>
/// <param name="StartDate">The first day of the trip. A park you marked by hand has no photo, so it uses this day as its first visit.</param>
/// <param name="MarkedParks">The ids of the parks you marked as visited on this trip, for a trip whose photos have no position.</param>
public sealed record ParkTrip(int TripId, string Name, IReadOnlyList<ParkPhoto> Photos, DateOnly StartDate = default, IReadOnlyList<string>? MarkedParks = null);

/// <summary>A trip that visited a park, with how many of its days were there.</summary>
/// <param name="Marked">True when you marked the visit by hand and no photo of the trip is in the park. Its days are 0.</param>
public sealed record ParkVisit(int TripId, string Name, int Days, DateOnly FirstDay, bool Marked = false);

/// <summary>The numbers for one visited park.</summary>
/// <param name="Days">Distinct local days, across all added trips, with at least one photo in the park.</param>
/// <param name="Trips">The trips that visit the park, earliest first.</param>
/// <param name="FirstVisit">The earliest local day with a photo in the park.</param>
public sealed record ParkStats(ParkPlace Place, int Days, IReadOnlyList<ParkVisit> Trips, DateOnly FirstVisit);

/// <summary>
/// Counts park days. A trip visits a park when at least the park's <c>MinPhotos</c> photos of that
/// trip are in it. A park day is a distinct local day with a photo in a park that the trip visits,
/// and a day with photos in two parks counts for both. This one function feeds Collections, so every
/// screen that shows park days agrees. See docs/specs/collections.md and ADR-013.
/// </summary>
public static class ParkDays
{
    /// <summary>
    /// The parks that photos place a trip in: at least the park's <c>MinPhotos</c> photos are in it.
    /// Each park comes with the distinct local days of those photos, oldest first.
    /// </summary>
    public static IReadOnlyList<(ParkPlace Place, IReadOnlyList<DateOnly> Days)> VisitedByPhotos(IEnumerable<ParkPhoto> photos, ParkLookup lookup)
    {
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(lookup);
        return [.. photos
            .Select(photo => (Photo: photo, Place: lookup.Find(photo.Latitude, photo.Longitude)))
            .Where(item => item.Place is not null)
            .GroupBy(item => item.Place!.Id, StringComparer.Ordinal)
            .Where(group => group.Count() >= group.First().Place!.MinPhotos)
            .Select(group => (group.First().Place!, (IReadOnlyList<DateOnly>)[.. group.Select(item => item.Photo.Day).Distinct().Order()]))];
    }

    /// <summary>The visited parks, most days first. Parks that no trip visits aren't listed.</summary>
    public static IReadOnlyList<ParkStats> Count(IEnumerable<ParkTrip> trips, ParkLookup lookup)
    {
        ArgumentNullException.ThrowIfNull(trips);
        ArgumentNullException.ThrowIfNull(lookup);

        var daysByPark = new Dictionary<string, HashSet<DateOnly>>(StringComparer.Ordinal);
        var visitsByPark = new Dictionary<string, List<ParkVisit>>(StringComparer.Ordinal);
        var places = new Dictionary<string, ParkPlace>(StringComparer.Ordinal);

        var firstByPark = new Dictionary<string, DateOnly>(StringComparer.Ordinal);
        foreach (var trip in trips)
        {
            var visitedByPhotos = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (place, days) in VisitedByPhotos(trip.Photos, lookup))
            {
                visitedByPhotos.Add(place.Id);
                places[place.Id] = place;
                if (!daysByPark.TryGetValue(place.Id, out var all)) daysByPark[place.Id] = all = [];
                all.UnionWith(days);
                if (!visitsByPark.TryGetValue(place.Id, out var visits)) visitsByPark[place.Id] = visits = [];
                visits.Add(new ParkVisit(trip.TripId, trip.Name, days.Count, days[0]));
                firstByPark[place.Id] = firstByPark.TryGetValue(place.Id, out var first) && first < days[0] ? first : days[0];
            }

            // A park you marked by hand counts the trip and adds no day: with no position, no day is known.
            foreach (var id in trip.MarkedParks ?? [])
            {
                if (visitedByPhotos.Contains(id) || lookup.Place(id) is not { } marked) continue;
                places[id] = marked;
                if (!visitsByPark.TryGetValue(id, out var visits)) visitsByPark[id] = visits = [];
                visits.Add(new ParkVisit(trip.TripId, trip.Name, 0, trip.StartDate, Marked: true));
                firstByPark[id] = firstByPark.TryGetValue(id, out var first) && first < trip.StartDate ? first : trip.StartDate;
            }
        }

        return [.. places.Values
            .Select(place => new ParkStats(
                place,
                daysByPark.TryGetValue(place.Id, out var counted) ? counted.Count : 0,
                [.. visitsByPark[place.Id].OrderBy(visit => visit.FirstDay).ThenBy(visit => visit.TripId)],
                firstByPark[place.Id]))
            .OrderByDescending(stats => stats.Days)
            .ThenBy(stats => stats.Place.Name, StringComparer.Ordinal)];
    }
}
