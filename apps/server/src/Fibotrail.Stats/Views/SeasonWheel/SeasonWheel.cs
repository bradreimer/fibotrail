namespace Fibotrail.Stats.Views.SeasonWheel;

/// <summary>A month of the year, with the country's days in it.</summary>
/// <param name="Month">1 for January to 12 for December.</param>
/// <param name="Hemisphere">"north" or "south": the hemisphere of most of the month's days, north on a tie or with no days.</param>
public sealed record WheelMonth(int Month, int Days, string Hemisphere);

/// <summary>A meteorological season, on the months of the wheel's hemisphere.</summary>
/// <param name="Name">Winter, Spring, Summer, or Autumn.</param>
/// <param name="Months">The three months of the season on the wheel, in order, such as 12, 1, 2.</param>
/// <param name="Days">The country's days that fall in this season, each by its own latitude.</param>
public sealed record WheelSeason(string Name, IReadOnlyList<int> Months, int Days, bool Seen);

/// <param name="Hemisphere">"north", "south", or "both": where the country's days are.</param>
/// <param name="ArcHemisphere">The hemisphere whose months the season arcs sit on: the one with most days.</param>
public sealed record SeasonWheelResult(string CountryName, string Hemisphere, string ArcHemisphere, int SeasonsSeen, IReadOnlyList<WheelMonth> Months, IReadOnlyList<WheelSeason> Seasons);

/// <summary>
/// The "Season wheel" view for one country: its days by month and by meteorological season. A day's
/// hemisphere is the sign of its latitude. See docs/specs/season-wheel.md.
/// </summary>
public static class SeasonWheelView
{
    private static readonly string[] Names = ["Winter", "Spring", "Summer", "Autumn"];

    // The first month of each season in the northern hemisphere: Dec, Mar, Jun, Sep.
    private static readonly int[] NorthStart = [12, 3, 6, 9];

    /// <summary>The season of a month (1 to 12), as an index into Winter, Spring, Summer, Autumn.</summary>
    public static int SeasonOf(int month, bool north)
    {
        var shifted = north ? month : ((month + 5) % 12) + 1;
        return shifted switch
        {
            12 or 1 or 2 => 0,
            3 or 4 or 5 => 1,
            6 or 7 or 8 => 2,
            _ => 3,
        };
    }

    public static SeasonWheelResult? Compute(StatsLibrary library, string countryKey)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (library.Country(countryKey) is not { } country) return null;

        var days = country.DayList;
        var northDays = days.Count(day => day.Latitude >= 0);
        var southDays = days.Count - northDays;
        var hemisphere = northDays > 0 && southDays > 0 ? "both" : southDays > 0 ? "south" : "north";
        var arcNorth = northDays >= southDays;

        var months = Enumerable.Range(1, 12).Select(month =>
        {
            var inMonth = days.Where(day => day.Day.Month == month).ToList();
            var south = inMonth.Count(day => day.Latitude < 0);
            return new WheelMonth(month, inMonth.Count, south > inMonth.Count - south ? "south" : "north");
        }).ToList();

        var seasonDays = new int[4];
        foreach (var day in days) seasonDays[SeasonOf(day.Day.Month, day.Latitude >= 0)]++;

        var seasons = Enumerable.Range(0, 4).Select(index =>
        {
            var start = arcNorth ? NorthStart[index] : ((NorthStart[index] + 5) % 12) + 1;
            return new WheelSeason(Names[index], [start, (start % 12) + 1, ((start + 1) % 12) + 1], seasonDays[index], seasonDays[index] > 0);
        }).ToList();

        return new SeasonWheelResult(country.Country.Name, hemisphere, arcNorth ? "north" : "south", seasons.Count(season => season.Seen), months, seasons);
    }
}
