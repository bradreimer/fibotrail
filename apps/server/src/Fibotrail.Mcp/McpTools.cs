using Fibotrail.Detection.Parks;
using Fibotrail.Places;
using Fibotrail.Stats;

namespace Fibotrail.Mcp;

/// <summary>
/// The tools. Each is a pure function of <see cref="McpData"/>, and each reads the same stats
/// library and park numbers the screens read, so a tool and a screen give one answer. A result
/// holds only the kinds in <see cref="Coarse"/>. See ADR-018 and docs/specs/mcp-server.md.
/// </summary>
public static class McpTools
{
    public const int DefaultTripLimit = 50;
    public const int MaxTripLimit = 200;

    private const string Rules =
        "Only trips you have added count. Days are local days with at least one photo that has a position; " +
        "a day with photos in two places counts for both. Photos on the way count for nothing.";

    public static IReadOnlyList<McpTool> All { get; } =
    [
        new(
            "list_collections",
            "Lists the collections of places, such as Disney parks and Universal parks, with each one's separate trips, " +
            "park days, and parks visited out of the total. Use the returned id with collection_visits. " + Rules,
            """{"type":"object","properties":{},"additionalProperties":false}""",
            typeof(CollectionList),
            (data, _) => ListCollections(data)),
        new(
            "collection_visits",
            "How often you have been to the places of one collection, such as Disney parks. 'trips' counts separate trips " +
            "that visited at least one park; a trip to two parks counts once. 'days' is the park days: the days of each park added " +
            "together, so a day in two parks counts for both. A park you marked by hand adds a trip and no days. " +
            "The answer to 'How many times have I been to a Disney park?' is trips, and days says how long. " + Rules,
            """{"type":"object","properties":{"collection":{"type":"string","description":"The collection id, such as disney-parks, or its name. See list_collections."}},"required":["collection"],"additionalProperties":false}""",
            typeof(CollectionVisits),
            CollectionVisitsOf),
        new(
            "list_countries",
            "Lists the countries you have visited, with trips and days in each, most days first. A country is visited when an " +
            "added trip has a stop in it. Pass a continent to see only that continent, with its visited and total counts. " + Rules,
            """{"type":"object","properties":{"continent":{"type":"string","description":"Optional. A continent such as Europe or North America."}},"additionalProperties":false}""",
            typeof(CountryList),
            CountriesOf),
        new(
            "country_summary",
            "One visited country: its continent, trips, days, first visit, and last visit. " + Rules,
            """{"type":"object","properties":{"country":{"type":"string","description":"The country name, such as Sweden, or its id."}},"required":["country"],"additionalProperties":false}""",
            typeof(CountrySummary),
            CountrySummaryOf),
        new(
            "list_trips",
            "Lists added trips, newest first, with dates, days, distance, main city, countries, collections, and parks. A trip has an id " +
            "and no name. The main city is where the trip spent the most days, the same city the trip card shows; it is null when no " +
            "photo has a city, and it is the only city a trip reports. The city filter matches the main city only. " +
            "A date range matches a trip that has any day inside it, and the trip's numbers are for the whole trip. " +
            "The list holds at most 200 trips and says when it was cut short.",
            """{"type":"object","properties":{"from":{"type":"string","description":"Optional first date, such as 2023-01-01."},"to":{"type":"string","description":"Optional last date, such as 2023-12-31."},"country":{"type":"string","description":"Optional country name or id."},"collection":{"type":"string","description":"Optional collection id or name."},"city":{"type":"string","description":"Optional. A main city, such as Orlando. Trips that only passed through it aren't listed."},"limit":{"type":"integer","description":"Optional. Most trips to return. The default is 50 and the most is 200."}},"additionalProperties":false}""",
            typeof(TripList),
            TripsOf),
        new(
            "get_trip",
            "One added trip by its id, with the same fields as list_trips.",
            """{"type":"object","properties":{"id":{"type":"integer","description":"The trip id, from list_trips."}},"required":["id"],"additionalProperties":false}""",
            typeof(TripEntry),
            TripOf),
        new(
            "days_away",
            "Days away from home, by calendar month or calendar year. Every day from the first to the last day of a trip counts, " +
            "so the total is the 'days away' of the Insights screen. Only periods with a trip are listed.",
            """{"type":"object","properties":{"by":{"type":"string","enum":["month","year"],"description":"Group by month or by year."}},"required":["by"],"additionalProperties":false}""",
            typeof(DaysAway),
            DaysAwayOf),
    ];

    /// <summary>The tool with this name, or null.</summary>
    public static McpTool? Find(string name) => All.FirstOrDefault(tool => string.Equals(tool.Name, name, StringComparison.Ordinal));

    private static bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    // ---- collections ----

