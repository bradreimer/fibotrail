using System.Globalization;
using Fibotrail.Games;
using Fibotrail.Games.Stamps;
using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
using Fibotrail.Server.Trips;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Fibotrail.Server.Games.Stamps;

/// <summary>Today's board.</summary>
/// <param name="TripsNeeded">How many more added trips with photos the library needs, or 0 when there's a board.</param>
/// <param name="Size">N: the board has N rows, N columns, and N trips. 0 when there's no board.</param>
/// <param name="Label">How hard the size is: Easy, Medium, or Hard.</param>
/// <param name="Weekday">The puzzle date's weekday, such as "Wed".</param>
/// <param name="SizeNote">Why the board is smaller than the weekday's size, or null when it isn't.</param>
/// <param name="Regions">The board as N rows of region letters, such as "AABBBCC". No trip name, date, or place is sent while you play.</param>
/// <param name="Photos">The photo of each cell, row by row from the top left.</param>
/// <param name="Result">The day's result, when it's played.</param>
public sealed record StampsPuzzleView(
    int Number,
    DateOnly Date,
    int TripsNeeded,
    int Size,
    string Label,
    string Weekday,
    string Rule,
    string? SizeNote,
    IReadOnlyList<string> Regions,
    IReadOnlyList<string> Photos,
    StampsResultView? Result);

/// <summary>The board you're playing, for a hint. Cells are numbered row by row from 0.</summary>
public sealed record StampsHintRequest(DateOnly Date, IReadOnlyList<int> Stamps, IReadOnlyList<int> Crosses);

/// <summary>One step toward the solution.</summary>
/// <param name="Cell">The cell, or null when nothing is left to find.</param>
/// <param name="Stamp">True when the cell needs a stamp, false when it needs a ✕.</param>
/// <param name="Wrong">True when the hint points at your own mistake: a wrong stamp or a wrong ✕.</param>
/// <param name="Reason">The rule in one plain sentence.</param>
public sealed record StampsHintView(int? Cell, bool Stamp, bool Wrong, string Reason);

/// <summary>A solved board: its stamp cells, and the time and hints as the browser counted them.</summary>
public sealed record StampsResultRequest(DateOnly Date, IReadOnlyList<int> Stamps, int Seconds, int Hints);

/// <summary>One stamp on the passport page.</summary>
/// <param name="ImmichUrl">The photo in Immich, at the address your browser uses. Null when IMMICH_EXTERNAL_URL is empty.</param>
public sealed record StampsPassportView(int Cell, string Region, string PhotoId, int TripId, string TripName, DateOnly StartDate, DateOnly EndDate, string? ImmichUrl);

/// <summary>A day's stored result, with the passport page and the stats after it.</summary>
/// <param name="Passport">The stamped photos, one per trip, by the trips' start dates.</param>
/// <param name="Share">The text to paste to friends. It has no trip name, place, date, or photo.</param>
public sealed record StampsResultView(bool Solved, int Size, int Seconds, int Hints, string Share, IReadOnlyList<StampsPassportView> Passport, GameStats Stats);

/// <summary>The Stamps game's routes. See docs/specs/stamps-game.md.</summary>
public static class StampsRoutes
{
    /// <summary>A board always ends solved, so a day is one try.</summary>
    private const int MaxTries = 1;

    public static readonly GameDefinition Definition = new(
        StampsPuzzle.Id,
        StampsPuzzle.Name,
        "Stamp each row, column, and trip once, with no two stamps touching.",
        MaxTries,
        Map,
        StampsPuzzle.TimeLabel);

    private static readonly ProblemDetail NoPuzzle = new("There's no board today. Add at least 5 trips with photos.");
    private static readonly ProblemDetail NotOnBoard = new("Each cell must be on the board, and no cell may be both a stamp and a ✕.");
    private static readonly ProblemDetail NotSolved = new("Those stamps don't solve today's board.");
    private static readonly ProblemDetail NotCounted = new("The time must be from 1 second to 1 day, and the hints from 0 to the number of cells.");

    private static void Map(RouteGroupBuilder game)
    {
        game.MapGet("/puzzle", async Task<Results<Ok<StampsPuzzleView>, BadRequest<ProblemDetail>>> (DateOnly date, FibotrailDbContext db, ServerSettings settings, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(date, time))
            {
                return GameDates.NotToday();
            }

            var library = await GameLibraryLoader.LoadAsync(db, cancellationToken);
            if (StampsPuzzle.Create(library, date) is not { } puzzle)
            {
                return TypedResults.Ok(new StampsPuzzleView(DailySeed.PuzzleNumber(date), date, Math.Max(1, StampsPuzzle.TripsNeeded(library)), 0, string.Empty, Weekday(date), StampsPuzzle.Rule, null, [], [], null));
            }

            var stored = await GameResultStore.FindAsync(db, StampsPuzzle.Id, date, cancellationToken);
            var result = stored is null ? null : await ViewAsync(db, settings, puzzle, stored, cancellationToken);
            return TypedResults.Ok(new StampsPuzzleView(puzzle.Number, date, 0, puzzle.Size, puzzle.Label, Weekday(date), StampsPuzzle.Rule, puzzle.SizeNote, puzzle.RegionRows(), puzzle.Photos, result));
        }).WithName("GetStampsPuzzle");

