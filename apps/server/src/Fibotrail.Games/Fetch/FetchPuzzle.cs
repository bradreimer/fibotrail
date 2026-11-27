using System.Globalization;
using System.Text;

namespace Fibotrail.Games.Fetch;

/// <summary>The trip a course comes from, shown at the end.</summary>
/// <param name="Place">The trip's main destination, or null when none is known.</param>
public sealed record FetchTrip(int Id, string Name, DateOnly StartDate, DateOnly EndDate, string? Place);

/// <summary>The graded runs of a day, and the taps that made them, up to where each run ended.</summary>
public sealed record FetchOutcome(bool Solved, IReadOnlyList<FetchRunGrade> Runs, IReadOnlyList<IReadOnlyList<int>> Taps)
{
    /// <summary>The run that caught the most treats: the first one, on a tie.</summary>
    public FetchRunGrade Best => Runs.Aggregate((best, run) => run.Caught.Count > best.Caught.Count ? run : best);
}

/// <summary>
/// The Fetch game: a schnauzer flies one of your trips. 8 gates follow the trip's photo latitudes,
/// north up, and 8 treats are its photos. You have 3 runs to reach the finish. The server grades
/// each run by flying it again from its taps. Pure functions over the game library. See
/// docs/specs/fetch-game.md.
/// </summary>
public sealed record FetchPuzzle(DateOnly Date, int Number, FetchCourse Course, FetchTrip Trip)
{
    public const string Id = "fetch";
    public const string Name = "Fetch";
    public const int MaxTries = 3;
    public const int GateCount = 8;
    public const int MaxTaps = 300;

    /// <summary>The band gap centers stay in, in pixels.</summary>
    public const int HighestCenter = 185;
    public const int LowestCenter = 435;

    /// <summary>The most a gap center moves from the one before it, in pixels.</summary>
    public const int MaxStep = 150;

    /// <summary>The smallest span of latitude scaled to the band, so GPS noise stays flat.</summary>
    public const double MinSpanDegrees = 0.5;

    public const int FirstGateX = 440;
    public const int GateSpacing = 240;
    public const int TreatOffset = 70;

    /// <summary>How many more trips with a located photo the library needs: 1 or 0.</summary>
    public static int TripsNeeded(GameLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        return library.Trips.Any(trip => trip.Photos.Any(photo => photo.HasLocation)) ? 0 : 1;
    }

    /// <summary>The course of a date, or null when no added trip has a photo with a location.</summary>
    public static FetchPuzzle? Create(GameLibrary library, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(library);
        var random = new SeededRandom(DailySeed.For(Id, date));

        // Sort first, so the picks don't depend on the order the database returned.
        var eligible = library.Trips
            .Select(trip => (Trip: trip, Photos: trip.Photos
                .Where(photo => photo.HasLocation)
                .OrderBy(photo => photo.LocalTime)
                .ThenBy(photo => photo.Id, StringComparer.Ordinal)
                .ToList()))
            .Where(pick => pick.Photos.Count > 0)
            .OrderBy(pick => pick.Trip.Id)
            .ToList();
        if (eligible.Count == 0)
        {
            return null;
        }

        var (trip, photos) = eligible[random.Next(eligible.Count)];
        var picked = Enumerable.Range(0, GateCount)
            .Select(index => photos[(int)Math.Round(index * (photos.Count - 1) / (double)(GateCount - 1), MidpointRounding.AwayFromZero)])
            .ToList();
        var centers = Centers(picked.Select(photo => photo.Latitude!.Value).ToList());

        const int unit = FetchPhysics.Unit;
        var physics = FetchPhysics.Default;
        var half = physics.GapHeight / 2;
        var gates = new List<FetchGate>();
        var treats = new List<FetchTreat>();
        for (var index = 0; index < GateCount; index++)
        {
            var x = (FirstGateX + (index * GateSpacing)) * unit;
            var day = DateOnly.FromDateTime(picked[index].LocalTime).DayNumber - trip.StartDate.DayNumber + 1;
            gates.Add(new FetchGate(x, (centers[index] * unit) - half, (centers[index] * unit) + half, day));

            var between = index < GateCount - 1 ? (centers[index] + centers[index + 1]) / 2 : centers[index];
            var offset = random.Next(2) == 0 ? -TreatOffset : TreatOffset;
            var treatX = x + physics.GateWidth + ((GateSpacing * unit) - physics.GateWidth) / 2;
            treats.Add(new FetchTreat(treatX, Math.Clamp(between + offset, 40, 580) * unit, picked[index].Id));
        }

        var finish = (FirstGateX + ((GateCount - 1) * GateSpacing) + GateSpacing) * unit;
        return new FetchPuzzle(
            date,
            DailySeed.PuzzleNumber(date),
            new FetchCourse(physics, gates, treats, finish),
            new FetchTrip(trip.Id, trip.Name, trip.StartDate, trip.EndDate, trip.Place));
    }

