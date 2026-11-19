namespace Fibotrail.Games.Timeline;

/// <summary>A card: one photo from one trip. Only the photo ID is shown until the game ends.</summary>
public sealed record TimelineCard(string PhotoId, int TripId, string TripName, DateTime Taken);

/// <summary>The graded tries of one day.</summary>
public sealed record TimelineOutcome(bool Solved, IReadOnlyList<IReadOnlyList<Mark>> Rows);

/// <summary>
/// The Timeline game: put five photos from five trips in the order you took them. Pure functions
/// over the game library. See docs/specs/timeline-game.md.
/// </summary>
/// <param name="Shown">The cards in the order they're first shown. Never the answer.</param>
/// <param name="Answer">The cards in the order the photos were taken.</param>
public sealed record TimelinePuzzle(DateOnly Date, int Number, IReadOnlyList<TimelineCard> Shown, IReadOnlyList<TimelineCard> Answer)
{
    public const string Id = "timeline";
    public const string Name = "Timeline";
    public const int Cards = 5;
    public const int MaxTries = 4;

    /// <summary>How many more trips with photos the library needs before there's a puzzle.</summary>
    public static int TripsNeeded(GameLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        return Math.Max(0, Cards - library.Trips.Count(trip => trip.Photos.Count > 0));
    }

    /// <summary>The puzzle of a date, or null when the library has too few trips with photos.</summary>
    public static TimelinePuzzle? Create(GameLibrary library, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (TripsNeeded(library) > 0)
        {
            return null;
        }

        var random = new SeededRandom(DailySeed.For(Id, date));

        // Sort first, so the picks don't depend on the order the database returned.
        var eligible = library.Trips.Where(trip => trip.Photos.Count > 0).OrderBy(trip => trip.Id);
        var cards = random.Shuffle(eligible).Take(Cards).Select(trip =>
        {
            var photos = trip.Photos.OrderBy(photo => photo.LocalTime).ThenBy(photo => photo.Id, StringComparer.Ordinal).ToList();
            var photo = photos[random.Next(photos.Count)];
            return new TimelineCard(photo.Id, trip.Id, trip.Name, photo.LocalTime);
        }).ToList();

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

        return new TimelinePuzzle(date, DailySeed.PuzzleNumber(date), shown, answer);
    }

    /// <summary>
    /// The marks of one try, or null when the order doesn't hold every card exactly once. A card is
    /// Right in its place, Close one place away, and Wrong otherwise.
    /// </summary>
    public IReadOnlyList<Mark>? Grade(IReadOnlyList<string> order)
    {
        ArgumentNullException.ThrowIfNull(order);
        var place = Answer.Select((card, index) => (card.PhotoId, index)).ToDictionary(pair => pair.PhotoId, pair => pair.index, StringComparer.Ordinal);
        if (order.Count != Answer.Count || order.Distinct(StringComparer.Ordinal).Count() != order.Count || !order.All(place.ContainsKey))
        {
            return null;
        }

        return [.. order.Select((id, index) => Math.Abs(place[id] - index) switch
        {
            0 => Mark.Right,
            1 => Mark.Close,
            _ => Mark.Wrong,
        })];
    }

    /// <summary>
    /// Grades every try of a day. Null when there are no tries, too many, a try that isn't valid, or
    /// a try after a solved one.
    /// </summary>
    public TimelineOutcome? Score(IReadOnlyList<IReadOnlyList<string>> guesses)
    {
        ArgumentNullException.ThrowIfNull(guesses);
        if (guesses.Count is 0 or > MaxTries)
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
