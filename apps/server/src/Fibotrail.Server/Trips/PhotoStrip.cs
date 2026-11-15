namespace Fibotrail.Server.Trips;

/// <summary>Chooses a few photos that stand for a whole trip.</summary>
public static class PhotoStrip
{
    /// <summary>
    /// Picks up to <paramref name="count"/> photos, spread evenly across the trip's days. With
    /// more days than photos to pick, the days are evenly spaced. With fewer, each day gives
    /// several photos, spread across that day.
    /// </summary>
    /// <param name="photosByTime">The trip's photos, oldest first.</param>
    /// <returns>The chosen photos, oldest first.</returns>
    public static IReadOnlyList<T> Pick<T>(IReadOnlyList<T> photosByTime, Func<T, DateOnly> day, int count)
    {
        ArgumentNullException.ThrowIfNull(photosByTime);
        ArgumentNullException.ThrowIfNull(day);
        if (photosByTime.Count <= count)
        {
            return photosByTime;
        }

        var days = photosByTime.GroupBy(day).Select(group => group.ToList()).ToList();
        var picked = new List<T>();
        if (days.Count >= count)
        {
            // One photo from each of `count` days, from the first day to the last.
            for (var slot = 0; slot < count; slot++)
            {
                var index = count == 1 ? 0 : (int)Math.Round(slot * (days.Count - 1) / (double)(count - 1));
                picked.Add(days[index][0]);
            }

            return picked;
        }

        // Fewer days than slots: share the slots between the days, and give spare slots to the days with photos left.
        var quota = days.Select((_, index) => (count / days.Count) + (index < count % days.Count ? 1 : 0)).ToArray();
        var spare = 0;
        for (var index = 0; index < days.Count; index++)
        {
            if (quota[index] > days[index].Count)
            {
                spare += quota[index] - days[index].Count;
                quota[index] = days[index].Count;
            }
        }

        for (var index = 0; index < days.Count && spare > 0; index++)
        {
            var extra = Math.Min(spare, days[index].Count - quota[index]);
            quota[index] += extra;
            spare -= extra;
        }

        for (var index = 0; index < days.Count; index++)
        {
            var photos = days[index];
            for (var slot = 0; slot < quota[index]; slot++)
            {
                picked.Add(photos[quota[index] == 1 ? 0 : (int)Math.Round(slot * (photos.Count - 1) / (double)(quota[index] - 1))]);
            }
        }

        return picked;
    }
}
