namespace Fibotrail.Mcp;

// Every property of every result carries a [Coarse] kind, or [Nested] when it holds another result.
// A test walks these types and fails on a property with neither. See docs/specs/mcp-server.md.

/// <summary>One collection in the list.</summary>
public sealed record CollectionEntry(
    [property: Coarse(Coarse.Identifier)] string Id,
    [property: Coarse(Coarse.Place)] string Name,
    [property: Coarse(Coarse.Count)] int Trips,
    [property: Coarse(Coarse.Count)] int Days,
    [property: Coarse(Coarse.Count)] int ParksVisited,
    [property: Coarse(Coarse.Count)] int ParksTotal);

/// <summary>The result of <c>list_collections</c>.</summary>
public sealed record CollectionList(
    [property: Nested] IReadOnlyList<CollectionEntry> Collections);

/// <summary>A resort of a collection.</summary>
public sealed record ResortEntry(
    [property: Coarse(Coarse.Place)] string Name,
    [property: Coarse(Coarse.Count)] int Trips,
    [property: Coarse(Coarse.Count)] int Days);

/// <summary>A visited park of a collection.</summary>
public sealed record ParkEntry(
    [property: Coarse(Coarse.Place)] string Name,
    [property: Coarse(Coarse.Place)] string Resort,
    [property: Coarse(Coarse.Count)] int Trips,
    [property: Coarse(Coarse.Count)] int Days,
    [property: Coarse(Coarse.Date)] DateOnly FirstVisit,
    [property: Coarse(Coarse.Date)] DateOnly LastVisit,
    [property: Coarse(Coarse.Flag)] bool MarkedByYou);

/// <summary>The result of <c>collection_visits</c>.</summary>
public sealed record CollectionVisits(
    [property: Coarse(Coarse.Identifier)] string Id,
    [property: Coarse(Coarse.Place)] string Collection,
    [property: Coarse(Coarse.Count)] int Trips,
    [property: Coarse(Coarse.Count)] int Days,
    [property: Coarse(Coarse.Count)] int ParksVisited,
    [property: Coarse(Coarse.Count)] int ParksTotal,
    [property: Coarse(Coarse.Date)] DateOnly? FirstVisit,
    [property: Coarse(Coarse.Date)] DateOnly? LastVisit,
    [property: Nested] IReadOnlyList<ResortEntry> Resorts,
    [property: Nested] IReadOnlyList<ParkEntry> Parks);

/// <summary>A visited country.</summary>
public sealed record CountryEntry(
    [property: Coarse(Coarse.Identifier)] string Id,
    [property: Coarse(Coarse.Country)] string Name,
    [property: Coarse(Coarse.Continent)] string Continent,
    [property: Coarse(Coarse.Count)] int Trips,
    [property: Coarse(Coarse.Count)] int Days);

/// <summary>The result of <c>list_countries</c>.</summary>
/// <param name="Scope">"World", or the name of the continent that was asked for.</param>
/// <param name="Visited">Visited countries in the scope.</param>
/// <param name="Total">Countries in the scope, by the bundled outlines.</param>
public sealed record CountryList(
    [property: Coarse(Coarse.Continent)] string Scope,
    [property: Coarse(Coarse.Count)] int Visited,
    [property: Coarse(Coarse.Count)] int Total,
    [property: Nested] IReadOnlyList<CountryEntry> Countries);

/// <summary>The result of <c>country_summary</c>.</summary>
public sealed record CountrySummary(
    [property: Coarse(Coarse.Identifier)] string Id,
    [property: Coarse(Coarse.Country)] string Name,
    [property: Coarse(Coarse.Continent)] string Continent,
    [property: Coarse(Coarse.Count)] int Trips,
    [property: Coarse(Coarse.Count)] int Days,
    [property: Coarse(Coarse.Date)] DateOnly FirstVisit,
    [property: Coarse(Coarse.Date)] DateOnly LastVisit);

/// <summary>An added trip. It has an ID and no name, because a name is text you typed.</summary>
/// <param name="MainCity">The trip's main city, or null when no photo has a city. The only city in any result.</param>
public sealed record TripEntry(
    [property: Coarse(Coarse.TripId)] int Id,
    [property: Coarse(Coarse.Date)] DateOnly StartDate,
    [property: Coarse(Coarse.Date)] DateOnly EndDate,
    [property: Coarse(Coarse.Count)] int Days,
    [property: Coarse(Coarse.DistanceKm)] double DistanceKm,
    [property: Coarse(Coarse.DistanceKm)] double FarthestFromHomeKm,
    [property: Coarse(Coarse.City)] string? MainCity,
    [property: Coarse(Coarse.Country)] IReadOnlyList<string> Countries,
    [property: Coarse(Coarse.Place)] IReadOnlyList<string> Collections,
    [property: Coarse(Coarse.Place)] IReadOnlyList<string> Parks);

/// <summary>The result of <c>list_trips</c>.</summary>
/// <param name="Total">Trips that match the filters.</param>
/// <param name="Truncated">True when more trips match than the list holds.</param>
public sealed record TripList(
    [property: Coarse(Coarse.Count)] int Total,
    [property: Coarse(Coarse.Count)] int Returned,
    [property: Coarse(Coarse.Flag)] bool Truncated,
    [property: Coarse(Coarse.Note)] string? Note,
    [property: Nested] IReadOnlyList<TripEntry> Trips);

/// <summary>One calendar month or year with trip days in it.</summary>
/// <param name="Period">"2023" or "2023-07".</param>
public sealed record PeriodEntry(
    [property: Coarse(Coarse.Identifier)] string Period,
    [property: Coarse(Coarse.Count)] int Days);

/// <summary>The result of <c>days_away</c>.</summary>
public sealed record DaysAway(
    [property: Coarse(Coarse.Identifier)] string By,
    [property: Coarse(Coarse.Count)] int TotalDays,
    [property: Nested] IReadOnlyList<PeriodEntry> Periods);
