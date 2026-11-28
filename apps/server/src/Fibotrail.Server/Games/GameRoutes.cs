using Fibotrail.Games;
using Fibotrail.Server.Data;
using Fibotrail.Server.Games.Fetch;
using Fibotrail.Server.Games.Pin;
using Fibotrail.Server.Games.Slide;
using Fibotrail.Server.Games.Stamps;
using Fibotrail.Server.Games.Timeline;
using Fibotrail.Server.Immich;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Fibotrail.Server.Games;

/// <summary>
/// A game, as the hub knows it. <paramref name="MapRoutes"/> maps the game's own routes under
/// <c>/api/games/{Id}/</c>.
/// </summary>
/// <param name="TimeLabel">For a timed game, reads the label its time stats are split by from a result's marks. Null for other games.</param>
public sealed record GameDefinition(string Id, string Name, string Description, int MaxTries, Action<RouteGroupBuilder> MapRoutes, Func<string, string?>? TimeLabel = null);

/// <summary>A card on the Games screen.</summary>
/// <param name="SolvedToday">Null when today isn't played yet.</param>
/// <param name="Share">Today's share text, for Share today. Null when today isn't played, or its result has no stored text.</param>
public sealed record GameEntry(string Id, string Name, string Description, bool PlayedToday, bool? SolvedToday, GameStats Stats, string? Share);

/// <summary>
/// The list of games. Adding or removing a game is one line here and one in the web registry. See
/// ADR-022.
/// </summary>
public static class GameRegistry
{
    public static readonly IReadOnlyList<GameDefinition> Games =
    [
        TimelineRoutes.Definition,
        SlideRoutes.Definition,
        PinRoutes.Definition,
        FetchRoutes.Definition,
        StampsRoutes.Definition,
    ];
}

/// <summary>Which dates a game accepts as "today".</summary>
public static class GameDates
{
    /// <summary>
    /// The browser sends its own today. Every time zone's today is within a day of the server's, so
    /// any other date is refused.
    /// </summary>
    public static bool IsToday(DateOnly date, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var server = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        return Math.Abs(date.DayNumber - server.DayNumber) <= 1;
    }

    public static BadRequest<ProblemDetail> NotToday() =>
        TypedResults.BadRequest(new ProblemDetail("Only today's puzzle can be played. Check the date on this device."));
}

/// <summary>The routes of the Games tab: the list of games, then each game's own routes.</summary>
public static class GameRoutes
{
    public static void MapGameRoutes(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapGet("/games", async Task<Results<Ok<IReadOnlyList<GameEntry>>, BadRequest<ProblemDetail>>> (DateOnly date, FibotrailDbContext db, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!GameDates.IsToday(date, time))
            {
                return GameDates.NotToday();
            }

            var entries = new List<GameEntry>();
            foreach (var game in GameRegistry.Games)
            {
                var today = await GameResultStore.FindAsync(db, game.Id, date, cancellationToken);
                var stats = await GameResultStore.StatsAsync(db, game.Id, game.MaxTries, date, game.TimeLabel, cancellationToken);
                entries.Add(new GameEntry(game.Id, game.Name, game.Description, today is not null, today?.Solved, stats, today?.Share));
            }

            return TypedResults.Ok<IReadOnlyList<GameEntry>>(entries);
        }).WithName("GetGames");

        foreach (var game in GameRegistry.Games)
        {
            game.MapRoutes(api.MapGroup($"/games/{game.Id}"));
        }
    }
}
