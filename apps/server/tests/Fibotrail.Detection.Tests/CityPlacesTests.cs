using Fibotrail.Detection.Cities;
using Fibotrail.Detection.Parks;

namespace Fibotrail.Detection.Tests;

/// <summary>Merging stops into cities, and keeping parks apart. See ADR-015.</summary>
public class CityPlacesTests
{
    private static readonly DateOnly Start = new(2024, 5, 1);

    private static PlaceStop Stop(int trip, string? name, double latitude, double longitude, int photos = 10, int monthOffset = 0) =>
        new(trip, $"Trip {trip}", Start.AddMonths(monthOffset), Start.AddMonths(monthOffset).AddDays(4), name, latitude, longitude, photos);

    private static ParkPlace Park(string id, string name, double south, double west) =>
        new(id, name, "Resort", 3, 50, [[[new(south, west), new(south, west + 0.01), new(south + 0.01, west + 0.01), new(south + 0.01, west), new(south, west)]]]);

    [Fact]
    public void Neighborhoods_of_one_city_merge_into_one_place_named_by_the_stop_with_the_most_photos()
    {
        // Shinjuku, Shibuya, and Ginza are all within 25 km of each other.
        var places = CityPlaces.Find(
        [
            Stop(1, "Shinjuku", 35.69, 139.70, 30),
            Stop(1, "Shibuya", 35.66, 139.70, 80),
            Stop(1, "Ginza", 35.67, 139.76, 12),
        ]);

        var tokyo = Assert.Single(places);
        Assert.Equal("Shibuya", tokyo.Name);
        Assert.False(tokyo.IsPark);
    }

    [Fact]
    public void Cities_farther_apart_than_the_radius_stay_separate()
    {
        var places = CityPlaces.Find([Stop(1, "Seattle", 47.61, -122.33), Stop(1, "Portland", 45.52, -122.68)]);

        Assert.Equal(["Portland", "Seattle"], places.Select(place => place.Name).Order());
    }

    [Fact]
    public void Two_trips_to_one_city_make_one_place_with_both_trips_and_the_earliest_first_visit()
    {
        var places = CityPlaces.Find([Stop(2, "Rome", 41.9, 12.5, monthOffset: 6), Stop(1, "Rome", 41.89, 12.49, monthOffset: 0)]);

        var rome = Assert.Single(places);
        Assert.Equal([1, 2], rome.Trips.Select(trip => trip.TripId));
        Assert.Equal(Start, rome.FirstVisit);
    }

    [Fact]
    public void Two_stops_of_one_trip_in_one_city_count_the_trip_once()
    {
        var places = CityPlaces.Find([Stop(1, "Rome", 41.9, 12.5), Stop(1, "Rome", 41.91, 12.51)]);

        Assert.Single(Assert.Single(places).Trips);
    }

    [Fact]
    public void A_tie_in_photos_goes_to_the_earliest_stop()
    {
        var places = CityPlaces.Find([Stop(1, "First", 41.9, 12.5, 10), Stop(1, "Second", 41.91, 12.51, 10)]);

        Assert.Equal("First", Assert.Single(places).Name);
    }

    [Fact]
    public void A_stop_with_no_name_takes_a_name_from_its_group_or_is_unnamed()
    {
        Assert.Equal("Rome", Assert.Single(CityPlaces.Find([Stop(1, null, 41.9, 12.5, 90), Stop(1, "Rome", 41.91, 12.51, 5)])).Name);
        Assert.Equal("Unnamed place", Assert.Single(CityPlaces.Find([Stop(1, null, 0, 0)])).Name);
    }

    [Fact]
    public void A_stop_inside_a_park_is_a_park_place_and_is_not_merged_into_the_city_next_to_it()
    {
        var magicKingdom = Park("mk", "Magic Kingdom", 28.41, -81.59);
        var lookup = new ParkLookup([magicKingdom]);

        var places = CityPlaces.Find(
        [
            Stop(1, "Orlando", 28.54, -81.38, 20),
            Stop(1, "Lake Buena Vista", 28.415, -81.585, 200),
        ], lookup);

        Assert.Equal(["Magic Kingdom", "Orlando"], places.Select(place => place.Name).Order());
        Assert.True(places.Single(place => place.Name == "Magic Kingdom").IsPark);
        Assert.Equal("mk", places.Single(place => place.IsPark).ParkId);
        Assert.False(places.Single(place => place.Name == "Orlando").IsPark);
    }

    [Fact]
    public void A_trip_made_only_of_park_stops_adds_no_city()
    {
        var lookup = new ParkLookup([Park("mk", "Magic Kingdom", 28.41, -81.59)]);

        var places = CityPlaces.Find([Stop(1, "Lake Buena Vista", 28.415, -81.585)], lookup);

        Assert.All(places, place => Assert.True(place.IsPark));
        Assert.Equal(0, places.Count(place => !place.IsPark));
    }

    [Fact]
    public void No_stops_give_no_places() => Assert.Empty(CityPlaces.Find([]));
}