        game.MapPost("/hint", async Task<Results<Ok<StampsHintView>, BadRequest<ProblemDetail>>> (StampsHintRequest request, FibotrailDbContext db, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(request.Date, time))
            {
                return GameDates.NotToday();
            }

            if (StampsPuzzle.Create(await GameLibraryLoader.LoadAsync(db, cancellationToken), request.Date) is not { } puzzle)
            {
                return TypedResults.BadRequest(NoPuzzle);
            }

            var stamps = request.Stamps ?? [];
            var crosses = request.Crosses ?? [];
            if (!puzzle.IsOnBoard(stamps, crosses))
            {
                return TypedResults.BadRequest(NotOnBoard);
            }

            // A hint is worked out from the board you send, and nothing is stored.
            var step = puzzle.Hint(stamps, crosses);
            return TypedResults.Ok(step is null
                ? new StampsHintView(null, false, false, "Nothing is left to find. Every stamp is in place.")
                : new StampsHintView(step.Cell, step.Stamp, step.Rule is StampsRule.WrongStamp or StampsRule.WrongCross, step.Reason));
        }).WithName("PostStampsHint");

        game.MapPost("/results", async Task<Results<Ok<StampsResultView>, BadRequest<ProblemDetail>>> (StampsResultRequest request, FibotrailDbContext db, ServerSettings settings, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(request.Date, time))
            {
                return GameDates.NotToday();
            }

            if (StampsPuzzle.Create(await GameLibraryLoader.LoadAsync(db, cancellationToken), request.Date) is not { } puzzle)
            {
                return TypedResults.BadRequest(NoPuzzle);
            }

            // A day already played keeps its first result, whatever this send holds.
            var stored = await GameResultStore.FindAsync(db, StampsPuzzle.Id, request.Date, cancellationToken);
            if (stored is null)
            {
                // The server grades the stamps. The time and hints are counted in the browser, so
                // they're only checked to be in range.
                var stamps = request.Stamps ?? [];
                if (!puzzle.IsSolvedBy(stamps))
                {
                    return TypedResults.BadRequest(NotSolved);
                }

                if (request.Seconds < 1 || request.Seconds > StampsPuzzle.MostSeconds || request.Hints < 0 || request.Hints > puzzle.Size * puzzle.Size)
                {
                    return TypedResults.BadRequest(NotCounted);
                }

                stored = await GameResultStore.AddOnceAsync(db, new GameResult
                {
                    Game = StampsPuzzle.Id,
                    PuzzleDate = request.Date,
                    Solved = true,
                    Tries = 1,
                    MaxTries = MaxTries,
                    Marks = StampsPuzzle.Encode(puzzle.Size, stamps, request.Hints),
                    Seconds = request.Seconds,
                    PlayedAt = time.GetUtcNow().UtcDateTime,
                }, cancellationToken);
            }

            var view = await ViewAsync(db, settings, puzzle, stored, cancellationToken);

            // The share text is kept with the result, for Share today on the Games screen.
            await GameResultStore.KeepShareAsync(db, stored, view.Share, cancellationToken);
            return TypedResults.Ok(view);
        }).WithName("PostStampsResult");
    }

    private static string Weekday(DateOnly date) => date.ToString("ddd", CultureInfo.InvariantCulture);

    private static async Task<StampsResultView> ViewAsync(FibotrailDbContext db, ServerSettings settings, StampsPuzzle puzzle, GameResult stored, CancellationToken cancellationToken)
    {
        var size = StampsPuzzle.SizeOf(stored.Marks);
        var hints = StampsPuzzle.HintsOf(stored.Marks);
        var seconds = stored.Seconds ?? 0;
        var stats = await GameResultStore.StatsAsync(db, StampsPuzzle.Id, MaxTries, puzzle.Date, StampsPuzzle.TimeLabel, cancellationToken);
        var passport = puzzle.Trips
            .OrderBy(trip => trip.StartDate)
            .ThenBy(trip => trip.TripId)
            .Select(trip => new StampsPassportView(
                trip.StampCell,
                StampsPuzzle.Letter(trip.Region),
                trip.PhotoId,
                trip.TripId,
                trip.TripName,
                trip.StartDate,
                trip.EndDate,
                TripPage.ImmichPhotoUrl(settings.ImmichExternalUrl, trip.PhotoId)))
            .ToList();
        return new StampsResultView(stored.Solved, size, seconds, hints, StampsPuzzle.ShareText(puzzle.Number, puzzle.Date, size, seconds, hints), passport, stats);
    }
}
