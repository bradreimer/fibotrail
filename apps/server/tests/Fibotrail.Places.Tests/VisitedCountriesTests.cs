namespace Fibotrail.Places.Tests;

public class VisitedCountriesTests
{
    private static IReadOnlyList<(double, double)> Square(double west, double south, double east, double north) =>
        [(west, south), (east, south), (east, north), (west, north), (west, south)];

    /// <summary>Alpha is the square at 0 to 10, Beta is 20 to 30, and Gamma, which has no code, is 40 to 50.</summary>
    private static CountryFinder Finder() => new(
    [
        new CountryOutline("001", "Alpha", [[Square(0, 0, 10, 10)]]),
        new CountryOutline("002", "Beta", [[Square(20, 0, 30, 10)]]),
        new CountryOutline(null, "Gamma", [[Square(40, 0, 50, 10)]]),
    ]);

    private static TripStops Trip(int id, string name, int month, params (double Latitude, double Longitude)[] stops) =>
        new(id, name, new DateOnly(2027, month, 1), new DateOnly(2027, month, 9), stops);

    [Fact]
    public void No_trips_visit_no_country()
    {
        Assert.Empty(VisitedCountries.Find([], Finder()));
    }

    [Fact]
    public void A_stop_in_a_country_makes_it_visited_with_its_trip_and_first_visit()
    {
        var visited = Assert.Single(VisitedCountries.Find([Trip(1, "Alpha trip", 3, (5, 5))], Finder()));

        Assert.Equal(("Alpha", new DateOnly(2027, 3, 1)), (visited.Country.Name, visited.FirstVisit));
        Assert.Equal([new VisitedTrip(1, "Alpha trip", new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 9))], visited.Trips);
    }

    [Fact]
    public void A_trip_with_stops_in_two_countries_counts_for_both_and_once_for_each()
    {
        var trip = Trip(1, "Both", 3, (5, 5), (6, 6), (5, 25));

        var visited = VisitedCountries.Find([trip], Finder());

        Assert.Equal(["Alpha", "Beta"], visited.Select(country => country.Country.Name));
        Assert.All(visited, country => Assert.Single(country.Trips));
    }

    [Fact]
    public void The_first_visit_is_the_earliest_trip_and_the_trips_are_listed_earliest_first()
    {
        var late = Trip(2, "Later", 9, (5, 5));
        var early = Trip(1, "Earlier", 3, (5, 5));

        var visited = Assert.Single(VisitedCountries.Find([late, early], Finder()));

        Assert.Equal(new DateOnly(2027, 3, 1), visited.FirstVisit);
        Assert.Equal(["Earlier", "Later"], visited.Trips.Select(trip => trip.Name));
    }

    [Fact]
    public void Trips_that_start_on_the_same_day_are_ordered_by_their_id()
    {
        var visited = Assert.Single(VisitedCountries.Find([Trip(8, "Second", 3, (5, 5)), Trip(4, "First", 3, (5, 5))], Finder()));

        Assert.Equal([4, 8], visited.Trips.Select(trip => trip.TripId));
    }

    [Fact]
    public void Countries_are_listed_by_first_visit_then_by_name()
    {
        var visited = VisitedCountries.Find(
        [
            Trip(1, "Gamma first", 2, (5, 45)),
            Trip(2, "Alpha and Beta", 6, (5, 5), (5, 25)),
        ], Finder());

        Assert.Equal(["Gamma", "Alpha", "Beta"], visited.Select(country => country.Country.Name));
    }

    [Fact]
    public void A_stop_off_the_coast_counts_for_the_nearest_country_and_one_far_away_counts_for_none()
    {
        var near = Trip(1, "Coast", 3, (5, 10.1));
        var far = Trip(2, "Sea", 4, (5, 15));

        var visited = VisitedCountries.Find([near, far], Finder());

        Assert.Equal(["Alpha"], visited.Select(country => country.Country.Name));
        Assert.Equal([1], visited.Single().Trips.Select(trip => trip.TripId));
    }

    [Fact]
    public void A_trip_with_no_stop_visits_nothing()
    {
        Assert.Empty(VisitedCountries.Find([Trip(1, "No stops", 3)], Finder()));
    }

    [Fact]
    public void A_country_with_no_code_is_still_one_country()
    {
        var visited = VisitedCountries.Find([Trip(1, "One", 3, (5, 45)), Trip(2, "Two", 4, (6, 46))], Finder());

        var gamma = Assert.Single(visited);
        Assert.Equal(("Gamma", 2), (gamma.Country.Name, gamma.Trips.Count));
    }

    [Fact]
    public void Real_outlines_put_Stockholm_and_Paris_in_two_countries()
    {
        var trip = Trip(1, "Europe", 5, (59.33, 18.07), (48.86, 2.35));

        var visited = VisitedCountries.Find([trip], CountryOutlines.Load());

        Assert.Equal(["France", "Sweden"], visited.Select(country => country.Country.Name).Order(StringComparer.Ordinal));
    }
}
