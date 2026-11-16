namespace Fibotrail.Places;

/// <summary>
/// Finds the region of a position inside a detail country. One <see cref="CountryFinder"/> for each
/// detail country does the matching, so a region follows the same rule as a country: the outline
/// that contains the position, or else the nearest one within a distance. A pure function of the
/// outlines it's given. See docs/specs/life-map.md, "Detail regions", and ADR-012.
/// </summary>
public sealed class RegionFinder
{
    private readonly Dictionary<string, CountryFinder> _byCountry;

    public RegionFinder(IEnumerable<DetailCountry> countries, IEnumerable<RegionOutline> regions)
    {
        ArgumentNullException.ThrowIfNull(countries);
        ArgumentNullException.ThrowIfNull(regions);

        DetailCountries = [.. countries];
        var grouped = regions.GroupBy(region => region.CountryId).ToDictionary(group => group.Key, group => group.Select(region => region.Outline).ToList());
        _byCountry = DetailCountries.ToDictionary(
            country => country.Id,
            country => new CountryFinder(grouped.GetValueOrDefault(country.Id) ?? []),
            StringComparer.Ordinal);
    }

    /// <summary>The countries that also track their regions.</summary>
    public IReadOnlyList<DetailCountry> DetailCountries { get; }

    /// <summary>How many regions a detail country has, or 0 when it isn't one.</summary>
    public int Total(string countryId) => _byCountry.TryGetValue(countryId, out var finder) ? finder.Count : 0;

    /// <summary>The regions of a detail country, by name. Empty for any other country.</summary>
    public IEnumerable<Country> Regions(string countryId) =>
        _byCountry.TryGetValue(countryId, out var finder) ? finder.Countries : [];

    /// <summary>
    /// The region of the position inside a detail country. A position that no outline contains joins
    /// the nearest region within <paramref name="nearestKm"/>. Null when the country isn't a detail
    /// country, or when no region is near enough.
    /// </summary>
    public CountryMatch? Find(string? countryId, double latitude, double longitude, double nearestKm = CountryFinder.DefaultNearestKm) =>
        countryId is not null && _byCountry.TryGetValue(countryId, out var finder)
            ? finder.Find(latitude, longitude, nearestKm)
            : null;
}