    /// <summary>
    /// The gap centers, in pixels, from the latitudes: north is up. The span is centered and at least
    /// <see cref="MinSpanDegrees"/>, and no center moves more than <see cref="MaxStep"/> from the one
    /// before it.
    /// </summary>
    public static IReadOnlyList<int> Centers(IReadOnlyList<double> latitudes)
    {
        ArgumentNullException.ThrowIfNull(latitudes);
        if (latitudes.Count == 0)
        {
            return [];
        }

        var min = latitudes.Min();
        var max = latitudes.Max();
        var middle = (min + max) / 2;
        var span = Math.Max(max - min, MinSpanDegrees);
        var centers = new List<int>();
        foreach (var latitude in latitudes)
        {
            var share = 0.5 + ((latitude - middle) / span);
            var center = (int)Math.Round(LowestCenter - (share * (LowestCenter - HighestCenter)), MidpointRounding.AwayFromZero);
            if (centers.Count > 0)
            {
                center = Math.Clamp(center, centers[^1] - MaxStep, centers[^1] + MaxStep);
            }

            centers.Add(center);
        }

        return centers;
    }

    /// <summary>True when the taps are at most <see cref="MaxTaps"/> ticks, each 2 or more, in increasing order.</summary>
    public static bool IsValidRun(IReadOnlyList<int>? taps)
    {
        if (taps is null || taps.Count > MaxTaps)
        {
            return false;
        }

        for (var index = 0; index < taps.Count; index++)
        {
            if (taps[index] < 2 || (index > 0 && taps[index] <= taps[index - 1]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Grades one run, or null when its taps aren't valid.</summary>
    public FetchRunGrade? Grade(IReadOnlyList<int>? taps) =>
        IsValidRun(taps) ? FetchFlight.Fly(Course, taps!) : null;

    /// <summary>
    /// Grades every run of a day. Null when there are none, more than <see cref="MaxTries"/>, one
    /// isn't valid, or one comes after a finished run. Taps after a run ended are dropped.
    /// </summary>
    public FetchOutcome? Score(IReadOnlyList<IReadOnlyList<int>>? runs)
    {
        if (runs is null || runs.Count is 0 or > MaxTries)
        {
            return null;
        }

        var grades = new List<FetchRunGrade>();
        var kept = new List<IReadOnlyList<int>>();
        foreach (var taps in runs)
        {
            if (grades.Count > 0 && grades[^1].Finished)
            {
                return null;
            }

            if (Grade(taps) is not { } grade)
            {
                return null;
            }

            grades.Add(grade);
            kept.Add([.. taps.Where(tick => tick <= grade.Tick)]);
        }

        return new FetchOutcome(grades[^1].Finished, grades, kept);
    }

    /// <summary>The taps as stored text: each run's ticks joined by ",", and runs joined by ";".</summary>
    public static string Encode(IEnumerable<IReadOnlyList<int>> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        return string.Join(';', runs.Select(taps => string.Join(',', taps.Select(tick => tick.ToString(CultureInfo.InvariantCulture)))));
    }

    public static IReadOnlyList<IReadOnlyList<int>> Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. text.Split(';').Select(run => (IReadOnlyList<int>)[.. run.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(tick => int.Parse(tick, CultureInfo.InvariantCulture))])];
    }

    /// <summary>
    /// The text you paste to brag: the game, the puzzle number, the runs used (X when none
    /// finished), and the most treats in one run. Then one line per run: 🦴 for a caught treat and
    /// ⬛ for a missed one, up to where the run ended, then 💥 or 🏁. It holds no trip name, place,
    /// date, or photo.
    /// </summary>
    public static string ShareText(int puzzleNumber, FetchOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var text = new StringBuilder();
        var tries = outcome.Solved ? outcome.Runs.Count.ToString(CultureInfo.InvariantCulture) : "X";
        text.Append(CultureInfo.InvariantCulture, $"Fibotrail {Name} #{puzzleNumber} {tries}/{MaxTries} 🦴 {outcome.Best.Caught.Count}/{GateCount}");
        foreach (var run in outcome.Runs)
        {
            text.Append('\n');

            // A caught treat is always past its gate, so the treats up to the gates passed cover them.
            var reached = run.Finished ? GateCount : run.GatesPassed;
            for (var index = 0; index < reached; index++)
            {
                text.Append(run.Caught.Contains(index) ? "🦴" : "⬛");
            }

            text.Append(run.Finished ? "🏁" : "💥");
        }

        return text.ToString();
    }
}
