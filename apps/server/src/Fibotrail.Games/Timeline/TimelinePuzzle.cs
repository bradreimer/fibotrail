namespace Fibotrail.Games.Timeline;

/// <summary>A card: one photo from one trip. Only the photo ID is shown until the game ends.</summary>
public sealed record TimelineCard(string PhotoId, int TripId, string TripName, DateTime Taken);

/// <summary>The graded tries of one day.</summary>
public sealed record TimelineOutcome(bool Solved, IReadOnlyList<IReadOnlyList<Mark>> Rows);

/// <summary>
/// The Timeline game: put five photos in the order you took them. Each weekday has its own twist.
/// Pure functions over the game library. See docs/specs/timeline-game.md.
/// </summary>
/// <param name="Twist">The twist the day plays with.</param>
/// <param name="Planned">The twist of the weekday. Differs from <paramref name="Twist"/> when the library can't support it.</param>
/// <param name="Shown">The cards in the order they're first shown. Never the answer.</param>
/// <param name="Answer">The cards in the order the photos were taken.</param>
public sealed record TimelinePuzzle(DateOnly Date, int Number, TimelineTwist Twist, TimelineTwist Planned, IReadOnlyList<TimelineCard> Shown, IReadOnlyList<TimelineCard> Answer)
{
    public const string Id = "timeline";
    public const string Name = "Timeline";

    /// <summary>The fewest cards of any twist. A library needs this many trips with photos.</summary>
    public const int Cards = 5;

    /// <summary>The tries this puzzle allows.</summary>
    public int MaxTries => TimelineTwists.MaxTries(Twist);

    /// <summary>The rule shown above the cards, which says so when the day fell back to Classic.</summary>
    public string Rule => Twist == Planned ? TimelineTwists.Rule(Twist) : TimelineTwists.FallbackRule(Planned);

    /// <summary>How many more trips with photos the library needs before there's a puzzle.</summary>
    public static int TripsNeeded(GameLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        return Math.Max(0, Cards - library.Trips.Count(trip => trip.Photos.Count > 0));
    }

    /// <summary>
    /// The puzzle of a date with its weekday's twist, or Classic when the library can't support
    /// that twist. Null when the library has too few trips with photos.
    /// </summary>
    public static TimelinePuzzle? Create(GameLibrary library, DateOnly date)
    {
        var planned = TimelineTwists.For(date);
        if (Create(library, date, planned) is { } puzzle)
        {
            return puzzle;
        }

        return Create(library, date, TimelineTwist.Classic) is { } classic ? classic with { Planned = planned } : null;
    }

    /// <summary>The puzzle of a date with a given twist, or null when the library can't support it.</summary>
    public static TimelinePuzzle? Create(GameLibrary library, DateOnly date, TimelineTwist twist)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (TripsNeeded(library) > 0)
        {
            return null;
        }

        var random = new SeededRandom(DailySeed.For(Id, date));

        // Sort first, so the picks don't depend on the order the database returned.
        var eligible = library.Trips.Where(trip => trip.Photos.Count > 0).OrderBy(trip => trip.Id).ToList();
        var cards = twist switch
        {
            TimelineTwist.Neighbors => Neighbors(eligible, random),
            TimelineTwist.OneTrip => OneTrip(eligible, random),
            _ => eligible.Count < TimelineTwists.Cards(twist) ? null : [.. random.Shuffle(eligible).Take(TimelineTwists.Cards(twist)).Select(trip => OnePhoto(trip, trip.Photos, random))],
        };
        if (cards is null)
        {
            return null;
        }

        var answer = cards.OrderBy(card => card.Taken).ThenBy(card => card.PhotoId, StringComparer.Ordinal).ToList();
        var shown = random.Shuffle(answer);
        for (var attempt = 0; attempt < 10 && shown.SequenceEqual(answer); attempt++)
        {
            shown = random.Shuffle(answer);
        }

        if (shown.SequenceEqual(answer))
        {
            // Ten sorted shuffles in a row is close to impossible, but the answer is never shown first.
            shown = [.. answer.Skip(1), answer[0]];
        }

