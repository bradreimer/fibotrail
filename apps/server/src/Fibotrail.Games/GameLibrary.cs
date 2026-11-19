namespace Fibotrail.Games;

/// <summary>
/// The games API: the one input of every game. It holds the added trips and their photos as plain
/// data, so a game never reads the database, Immich, or another feature. A game that needs a new
/// fact adds it here. See docs/specs/games.md and ADR-022.
/// </summary>
public sealed record GameLibrary(IReadOnlyList<GameTrip> Trips)
{
    public static readonly GameLibrary Empty = new([]);
}

/// <summary>An added trip.</summary>
/// <param name="Place">The main destination, such as a city, or null when none is known.</param>
public sealed record GameTrip(int Id, string Name, DateOnly StartDate, DateOnly EndDate, string? Place, IReadOnlyList<GamePhoto> Photos);

/// <summary>An image on a trip, not a video and not on the way.</summary>
/// <param name="Id">The Immich asset ID, for the thumbnail.</param>
/// <param name="LocalTime">The wall-clock time at the place of capture.</param>
public sealed record GamePhoto(string Id, DateTime LocalTime);
