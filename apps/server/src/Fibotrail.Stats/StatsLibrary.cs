using Fibotrail.Detection;
using Fibotrail.Detection.Cities;
using Fibotrail.Detection.Parks;
using Fibotrail.Places;

namespace Fibotrail.Stats;

/// <summary>An away photo with a position, reduced to the local day it was taken and where.</summary>
public sealed record PhotoPosition(DateOnly Day, double Latitude, double Longitude);

/// <summary>A stored stop with its place name and photo count, for grouping stops into cities.</summary>
public sealed record NamedStop(string? Name, double Latitude, double Longitude, int PhotoCount);

/// <summary>An added trip, as the stats library reads it.</summary>
/// <param name="Stops">The stored stops. Photos on the way aren't among them.</param>
/// <param name="Photos">The positioned away photos of the trip, without photos on the way.</param>
/// <param name="MainCity">
/// The trip's main place when it is a city, by the main destination rule. Null when no photo of the
/// trip has a city, so the main place is a state or a country. See ADR-023.
/// </param>
public sealed record StatsTrip(
    int Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    int Days,
    double DistanceKm,
    double FarthestFromHomeKm,
    IReadOnlyList<(double Latitude, double Longitude)> Stops,
    IReadOnlyList<PhotoPosition> Photos,
    IReadOnlyList<NamedStop>? NamedStops = null,
    IReadOnlyList<string>? MarkedParks = null,
    string? MainCity = null);

/// <summary>A local day with a positioned away photo in a country, and the latitude of the first such photo.</summary>
public sealed record CountryDay(DateOnly Day, double Latitude);

/// <summary>A country with its trips and days, as the MCP server reports them.</summary>
/// <param name="Trips">Added trips with a stop in the country, earliest first.</param>
/// <param name="Days">Distinct local days with at least one positioned away photo in the country.</param>
/// <param name="DayList">Those days, oldest first, each with a latitude. <c>Days</c> is its length.</param>
public sealed record CountryStats(Country Country, Continent Continent, DateOnly FirstVisit, IReadOnlyList<VisitedTrip> Trips, int Days, IReadOnlyList<CountryDay> DayList)
{
    /// <summary>
    /// The latest local day with a positioned away photo in the country. A country with no such day
    /// uses the last day of its latest trip.
    /// </summary>
    public DateOnly LastVisit => DayList.Count > 0 ? DayList[^1].Day : Trips.Max(trip => trip.EndDate);
}

/// <summary>The totals at the top of the Insights screen.</summary>
public sealed record StatsTotals(int Trips, int DaysAway, double DistanceKm, int Countries, int Continents);

/// <summary>
/// The shared, read-only input of the Insights totals, the collections, and the MCP server: the
/// added trips and the numbers worked out per country, by the rules of docs/specs/insights.md.
/// Pure: callers pass the added trips and the outlines, so it is tested with plain data. See ADR-019.
/// </summary>
public sealed class StatsLibrary
{
    private StatsLibrary(IReadOnlyList<StatsTrip> trips, IReadOnlyList<CountryStats> countries, IReadOnlyDictionary<Continent, int> totals, int outlineCountries, IReadOnlyList<ParkStats> parks, IReadOnlyList<VisitedPlace> places)
    {
        Trips = trips;
        Countries = countries;
        ContinentTotals = totals;
        OutlineCountries = outlineCountries;
        Parks = parks;
        Places = places;
    }

    /// <summary>The added trips, earliest first.</summary>
    public IReadOnlyList<StatsTrip> Trips { get; }

    /// <summary>The visited countries, most days first, then most trips, then name.</summary>
    public IReadOnlyList<CountryStats> Countries { get; }

    /// <summary>The number of countries on each continent.</summary>
    public IReadOnlyDictionary<Continent, int> ContinentTotals { get; }

    /// <summary>The number of bundled outlines that have an id.</summary>
    public int OutlineCountries { get; }

    /// <summary>The visited parks, most days first, by the rules of docs/specs/collections.md. Collections reads this.</summary>
    public IReadOnlyList<ParkStats> Parks { get; }

    /// <summary>The places of added trips: merged cities and parks, by the rules of ADR-015.</summary>
    public IReadOnlyList<VisitedPlace> Places { get; }

    /// <summary>The number of cities: places that aren't parks.</summary>
    public int CityCount => Places.Count(place => !place.IsPark);