        return new TimelinePuzzle(date, DailySeed.PuzzleNumber(date), twist, twist, shown, answer);
    }

    /// <summary>Five trips in a row by start date, from a random place in the list.</summary>
    private static List<TimelineCard> Neighbors(List<GameTrip> eligible, SeededRandom random)
    {
        var byStart = eligible.OrderBy(trip => trip.StartDate).ThenBy(trip => trip.Id).ToList();
        var first = random.Next(byStart.Count - Cards + 1);
        return [.. byStart.Skip(first).Take(Cards).Select(trip => OnePhoto(trip, trip.Photos, random))];
    }

    /// <summary>
    /// Five photos of one trip, from five different days, so each pair is a day apart or more.
    /// Null when no trip has photos on five days.
    /// </summary>
    private static List<TimelineCard>? OneTrip(List<GameTrip> eligible, SeededRandom random)
    {
        var multiDay = eligible.Where(trip => trip.Photos.Select(photo => photo.LocalTime.Date).Distinct().Count() >= Cards).ToList();
        if (multiDay.Count == 0)
        {
            return null;
        }

        var trip = multiDay[random.Next(multiDay.Count)];
        var days = trip.Photos.GroupBy(photo => photo.LocalTime.Date).OrderBy(day => day.Key);
        return [.. random.Shuffle(days).Take(Cards).Select(day => OnePhoto(trip, [.. day], random))];
    }

    private static TimelineCard OnePhoto(GameTrip trip, IReadOnlyList<GamePhoto> photos, SeededRandom random)
    {
        var sorted = photos.OrderBy(photo => photo.LocalTime).ThenBy(photo => photo.Id, StringComparer.Ordinal).ToList();
        var photo = sorted[random.Next(sorted.Count)];
        return new TimelineCard(photo.Id, trip.Id, trip.Name, photo.LocalTime);
    }

    /// <summary>
    /// The marks of one try, or null when the order doesn't hold every card exactly once. A card is
    /// Right in its place. Otherwise the twist decides: Close one place away and Wrong farther;
    /// with Arrows, Earlier or Later; with Count only, the row is just the Right count, sorted
    /// first, then Wrong, so it doesn't say which cards are in place.
    /// </summary>
    public IReadOnlyList<Mark>? Grade(IReadOnlyList<string> order)
    {
        ArgumentNullException.ThrowIfNull(order);
        var place = Answer.Select((card, index) => (card.PhotoId, index)).ToDictionary(pair => pair.PhotoId, pair => pair.index, StringComparer.Ordinal);
        if (order.Count != Answer.Count || order.Distinct(StringComparer.Ordinal).Count() != order.Count || !order.All(place.ContainsKey))
        {
            return null;
        }

        var marks = order.Select((id, index) => (place[id] - index) switch
        {
            0 => Mark.Right,
            < 0 when Twist == TimelineTwist.Arrows => Mark.Earlier,
            > 0 when Twist == TimelineTwist.Arrows => Mark.Later,
            -1 or 1 when Twist != TimelineTwist.CountOnly => Mark.Close,
            _ => Mark.Wrong,
        }).ToList();

        return Twist == TimelineTwist.CountOnly ? [.. marks.OrderBy(mark => mark == Mark.Right ? 0 : 1)] : marks;
    }

    /// <summary>
    /// Grades every try of a day. Null when there are no tries, too many, a try that isn't valid, or
    /// a try after a solved one.
    /// </summary>
    public TimelineOutcome? Score(IReadOnlyList<IReadOnlyList<string>> guesses)
    {
        ArgumentNullException.ThrowIfNull(guesses);
        if (guesses.Count == 0 || guesses.Count > MaxTries)
        {
            return null;
        }

        var rows = new List<IReadOnlyList<Mark>>();
        foreach (var guess in guesses)
        {
            if (rows.Count > 0 && rows[^1].All(mark => mark == Mark.Right))
            {
                return null;
            }

            if (Grade(guess) is not { } marks)
            {
                return null;
            }

            rows.Add(marks);
        }

        return new TimelineOutcome(rows[^1].All(mark => mark == Mark.Right), rows);
    }
}