    private static CollectionVisits Visits(McpCatalogEntry collection, StatsLibrary library)
    {
        var open = collection.Places.Where(place => !place.Planned).ToList();
        var openIds = open.Select(place => place.ParkId).ToHashSet(StringComparer.Ordinal);
        var stats = library.Parks.Where(park => openIds.Contains(park.Place.Id)).ToList();
        var byId = stats.ToDictionary(park => park.Place.Id, StringComparer.Ordinal);

        var resorts = open
            .GroupBy(place => place.Resort, StringComparer.Ordinal)
            .Select(group =>
            {
                var visited = group.Where(place => byId.ContainsKey(place.ParkId)).Select(place => byId[place.ParkId]).ToList();
                return new ResortEntry(group.Key, ParkTrips.Distinct(visited), visited.Sum(park => park.Days));
            })
            .ToList();

        var parks = stats
            .OrderByDescending(park => park.Days)
            .ThenBy(park => park.Place.Name, StringComparer.Ordinal)
            .Select(park => new ParkEntry(
                park.Place.Name,
                park.Place.Group,
                park.Trips.Count,
                park.Days,
                park.FirstVisit,
                park.LastVisit,
                park.Trips.All(visit => visit.Marked)))
            .ToList();

        return new CollectionVisits(
            collection.Id,
            collection.Name,
            ParkTrips.Distinct(stats),
            stats.Sum(park => park.Days),
            stats.Count,
            open.Count,
            stats.Count == 0 ? null : stats.Min(park => park.FirstVisit),
            stats.Count == 0 ? null : stats.Max(park => park.LastVisit),
            resorts,
            parks);
    }

    private static CollectionList ListCollections(McpData data) =>
        new([.. data.Collections.Select(collection =>
        {
            var visits = Visits(collection, data.Library);
            return new CollectionEntry(collection.Id, collection.Name, visits.Trips, visits.Days, visits.ParksVisited, visits.ParksTotal);
        })]);

    private static McpCatalogEntry FindCollection(McpData data, string input) =>
        data.Collections.FirstOrDefault(collection => Same(collection.Id, input) || Same(collection.Name, input))
        ?? throw new ToolInputException(
            data.Collections.Count == 0
                ? $"There is no collection '{input}'. No collections are installed."
                : $"There is no collection '{input}'. The collections are: {string.Join(", ", data.Collections.Select(collection => $"{collection.Id} ({collection.Name})"))}.");

    private static CollectionVisits CollectionVisitsOf(McpData data, ToolArgs args) =>
        Visits(FindCollection(data, args.RequiredText("collection")), data.Library);

    // ---- countries ----

    private static Continent? FindContinent(string input) =>
        Continents.All.Cast<Continent?>().FirstOrDefault(continent => Same(Continents.Name(continent!.Value), input) || Same(Continents.Slug(continent.Value), input));

    private static CountryEntry Entry(CountryStats country) =>
        new(country.Country.Key, country.Country.Name, Continents.Name(country.Continent), country.Trips.Count, country.Days);

    private static CountryList CountriesOf(McpData data, ToolArgs args)
    {
        var library = data.Library;
        if (args.Text("continent") is not { } input)
        {
            return new CountryList("World", library.Countries.Count, library.OutlineCountries, [.. library.Countries.Select(Entry)]);
        }

        var continent = FindContinent(input)
            ?? throw new ToolInputException($"There is no continent '{input}'. The continents are: {string.Join(", ", Continents.All.Select(Continents.Name))}.");
        var countries = library.CountriesOn(continent);
        return new CountryList(Continents.Name(continent), countries.Count, library.ContinentTotals[continent], [.. countries.Select(Entry)]);
    }

