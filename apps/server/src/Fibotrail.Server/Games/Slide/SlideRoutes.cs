using Fibotrail.Games;
using Fibotrail.Games.Slide;
using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Fibotrail.Server.Games.Slide;

/// <summary>Today's puzzle.</summary>
/// <param name="TripsNeeded">1 when no added trip has a photo yet, and 0 when there's a puzzle.</param>
/// <param name="PhotoId">The photo to cut into tiles, or null when there's no puzzle. No trip name or date is sent while you play.</param>
/// <param name="Tiles">The board as first shown, row by row: tile numbers 1 to 8, and 0 for the empty cell.</param>
/// <param name="Result">The day's result, when it's played.</param>
public sealed record SlidePuzzleView(int Number, DateOnly Date, int TripsNeeded, string? PhotoId, IReadOnlyList<int> Tiles, SlideResultView? Result);

/// <summary>The moves of a solved game: the tile slid into the empty cell, in order.</summary>
public sealed record SlideResultRequest(DateOnly Date, IReadOnlyList<int> Moves);

/// <summary>The trip the photo comes from, for the caption under the whole photo.</summary>
public sealed record SlideAnswerView(string PhotoId, int TripId, string TripName, DateOnly StartDate, DateOnly EndDate, string? Place);

/// <summary>A day's stored result, with the photo's trip and the stats after it.</summary>
/// <param name="Share">The text to paste to friends. It has no trip name, place, date, or photo.</param>
public sealed record SlideResultView(bool Solved, int Moves, string Share, SlideAnswerView Answer, GameStats Stats);

/// <summary>The Slide game's routes. See docs/specs/slide-game.md.</summary>
public static class SlideRoutes
{
    /// <summary>A sliding puzzle can't be lost, so a day is one try, and solved.</summary>
    private const int MaxTries = 1;

    public static readonly GameDefinition Definition = new(
        SlidePuzzle.Id,
        SlidePuzzle.Name,
        "Slide the tiles to put one of your trip photos back together.",
        MaxTries,
        Map);

    private static readonly ProblemDetail NoPuzzle = new("There's no puzzle today. Add a trip with photos.");
    private static readonly ProblemDetail NotSolved = new("Those moves don't solve today's puzzle.");

    private static void Map(RouteGroupBuilder game)
    {
        game.MapGet("/puzzle", async Task<Results<Ok<SlidePuzzleView>, BadRequest<ProblemDetail>>> (DateOnly date, FibotrailDbContext db, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(date, time))
            {
                return GameDates.NotToday();
            }

            var library = await GameLibraryLoader.LoadAsync(db, cancellationToken);
            if (SlidePuzzle.Create(library, date) is not { } puzzle)
            {
                return TypedResults.Ok(new SlidePuzzleView(DailySeed.PuzzleNumber(date), date, SlidePuzzle.TripsNeeded(library), null, [], null));
            }

            var stored = await GameResultStore.FindAsync(db, SlidePuzzle.Id, date, cancellationToken);
            var result = stored is null ? null : await ViewAsync(db, puzzle, stored, cancellationToken);
            return TypedResults.Ok(new SlidePuzzleView(puzzle.Number, date, 0, puzzle.Photo.PhotoId, puzzle.Start, result));
        }).WithName("GetSlidePuzzle");

        game.MapPost("/results", async Task<Results<Ok<SlideResultView>, BadRequest<ProblemDetail>>> (SlideResultRequest request, FibotrailDbContext db, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(request.Date, time))
            {
                return GameDates.NotToday();
            }

            var puzzle = SlidePuzzle.Create(await GameLibraryLoader.LoadAsync(db, cancellationToken), request.Date);
            if (puzzle is null)
            {
                return TypedResults.BadRequest(NoPuzzle);
            }

            // A day already played keeps its first result, whatever this send holds.
            var stored = await GameResultStore.FindAsync(db, SlidePuzzle.Id, request.Date, cancellationToken);
            if (stored is null)
            {
                // The server replays the moves, so the browser never sends a score.
                var moves = request.Moves ?? [];
                if (!puzzle.IsSolvedBy(moves))
                {
                    return TypedResults.BadRequest(NotSolved);
                }

                stored = await GameResultStore.AddOnceAsync(db, new GameResult
                {
                    Game = SlidePuzzle.Id,
                    PuzzleDate = request.Date,
                    Solved = true,
                    Tries = 1,
                    MaxTries = MaxTries,
                    Marks = SlidePuzzle.Encode(moves),
                    PlayedAt = time.GetUtcNow().UtcDateTime,
                }, cancellationToken);
            }

            return TypedResults.Ok(await ViewAsync(db, puzzle, stored, cancellationToken));
        }).WithName("PostSlideResult");
    }

    private static async Task<SlideResultView> ViewAsync(FibotrailDbContext db, SlidePuzzle puzzle, GameResult stored, CancellationToken cancellationToken)
    {
        // The stored marks are the moves, one digit per tile, so the move count is their length.
        var moves = stored.Marks.Length;
        var stats = await GameResultStore.StatsAsync(db, SlidePuzzle.Id, MaxTries, puzzle.Date, cancellationToken);
        var photo = puzzle.Photo;
        return new SlideResultView(
            stored.Solved,
            moves,
            SlidePuzzle.ShareText(puzzle.Number, moves),
            new SlideAnswerView(photo.PhotoId, photo.TripId, photo.TripName, photo.StartDate, photo.EndDate, photo.Place),
            stats);
    }
}
