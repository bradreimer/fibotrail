using Fibotrail.Detection.Parks;
using Fibotrail.Places;
using Fibotrail.Stats;

namespace Fibotrail.Mcp.Tests;

/// <summary>
/// A small made-up library: three Disney trips (4, 5, and 5 days), a Universal trip, a Sweden trip,
/// and a Japan trip, with squares for countries and parks. The numbers below are worked out by hand.
/// </summary>
internal static class Fixture
{
    public static readonly CountryOutline[] Outlines =
    [
        Square("840", "United States of America", 20, -125, 50, -70),
        Square("752", "Sweden", 55, 11, 69, 24),
        Square("392", "Japan", 30, 129, 45, 146),
        Square("208", "Denmark", 54, 8, 58, 13),
    ];

    private static readonly ParkPlace MagicKingdom = Park("magic-kingdom", "Magic Kingdom", "Walt Disney World Resort", 28.41, -81.59);
    private static readonly ParkPlace Epcot = Park("epcot", "EPCOT", "Walt Disney World Resort", 28.36, -81.56);
    private static readonly ParkPlace Disneyland = Park("disneyland-park", "Disneyland Park", "Disneyland Resort", 33.80, -117.92);
    private static readonly ParkPlace UniversalStudiosFlorida = Park("universal-studios-florida", "Universal Studios Florida", "Universal Orlando Resort", 28.47, -81.47);

    public static readonly IReadOnlyList<McpCatalogEntry> Collections =
    [
        new("disney-parks", "Disney parks",
        [
            new("magic-kingdom", "Magic Kingdom", "Walt Disney World Resort", false),
            new("epcot", "EPCOT", "Walt Disney World Resort", false),
            new("disneyland-park", "Disneyland Park", "Disneyland Resort", false),
            new("abu-dhabi", "Disney Abu Dhabi", "Abu Dhabi", true),
        ]),
        new("universal-parks", "Universal parks",
        [
            new("universal-studios-florida", "Universal Studios Florida", "Universal Orlando Resort", false),
        ]),
    ];

    public static readonly ParkLookup Lookup = new([MagicKingdom, Epcot, Disneyland, UniversalStudiosFlorida]);

    private static CountryOutline Square(string id, string name, double south, double west, double north, double east) =>
        new(id, name, [[[(west, south), (east, south), (east, north), (west, north), (west, south)]]]);

    private static ParkPlace Park(string id, string name, string group, double south, double west) =>
        new(id, name, group, 1, 0, [[[new(south, west), new(south, west + 0.02), new(south + 0.02, west + 0.02), new(south + 0.02, west), new(south, west)]]]);

    private static (double Latitude, double Longitude) Center(ParkPlace park)
    {
        var ring = park.Polygons[0][0];
        return ((ring[0].Latitude + ring[2].Latitude) / 2, (ring[0].Longitude + ring[2].Longitude) / 2);
    }

    public static (double Latitude, double Longitude) InMagicKingdom => Center(MagicKingdom);

    public static (double Latitude, double Longitude) InDisneyland => Center(Disneyland);

    public static (double Latitude, double Longitude) InUniversal => Center(UniversalStudiosFlorida);

    public static StatsTrip Trip(int id, string name, DateOnly start, int days, (double Latitude, double Longitude) place, params int[] photoDayOffsets) =>
        Trip(id, name, start, days, [.. photoDayOffsets.Select(offset => (place, offset))]);

    public static StatsTrip Trip(int id, string name, DateOnly start, int days, IEnumerable<((double Latitude, double Longitude) Place, int Offset)> photos, IReadOnlyList<string>? marked = null, double distanceKm = 1000)
    {
        var list = photos.ToList();
        return new StatsTrip(
            id, name, start, start.AddDays(days - 1), days, distanceKm, distanceKm / 2,
            [.. list.Select(photo => photo.Place).Distinct()],
            [.. list.Select(photo => new PhotoPosition(start.AddDays(photo.Offset), photo.Place.Latitude, photo.Place.Longitude))],
            MarkedParks: marked);
    }

    /// <summary>The trips of the made-up library. Disney trips 1, 2, 3 have 4, 5, and 5 days and 14 park days in all.</summary>
    public static List<StatsTrip> Trips()
    {
        var mk = InMagicKingdom;
        var dl = InDisneyland;
        return
        [
            Trip(1, "Spring break", new DateOnly(2019, 3, 1), 4, mk, 0, 1, 2, 3),
            Trip(2, "Summer", new DateOnly(2021, 6, 10), 5, mk, 0, 1, 2, 3, 4),
            Trip(3, "Mom's 70th", new DateOnly(2023, 11, 1), 5, [(mk, 0), (mk, 1), (mk, 2), (dl, 3), (dl, 4)]),
            Trip(4, "Midsummer", new DateOnly(2022, 7, 1), 7, (59, 15), 0, 1, 2, 3, 4, 5, 6),
            Trip(5, "Cherry blossoms", new DateOnly(2024, 4, 1), 6, (35, 139), 0, 1, 2, 3, 4, 5),
            Trip(6, "Universal", new DateOnly(2020, 1, 10), 3, InUniversal, 0, 1, 2),
        ];
    }

    public static StatsLibrary Library(IEnumerable<StatsTrip>? trips = null)
    {
        var finder = new CountryFinder(Outlines);
        return StatsLibrary.Build(trips ?? Trips(), finder, Outlines, Lookup);
    }

    public static McpData Data(IEnumerable<StatsTrip>? trips = null) => new(Library(trips), Collections);

    public static T Run<T>(string tool, McpData data, string json = "{}")
        where T : class
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return (T)McpTools.Find(tool)!.Run(data, new ToolArgs(document.RootElement.Clone()));
    }
}