    /// <summary>Builds the library. Pass added trips only.</summary>
    public static StatsLibrary Build(IEnumerable<StatsTrip> trips, CountryFinder finder, IEnumerable<CountryOutline> outlines, ParkLookup? parkLookup = null)
    {
        ArgumentNullException.ThrowIfNull(trips);
        ArgumentNullException.ThrowIfNull(finder);
        ArgumentNullException.ThrowIfNull(outlines);

        var ordered = trips.OrderBy(trip => trip.StartDate).ThenBy(trip => trip.Id).ToList();
        var outlineList = outlines.ToList();
        var visited = VisitedCountries.Find(
            ordered.Select(trip => new TripStops(trip.Id, trip.Name, trip.StartDate, trip.EndDate, trip.Stops)),
            finder);

        // Days: each local day counts for every country that has a positioned away photo that day.
        var daysByCountry = new Dictionary<string, Dictionary<DateOnly, double>>(StringComparer.Ordinal);
        foreach (var photo in ordered.SelectMany(trip => trip.Photos))
        {
            if (finder.Find(photo.Latitude, photo.Longitude)?.Country is not { } country) continue;
            if (!daysByCountry.TryGetValue(country.Key, out var days)) daysByCountry[country.Key] = days = [];
            days.TryAdd(photo.Day, photo.Latitude);
        }

        var countries = visited
            .Select(entry => (Entry: entry, Continent: Continents.Of(entry.Country)))
            .Where(item => item.Continent is not null)
            .Select(item =>
            {
                IReadOnlyList<CountryDay> dayList = daysByCountry.TryGetValue(item.Entry.Country.Key, out var days)
                    ? [.. days.OrderBy(pair => pair.Key).Select(pair => new CountryDay(pair.Key, pair.Value))]
                    : [];
                return new CountryStats(item.Entry.Country, item.Continent!.Value, item.Entry.FirstVisit, item.Entry.Trips, dayList.Count, dayList);
            })
            .OrderByDescending(country => country.Days)
            .ThenByDescending(country => country.Trips.Count)
            .ThenBy(country => country.Country.Name, StringComparer.Ordinal)
            .ToList();

        var parks = parkLookup is null
            ? []
            : ParkDays.Count(ordered.Select(trip => new ParkTrip(trip.Id, trip.Name, [.. trip.Photos.Select(photo => new ParkPhoto(photo.Day, photo.Latitude, photo.Longitude))], trip.StartDate, trip.MarkedParks, trip.EndDate)), parkLookup);

        var places = CityPlaces.Find(
            ordered.SelectMany(trip => (trip.NamedStops ?? []).Select(stop =>
                new PlaceStop(trip.Id, trip.Name, trip.StartDate, trip.EndDate, stop.Name, stop.Latitude, stop.Longitude, stop.PhotoCount, finder.Find(stop.Latitude, stop.Longitude)?.Country.Name))),
            parkLookup);

        return new StatsLibrary(ordered, countries, Continents.Totals(outlineList), outlineList.Count(outline => outline.Id is not null), parks, places);
    }

    /// <summary>The totals at the top of the Insights screen.</summary>
    public StatsTotals Totals() => new(
        Trips.Count,
        Trips.Sum(trip => trip.Days),
        Math.Round(Trips.Sum(trip => trip.DistanceKm), 1),
        Countries.Count,
        Countries.Select(country => country.Continent).Distinct().Count());

    /// <summary>The visited countries of a continent, in the library's order.</summary>
    public IReadOnlyList<CountryStats> CountriesOn(Continent continent) => [.. Countries.Where(country => country.Continent == continent)];

    /// <summary>One visited country by its key (its id, or its name when it has none), or null.</summary>
    public CountryStats? Country(string key) => Countries.FirstOrDefault(country => country.Country.Key == key);

    /// <summary>
    /// The words that find each trip in the library's search: its countries, its cities and their
    /// stop names, its parks, their resorts, and their collections. Each list is in that order,
    /// with no duplicates, ignoring case. See docs/specs/trip-library.md.
    /// </summary>
    /// <param name="collectionOfPark">The collection name of each park id, such as "Disney Parks".</param>
    public IReadOnlyDictionary<int, IReadOnlyList<string>> KeywordsByTrip(IReadOnlyDictionary<string, string>? collectionOfPark = null)
    {
        var words = Trips.ToDictionary(trip => trip.Id, _ => new List<string>());
        void Add(int tripId, string? word)
        {
            if (string.IsNullOrWhiteSpace(word) || !words.TryGetValue(tripId, out var list)) return;
            if (!list.Contains(word, StringComparer.OrdinalIgnoreCase)) list.Add(word);
        }

        foreach (var country in Countries)
        {
            foreach (var trip in country.Trips) Add(trip.TripId, country.Country.Name);
        }

        foreach (var place in Places)
        {
            foreach (var trip in place.Trips)
            {
                Add(trip.TripId, place.Name);
                foreach (var stop in place.StopNames ?? []) Add(trip.TripId, stop);
            }
        }

        foreach (var park in Parks)
        {
            foreach (var visit in park.Trips)
            {
                Add(visit.TripId, park.Place.Name);
                Add(visit.TripId, park.Place.Group);
                Add(visit.TripId, collectionOfPark?.GetValueOrDefault(park.Place.Id));
            }
        }

        return words.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value);
    }
}
