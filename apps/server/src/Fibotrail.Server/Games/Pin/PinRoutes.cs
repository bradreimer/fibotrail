using Fibotrail.Games;
using Fibotrail.Games.Pin;
using Fibotrail.Server.Data;
using Fibotrail.Server.Immich;
using Fibotrail.Server.Trips;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Fibotrail.Server.Games.Pin;

/// <summary>Today's puzzle.</summary>
/// <param name="TripsNeeded">1 when no added trip has a photo with a location yet, and 0 when there's a puzzle.</param>
/// <param name="PhotoId">The photo to place, or null when there's no puzzle. No location, trip name, or date is sent while you play.</param>
/// <param name="Result">The day's result, when it's played.</param>
public sealed record PinPuzzleView(int Number, DateOnly Date, int MaxTries, int TripsNeeded, string? PhotoId, PinResultView? Result);

/// <summary>One pin to grade, without storing it.</summary>
public sealed record PinCheckRequest(DateOnly Date, GeoPoint Pin);

/// <summary>Every pin of a day, in order, for the server to grade and store.</summary>
public sealed record PinResultRequest(DateOnly Date, IReadOnlyList<GeoPoint> Pins);

/// <summary>The trip the photo comes from and where it was taken, shown at the end.</summary>
/// <param name="ImmichUrl">The photo in Immich, at the address your browser uses. Null when IMMICH_EXTERNAL_URL is empty.</param>
public sealed record PinAnswerView(string PhotoId, double Latitude, double Longitude, int TripId, string TripName, DateOnly StartDate, DateOnly EndDate, string? Place, string? ImmichUrl);

/// <summary>A day's graded result, with the answer and the stats after it.</summary>
/// <param name="Marks">The grade of each pin, in order.</param>
/// <param name="Share">The text to paste to friends. It has no distance, place, trip name, date, or photo.</param>
public sealed record PinResultView(bool Solved, int Tries, int MaxTries, IReadOnlyList<PinMark> Marks, string Share, PinAnswerView Answer, GameStats Stats);

/// <summary>The Pin game's routes. See docs/specs/pin-game.md.</summary>
public static class PinRoutes
{
    public static readonly GameDefinition Definition = new(
        PinPuzzle.Id,
        PinPuzzle.Name,
        "Pin the map where you took one of your trip photos.",
        PinPuzzle.MaxTries,
        Map);

    private static readonly ProblemDetail NoPuzzle = new("There's no puzzle today. Add a trip with photos that have a location.");
    private static readonly ProblemDetail NotValid = new("Each pin must be on the map, within the tries allowed, and none may follow a solved pin.");
    private static readonly ProblemDetail NotFinished = new("A game ends when it's solved or out of tries.");

    private static void Map(RouteGroupBuilder game)
    {
        game.MapGet("/puzzle", async Task<Results<Ok<PinPuzzleView>, BadRequest<ProblemDetail>>> (DateOnly date, FibotrailDbContext db, ServerSettings settings, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(date, time))
            {
                return GameDates.NotToday();
            }

            var library = await GameLibraryLoader.LoadAsync(db, cancellationToken);
            if (PinPuzzle.Create(library, date) is not { } puzzle)
            {
                return TypedResults.Ok(new PinPuzzleView(DailySeed.PuzzleNumber(date), date, PinPuzzle.MaxTries, PinPuzzle.TripsNeeded(library), null, null));
            }

            var stored = await GameResultStore.FindAsync(db, PinPuzzle.Id, date, cancellationToken);
            var result = stored is null ? null : await ViewAsync(db, settings, puzzle, stored, cancellationToken);
            return TypedResults.Ok(new PinPuzzleView(puzzle.Number, date, PinPuzzle.MaxTries, 0, puzzle.Photo.PhotoId, result));
        }).WithName("GetPinPuzzle");

        game.MapPost("/check", async Task<Results<Ok<PinMark>, BadRequest<ProblemDetail>>> (PinCheckRequest request, FibotrailDbContext db, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(request.Date, time))
            {
                return GameDates.NotToday();
            }

            var puzzle = PinPuzzle.Create(await GameLibraryLoader.LoadAsync(db, cancellationToken), request.Date);
            if (puzzle is null)
            {
                return TypedResults.BadRequest(NoPuzzle);
            }

            return puzzle.Grade(request.Pin) is { } mark ? TypedResults.Ok(mark) : TypedResults.BadRequest(NotValid);
        }).WithName("CheckPin");

        game.MapPost("/results", async Task<Results<Ok<PinResultView>, BadRequest<ProblemDetail>>> (PinResultRequest request, FibotrailDbContext db, ServerSettings settings, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(request.Date, time))
            {
                return GameDates.NotToday();
            }

            var puzzle = PinPuzzle.Create(await GameLibraryLoader.LoadAsync(db, cancellationToken), request.Date);
            if (puzzle is null)
            {
                return TypedResults.BadRequest(NoPuzzle);
            }

            // A day already played keeps its first result, whatever this send holds.
            var stored = await GameResultStore.FindAsync(db, PinPuzzle.Id, request.Date, cancellationToken);
            if (stored is null)
            {
                // The server grades the pins, so the browser never sends a distance or a score.
                var pins = request.Pins ?? [];
                if (puzzle.Score(pins) is not { } outcome)
                {
                    return TypedResults.BadRequest(NotValid);
                }

                if (!outcome.Solved && outcome.Marks.Count < PinPuzzle.MaxTries)
                {
                    return TypedResults.BadRequest(NotFinished);
                }

                stored = await GameResultStore.AddOnceAsync(db, new GameResult
                {
                    Game = PinPuzzle.Id,
                    PuzzleDate = request.Date,
                    Solved = outcome.Solved,
                    Tries = outcome.Marks.Count,
                    MaxTries = PinPuzzle.MaxTries,
                    Marks = PinPuzzle.Encode(outcome.Marks.Select(mark => mark.Pin)),
                    PlayedAt = time.GetUtcNow().UtcDateTime,
                }, cancellationToken);
            }

            return TypedResults.Ok(await ViewAsync(db, settings, puzzle, stored, cancellationToken));
        }).WithName("PostPinResult");
    }

    private static async Task<PinResultView> ViewAsync(FibotrailDbContext db, ServerSettings settings, PinPuzzle puzzle, GameResult stored, CancellationToken cancellationToken)
    {
        // The stored marks are the pins, so the grades come back from them. A stored result outlives
        // a library change, so a pin that no longer grades is skipped rather than failing the page.
        var marks = PinPuzzle.Decode(stored.Marks).Select(puzzle.Grade).OfType<PinMark>().ToList();
        var stats = await GameResultStore.StatsAsync(db, PinPuzzle.Id, PinPuzzle.MaxTries, puzzle.Date, cancellationToken);
        var photo = puzzle.Photo;
        return new PinResultView(
            stored.Solved,
            stored.Tries,
            stored.MaxTries,
            marks,
            PinPuzzle.ShareText(puzzle.Number, new PinOutcome(stored.Solved, marks)),
            new PinAnswerView(photo.PhotoId, photo.Location.Latitude, photo.Location.Longitude, photo.TripId, photo.TripName, photo.StartDate, photo.EndDate, photo.Place, TripPage.ImmichPhotoUrl(settings.ImmichExternalUrl, photo.PhotoId)),
            stats);
    }
}
