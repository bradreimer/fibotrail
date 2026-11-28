using Fibotrail.Games;
using Fibotrail.Games.Timeline;
using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
using Fibotrail.Server.Trips;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Fibotrail.Server.Games.Timeline;

/// <summary>
/// A card as it's shown while you play: the photo only. A trip name often gives the order away, so
/// it isn't sent until the game ends.
/// </summary>
public sealed record TimelineCardView(string PhotoId);

/// <summary>A card in the answer: its trip, for a link to the trip page, and the day its photo was taken.</summary>
/// <param name="ImmichUrl">The photo in Immich, at the address your browser uses. Null when IMMICH_EXTERNAL_URL is empty.</param>
public sealed record TimelineAnswerCard(string PhotoId, int TripId, string TripName, DateOnly Date, string? ImmichUrl);

/// <summary>The day's twist: which one, its name, and the one-line rule shown above the cards.</summary>
/// <param name="Planned">The weekday's twist. It differs from <paramref name="Id"/> when the library can't support it.</param>
public sealed record TimelineTwistView(TimelineTwist Id, TimelineTwist Planned, string Name, string Rule)
{
    public static TimelineTwistView Of(TimelinePuzzle puzzle)
    {
        ArgumentNullException.ThrowIfNull(puzzle);
        return new(puzzle.Twist, puzzle.Planned, TimelineTwists.Name(puzzle.Twist), puzzle.Rule);
    }

    public static TimelineTwistView ForDate(DateOnly date)
    {
        var twist = TimelineTwists.For(date);
        return new(twist, twist, TimelineTwists.Name(twist), TimelineTwists.Rule(twist));
    }
}

/// <summary>Today's puzzle.</summary>
/// <param name="TripsNeeded">More trips with photos needed before there's a puzzle. 0 when there is one.</param>
/// <param name="Cards">The cards in shown order. Empty when there's no puzzle.</param>
/// <param name="Result">The day's result, when it's played.</param>
public sealed record TimelinePuzzleView(int Number, DateOnly Date, TimelineTwistView Twist, int MaxTries, int TripsNeeded, IReadOnlyList<TimelineCardView> Cards, TimelineResultView? Result);

/// <summary>One try to grade, without storing it.</summary>
public sealed record TimelineCheckRequest(DateOnly Date, IReadOnlyList<string> Order);

/// <summary>The marks of one try, one for each position.</summary>
public sealed record TimelineMarks(IReadOnlyList<Mark> Marks);

/// <summary>Every try of a day, for the server to grade and store.</summary>
public sealed record TimelineResultRequest(DateOnly Date, IReadOnlyList<IReadOnlyList<string>> Guesses);

/// <summary>A day's graded result, with the answer and the stats after it.</summary>
/// <param name="Rows">The marks of each try.</param>
/// <param name="Share">The text to paste to friends. It has no trip name, place, date, or photo.</param>
public sealed record TimelineResultView(bool Solved, int Tries, int MaxTries, IReadOnlyList<IReadOnlyList<Mark>> Rows, string Share, IReadOnlyList<TimelineAnswerCard> Answer, GameStats Stats);

/// <summary>The Timeline game's routes. See docs/specs/timeline-game.md.</summary>
public static class TimelineRoutes
{
    public static readonly GameDefinition Definition = new(
        TimelinePuzzle.Id,
        TimelinePuzzle.Name,
        "Put your photos in the order you took them, with a new twist each day.",
        TimelineTwists.MostTries,
        Map);

    private static readonly ProblemDetail NoPuzzle = new("There's no puzzle today. Add more trips with photos.");
    private static readonly ProblemDetail NotValid = new("Each try must hold every card once, within the tries allowed.");

