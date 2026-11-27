using Fibotrail.Games;
using Fibotrail.Games.Fetch;
using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Fibotrail.Server.Games.Fetch;

/// <summary>Today's course.</summary>
/// <param name="TripsNeeded">1 when no added trip has a photo with a location yet, and 0 when there's a course.</param>
/// <param name="Course">The course, or null when there's none. No trip name, date, place, or coordinates are sent while you play.</param>
/// <param name="Result">The day's result, when it's played.</param>
public sealed record FetchPuzzleView(int Number, DateOnly Date, int MaxTries, int TripsNeeded, FetchCourse? Course, FetchResultView? Result);

/// <summary>One run to grade, without storing it.</summary>
/// <param name="Taps">The ticks of the flaps after the first, each 2 or more, in order.</param>
public sealed record FetchCheckRequest(DateOnly Date, IReadOnlyList<int> Taps);

/// <summary>Every run of a day, in order, for the server to grade and store.</summary>
public sealed record FetchResultRequest(DateOnly Date, IReadOnlyList<IReadOnlyList<int>> Runs);

/// <summary>The trip the course comes from, shown at the end.</summary>
public sealed record FetchTripView(int TripId, string TripName, DateOnly StartDate, DateOnly EndDate, string? Place);

/// <summary>A day's graded result, with the trip and the stats after it.</summary>
/// <param name="Treats">The most treats caught in one run.</param>
/// <param name="CaughtPhotoIds">The photos of the treats that run caught, in course order.</param>
/// <param name="Share">The text to paste to friends. It has no trip name, place, date, or photo.</param>
public sealed record FetchResultView(bool Solved, int Tries, int MaxTries, IReadOnlyList<FetchRunGrade> Runs, int Treats, IReadOnlyList<string> CaughtPhotoIds, string Share, FetchTripView Trip, GameStats Stats);

/// <summary>The Fetch game's routes. See docs/specs/fetch-game.md.</summary>
public static class FetchRoutes
{
    public static readonly GameDefinition Definition = new(
        FetchPuzzle.Id,
        FetchPuzzle.Name,
        "Fly the schnauzer along one of your trips, and catch its photos.",
        FetchPuzzle.MaxTries,
        Map);

    private static readonly ProblemDetail NoPuzzle = new("There's no course today. Add a trip with photos that have a location.");
    private static readonly ProblemDetail NotValid = new("Each run must have at most 300 taps in order, within the runs allowed, and none may follow a finished run.");
    private static readonly ProblemDetail NotFinished = new("A game ends with a finish or when the runs are used.");

    private static void Map(RouteGroupBuilder game)
    {
        game.MapGet("/puzzle", async Task<Results<Ok<FetchPuzzleView>, BadRequest<ProblemDetail>>> (DateOnly date, FibotrailDbContext db, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(date, time))
            {
                return GameDates.NotToday();
            }

            var library = await GameLibraryLoader.LoadAsync(db, cancellationToken);
            if (FetchPuzzle.Create(library, date) is not { } puzzle)
            {
                return TypedResults.Ok(new FetchPuzzleView(DailySeed.PuzzleNumber(date), date, FetchPuzzle.MaxTries, FetchPuzzle.TripsNeeded(library), null, null));
            }

            var stored = await GameResultStore.FindAsync(db, FetchPuzzle.Id, date, cancellationToken);
            var result = stored is null ? null : await ViewAsync(db, puzzle, stored, cancellationToken);
            return TypedResults.Ok(new FetchPuzzleView(puzzle.Number, date, FetchPuzzle.MaxTries, 0, puzzle.Course, result));
        }).WithName("GetFetchPuzzle");

        game.MapPost("/check", async Task<Results<Ok<FetchRunGrade>, BadRequest<ProblemDetail>>> (FetchCheckRequest request, FibotrailDbContext db, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(request.Date, time))
            {
                return GameDates.NotToday();
            }

            var puzzle = FetchPuzzle.Create(await GameLibraryLoader.LoadAsync(db, cancellationToken), request.Date);
            if (puzzle is null)
            {
                return TypedResults.BadRequest(NoPuzzle);
            }

            return puzzle.Grade(request.Taps) is { } grade ? TypedResults.Ok(grade) : TypedResults.BadRequest(NotValid);
        }).WithName("CheckFetchRun");

        game.MapPost("/results", async Task<Results<Ok<FetchResultView>, BadRequest<ProblemDetail>>> (FetchResultRequest request, FibotrailDbContext db, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(request.Date, time))
            {
                return GameDates.NotToday();
            }

            var puzzle = FetchPuzzle.Create(await GameLibraryLoader.LoadAsync(db, cancellationToken), request.Date);
            if (puzzle is null)
            {
                return TypedResults.BadRequest(NoPuzzle);
            }

            // A day already played keeps its first result, whatever this send holds.
            var stored = await GameResultStore.FindAsync(db, FetchPuzzle.Id, request.Date, cancellationToken);
            if (stored is null)
            {
                // The server flies every run again from its taps, so the browser never sends a score.
                if (puzzle.Score(request.Runs) is not { } outcome)
                {
                    return TypedResults.BadRequest(NotValid);
                }

                if (!outcome.Solved && outcome.Runs.Count < FetchPuzzle.MaxTries)
                {
                    return TypedResults.BadRequest(NotFinished);
                }

                stored = await GameResultStore.AddOnceAsync(db, new GameResult
                {
                    Game = FetchPuzzle.Id,
                    PuzzleDate = request.Date,
                    Solved = outcome.Solved,
                    Tries = outcome.Runs.Count,
                    MaxTries = FetchPuzzle.MaxTries,
                    Marks = FetchPuzzle.Encode(outcome.Taps),
                    PlayedAt = time.GetUtcNow().UtcDateTime,
                }, cancellationToken);
            }

            return TypedResults.Ok(await ViewAsync(db, puzzle, stored, cancellationToken));
        }).WithName("PostFetchResult");
    }

    private static async Task<FetchResultView> ViewAsync(FibotrailDbContext db, FetchPuzzle puzzle, GameResult stored, CancellationToken cancellationToken)
    {
        // The stored marks are the taps, so the grades come back from them. A stored result outlives
        // a library change, so a run that no longer grades is skipped rather than failing the page.
        var runs = FetchPuzzle.Decode(stored.Marks).Select(puzzle.Grade).OfType<FetchRunGrade>().ToList();
        var outcome = new FetchOutcome(stored.Solved, runs, []);
        var best = runs.Count > 0 ? outcome.Best : null;
        var stats = await GameResultStore.StatsAsync(db, FetchPuzzle.Id, FetchPuzzle.MaxTries, puzzle.Date, cancellationToken);
        var trip = puzzle.Trip;
        return new FetchResultView(
            stored.Solved,
            stored.Tries,
            stored.MaxTries,
            runs,
            best?.Caught.Count ?? 0,
            best is null ? [] : [.. best.Caught.Select(index => puzzle.Course.Treats[index].PhotoId)],
            runs.Count > 0 ? FetchPuzzle.ShareText(puzzle.Number, outcome) : $"Fibotrail {FetchPuzzle.Name} #{puzzle.Number}",
            new FetchTripView(trip.Id, trip.Name, trip.StartDate, trip.EndDate, trip.Place),
            stats);
    }
}
