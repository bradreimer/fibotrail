using Fibotrail.Stats;

namespace Fibotrail.Mcp;

/// <summary>A place of a collection, as the tools read it.</summary>
/// <param name="ParkId">The ID that the stats library uses for the park.</param>
/// <param name="Resort">The resort the park belongs to, such as "Walt Disney World Resort".</param>
/// <param name="Planned">True for a park that hasn't opened. It isn't counted.</param>
public sealed record McpPlace(string ParkId, string Name, string Resort, bool Planned);

/// <summary>A collection, such as <c>disney-parks</c>.</summary>
public sealed record McpCatalogEntry(string Id, string Name, IReadOnlyList<McpPlace> Places);

/// <summary>
/// Everything a tool reads: the stats library of the added trips, and the collection files. The
/// server builds it once for each call, from the same code the screens use, so a tool and a
/// screen can't disagree. See ADR-018.
/// </summary>
public sealed record McpData(StatsLibrary Library, IReadOnlyList<McpCatalogEntry> Collections);
