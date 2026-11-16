namespace Fibotrail.Stats.Views.TravelClock;

/// <summary>One month of the year, across every year.</summary>
/// <param name="Month">1 for January to 12 for December.</param>
/// <param name="Days">Local days of added trips that fall in the month, in any year.</param>
/// <param name="Trips">Added trips with at least one day in the month, newest first.</param>
public sealed record ClockMonth(int Month, int Days, IReadOnlyList<TripRef> Trips);

/// <summary>
/// The "Travel clock" view: days away and trips in each month of the year. Every day of a trip
/// counts, with or without a positioned photo. See docs/specs/travel-clock.md.
/// </summary>
public static class TravelClockView
{
    public static IReadOnlyList<ClockMonth> Compute(StatsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);

        var days = new int[12];
        var trips = Enumerable.Range(0, 12).Select(_ => new List<StatsTrip>()).ToArray();
        foreach (var trip in library.Trips)
        {
            var seen = new HashSet<int>();
            for (var day = trip.StartDate; day <= trip.EndDate; day = day.AddDays(1))
            {
                days[day.Month - 1]++;
                if (seen.Add(day.Month)) trips[day.Month - 1].Add(trip);
            }
        }

        return [.. Enumerable.Range(0, 12).Select(index => new ClockMonth(
            index + 1,
            days[index],
            [.. trips[index].OrderByDescending(trip => trip.StartDate).ThenByDescending(trip => trip.Id).Select(trip => trip.ToRef())]))];
    }
}