    private static CountryStats FindCountry(StatsLibrary library, string input)
    {
        var matches = library.Countries.Where(country => Same(country.Country.Key, input) || Same(country.Country.Name, input)).ToList();
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new ToolInputException($"No added trip has a stop in '{input}'. Use list_countries to see the countries you have visited."),
            _ => throw new ToolInputException($"'{input}' matches {matches.Count} countries: {string.Join(", ", matches.Select(country => $"{country.Country.Name} ({country.Country.Key})"))}. Use the id."),
        };
    }

    private static CountrySummary CountrySummaryOf(McpData data, ToolArgs args)
    {
        var country = FindCountry(data.Library, args.RequiredText("country"));
        return new CountrySummary(
            country.Country.Key,
            country.Country.Name,
            Continents.Name(country.Continent),
            country.Trips.Count,
            country.Days,
            country.FirstVisit,
            country.LastVisit);
    }

    // ---- trips ----

    private static TripEntry ToEntry(StatsTrip trip, McpData data, Dictionary<int, List<string>> parksByTrip)
    {
        var library = data.Library;
        var countries = library.Countries
            .Where(country => country.Trips.Any(visit => visit.TripId == trip.Id))
            .Select(country => country.Country.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        var parks = parksByTrip.GetValueOrDefault(trip.Id, []);
        var parkIds = ParkIdsOf(library, trip.Id);
        var collections = data.Collections
            .Where(collection => collection.Places.Any(place => !place.Planned && parkIds.Contains(place.ParkId)))
            .Select(collection => collection.Name)
            .ToList();
        return new TripEntry(
            trip.Id,
            trip.StartDate,
            trip.EndDate,
            trip.Days,
            Math.Round(trip.DistanceKm, 1),
            Math.Round(trip.FarthestFromHomeKm, 1),
            trip.MainCity,
            countries,
            collections,
            [.. parks.Order(StringComparer.Ordinal)]);
    }

    private static HashSet<string> ParkIdsOf(StatsLibrary library, int tripId) =>
        library.Parks.Where(park => park.Trips.Any(visit => visit.TripId == tripId)).Select(park => park.Place.Id).ToHashSet(StringComparer.Ordinal);

    private static Dictionary<int, List<string>> ParksByTrip(StatsLibrary library)
    {
        var result = new Dictionary<int, List<string>>();
        foreach (var park in library.Parks)
        {
            foreach (var visit in park.Trips)
            {
                if (!result.TryGetValue(visit.TripId, out var list)) result[visit.TripId] = list = [];
                list.Add(park.Place.Name);
            }
        }

        return result;
    }

    private static TripList TripsOf(McpData data, ToolArgs args)
    {
        var library = data.Library;
        var from = args.Date("from");
        var to = args.Date("to");
        var limit = Math.Clamp(args.WholeNumber("limit") ?? DefaultTripLimit, 1, MaxTripLimit);

        HashSet<int>? inCountry = null;
        if (args.Text("country") is { } countryInput)
        {
            inCountry = [.. FindCountry(library, countryInput).Trips.Select(visit => visit.TripId)];
        }

        HashSet<int>? inCollection = null;
        if (args.Text("collection") is { } collectionInput)
        {
            var collection = FindCollection(data, collectionInput);
            var openIds = collection.Places.Where(place => !place.Planned).Select(place => place.ParkId).ToHashSet(StringComparer.Ordinal);
            inCollection = [.. library.Parks.Where(park => openIds.Contains(park.Place.Id)).SelectMany(park => park.Trips).Select(visit => visit.TripId)];
        }

        var city = args.Text("city");

        var matching = library.Trips
            .Where(trip => (from is null || trip.EndDate >= from) && (to is null || trip.StartDate <= to))
            .Where(trip => inCountry is null || inCountry.Contains(trip.Id))
            .Where(trip => inCollection is null || inCollection.Contains(trip.Id))
            .Where(trip => city is null || Same(trip.MainCity, city))
            .OrderByDescending(trip => trip.StartDate)
            .ThenByDescending(trip => trip.Id)
            .ToList();

        var parksByTrip = ParksByTrip(library);
        var listed = matching.Take(limit).Select(trip => ToEntry(trip, data, parksByTrip)).ToList();
        var truncated = matching.Count > listed.Count;
        return new TripList(
            matching.Count,
            listed.Count,
            truncated,
            truncated ? $"The list is cut short: {matching.Count} trips match and {listed.Count} are listed. Narrow the dates, or filter by country, collection, or city." : null,
            listed);
    }

    private static TripEntry TripOf(McpData data, ToolArgs args)
    {
        var id = args.WholeNumber("id") ?? throw new ToolInputException("'id' is required.");
        var trip = data.Library.Trips.FirstOrDefault(candidate => candidate.Id == id)
            ?? throw new ToolInputException($"No added trip has the id {id}. Use list_trips to see the ids.");
        return ToEntry(trip, data, ParksByTrip(data.Library));
    }

    // ---- days away ----

    private static DaysAway DaysAwayOf(McpData data, ToolArgs args)
    {
        var by = args.RequiredText("by").ToUpperInvariant() switch
        {
            "MONTH" => "month",
            "YEAR" => "year",
            _ => throw new ToolInputException("'by' must be \"month\" or \"year\"."),
        };

        var periods = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var trip in data.Library.Trips)
        {
            for (var day = trip.StartDate; day <= trip.EndDate; day = day.AddDays(1))
            {
                var key = by == "year" ? day.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture) : day.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
                periods[key] = periods.GetValueOrDefault(key) + 1;
            }
        }

        return new DaysAway(by, periods.Values.Sum(), [.. periods.Select(pair => new PeriodEntry(pair.Key, pair.Value))]);
    }
}