    private static void Map(RouteGroupBuilder game)
    {
        game.MapGet("/puzzle", async Task<Results<Ok<TimelinePuzzleView>, BadRequest<ProblemDetail>>> (DateOnly date, FibotrailDbContext db, ServerSettings settings, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(date, time))
            {
                return GameDates.NotToday();
            }

            var library = await GameLibraryLoader.LoadAsync(db, cancellationToken);
            var number = DailySeed.PuzzleNumber(date);
            if (TimelinePuzzle.Create(library, date) is not { } puzzle)
            {
                return TypedResults.Ok(new TimelinePuzzleView(number, date, TimelineTwistView.ForDate(date), TimelineTwists.MaxTries(TimelineTwists.For(date)), TimelinePuzzle.TripsNeeded(library), [], null));
            }

            var stored = await GameResultStore.FindAsync(db, TimelinePuzzle.Id, date, cancellationToken);
            var result = stored is null ? null : await ViewAsync(db, settings, puzzle, stored, cancellationToken);
            return TypedResults.Ok(new TimelinePuzzleView(
                number,
                date,
                TimelineTwistView.Of(puzzle),
                puzzle.MaxTries,
                0,
                [.. puzzle.Shown.Select(card => new TimelineCardView(card.PhotoId))],
                result));
        }).WithName("GetTimelinePuzzle");

        game.MapPost("/check", async Task<Results<Ok<TimelineMarks>, BadRequest<ProblemDetail>>> (TimelineCheckRequest request, FibotrailDbContext db, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(request.Date, time))
            {
                return GameDates.NotToday();
            }

            var puzzle = TimelinePuzzle.Create(await GameLibraryLoader.LoadAsync(db, cancellationToken), request.Date);
            if (puzzle is null)
            {
                return TypedResults.BadRequest(NoPuzzle);
            }

            return puzzle.Grade(request.Order ?? []) is { } marks ? TypedResults.Ok(new TimelineMarks(marks)) : TypedResults.BadRequest(NotValid);
        }).WithName("CheckTimeline");

        game.MapPost("/results", async Task<Results<Ok<TimelineResultView>, BadRequest<ProblemDetail>>> (TimelineResultRequest request, FibotrailDbContext db, ServerSettings settings, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(request.Date, time))
            {
                return GameDates.NotToday();
            }

            var puzzle = TimelinePuzzle.Create(await GameLibraryLoader.LoadAsync(db, cancellationToken), request.Date);
            if (puzzle is null)
            {
                return TypedResults.BadRequest(NoPuzzle);
            }

            // A day already played keeps its first result, whatever this send holds.
            var stored = await GameResultStore.FindAsync(db, TimelinePuzzle.Id, request.Date, cancellationToken);
            if (stored is null)
            {
                if (puzzle.Score(request.Guesses ?? []) is not { } outcome)
                {
                    return TypedResults.BadRequest(NotValid);
                }

                // The game ends when it's solved or out of tries. An unfinished game isn't a result.
                if (!outcome.Solved && outcome.Rows.Count < puzzle.MaxTries)
                {
                    return TypedResults.BadRequest(NotValid);
                }

                stored = await GameResultStore.AddOnceAsync(db, new GameResult
                {
                    Game = TimelinePuzzle.Id,
                    PuzzleDate = request.Date,
                    Solved = outcome.Solved,
                    Tries = outcome.Rows.Count,
                    MaxTries = puzzle.MaxTries,
                    Marks = Marks.Encode(outcome.Rows),
                    PlayedAt = time.GetUtcNow().UtcDateTime,
                }, cancellationToken);
            }

            var view = await ViewAsync(db, settings, puzzle, stored, cancellationToken);

            // The share text is kept with the result, for Share today on the Games screen.
            await GameResultStore.KeepShareAsync(db, stored, view.Share, cancellationToken);
            return TypedResults.Ok(view);
        }).WithName("PostTimelineResult");
    }

    private static async Task<TimelineResultView> ViewAsync(FibotrailDbContext db, ServerSettings settings, TimelinePuzzle puzzle, GameResult stored, CancellationToken cancellationToken)
    {
        var rows = Marks.Decode(stored.Marks);
        var stats = await GameResultStore.StatsAsync(db, TimelinePuzzle.Id, TimelineTwists.MostTries, puzzle.Date, cancellationToken);
        return new TimelineResultView(
            stored.Solved,
            stored.Tries,
            stored.MaxTries,
            rows,
            Marks.ShareText(TimelinePuzzle.Name, puzzle.Number, stored.Solved, rows, stored.MaxTries, puzzle.Twist == TimelineTwist.Classic ? null : TimelineTwists.Name(puzzle.Twist)),
            [.. puzzle.Answer.Select(card => new TimelineAnswerCard(card.PhotoId, card.TripId, card.TripName, DateOnly.FromDateTime(card.Taken), TripPage.ImmichPhotoUrl(settings.ImmichExternalUrl, card.PhotoId)))],
            stats);
    }
}
