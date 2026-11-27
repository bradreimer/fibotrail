using Fibotrail.Stats;

namespace Fibotrail.Mcp.Tests;

public class CollectionToolTests
{
    [Fact]
    public void Three_Disney_trips_read_3_trips_and_14_park_days()
    {
        var result = Fixture.Run<CollectionVisits>("collection_visits", Fixture.Data(), """{"collection":"disney-parks"}""");

        Assert.Equal(3, result.Trips);
        Assert.Equal(14, result.Days);
        Assert.Equal(2, result.ParksVisited);
        Assert.Equal(3, result.ParksTotal);
        Assert.Equal(new DateOnly(2019, 3, 1), result.FirstVisit);
        Assert.Equal(new DateOnly(2023, 11, 5), result.LastVisit);
    }

    [Fact]
    public void Each_resort_and_park_has_its_own_trips_and_days()
    {
        var result = Fixture.Run<CollectionVisits>("collection_visits", Fixture.Data(), """{"collection":"disney-parks"}""");

        var orlando = Assert.Single(result.Resorts, resort => resort.Name == "Walt Disney World Resort");
        Assert.Equal((3, 12), (orlando.Trips, orlando.Days));
        var anaheim = Assert.Single(result.Resorts, resort => resort.Name == "Disneyland Resort");
        Assert.Equal((1, 2), (anaheim.Trips, anaheim.Days));

        var magicKingdom = Assert.Single(result.Parks, park => park.Name == "Magic Kingdom");
        Assert.Equal((3, 12), (magicKingdom.Trips, magicKingdom.Days));
        Assert.Equal(new DateOnly(2023, 11, 3), magicKingdom.LastVisit);
        Assert.False(magicKingdom.MarkedByYou);
        Assert.DoesNotContain(result.Parks, park => park.Name == "EPCOT");
    }

    [Fact]
    public void A_planned_park_isnt_counted()
    {
        var result = Fixture.Run<CollectionVisits>("collection_visits", Fixture.Data(), """{"collection":"disney-parks"}""");

        Assert.Equal(3, result.ParksTotal);
        Assert.DoesNotContain(result.Resorts, resort => resort.Name == "Abu Dhabi");
    }

    [Fact]
    public void A_collection_is_found_by_id_or_name_in_any_case()
    {
        var data = Fixture.Data();

        Assert.Equal("disney-parks", Fixture.Run<CollectionVisits>("collection_visits", data, """{"collection":"Disney Parks"}""").Id);
        Assert.Equal("disney-parks", Fixture.Run<CollectionVisits>("collection_visits", data, """{"collection":" DISNEY-PARKS "}""").Id);
    }

    [Fact]
    public void An_unknown_collection_lists_the_valid_choices()
    {
        var error = Assert.Throws<ToolInputException>(() => Fixture.Run<CollectionVisits>("collection_visits", Fixture.Data(), """{"collection":"water-parks"}"""));

        Assert.Contains("disney-parks", error.Message, StringComparison.Ordinal);
        Assert.Contains("universal-parks", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void With_no_collections_installed_the_error_says_so()
    {
        var data = new McpData(Fixture.Library(), []);

        var error = Assert.Throws<ToolInputException>(() => Fixture.Run<CollectionVisits>("collection_visits", data, """{"collection":"disney-parks"}"""));

        Assert.Contains("No collections are installed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_park_marked_by_hand_adds_a_trip_and_no_days()
    {
        var trips = Fixture.Trips();
        trips.Add(Fixture.Trip(7, "Old trip", new DateOnly(2005, 8, 1), 3, [], marked: ["epcot"]));

        var result = Fixture.Run<CollectionVisits>("collection_visits", Fixture.Data(trips), """{"collection":"disney-parks"}""");

        Assert.Equal(4, result.Trips);
        Assert.Equal(14, result.Days);
        var epcot = Assert.Single(result.Parks, park => park.Name == "EPCOT");
        Assert.Equal((1, 0), (epcot.Trips, epcot.Days));
        Assert.True(epcot.MarkedByYou);
        Assert.Equal(new DateOnly(2005, 8, 1), result.FirstVisit);
    }

    [Fact]
    public void The_list_shows_each_collection()
    {
        var result = Fixture.Run<CollectionList>("list_collections", Fixture.Data());

        var disney = Assert.Single(result.Collections, collection => collection.Id == "disney-parks");
        Assert.Equal((3, 14, 2, 3), (disney.Trips, disney.Days, disney.ParksVisited, disney.ParksTotal));
        var universal = Assert.Single(result.Collections, collection => collection.Id == "universal-parks");
        Assert.Equal((1, 3), (universal.Trips, universal.Days));
    }

    [Fact]
    public void An_empty_library_reads_zero()
    {
        var result = Fixture.Run<CollectionVisits>("collection_visits", Fixture.Data([]), """{"collection":"disney-parks"}""");

        Assert.Equal((0, 0, 0), (result.Trips, result.Days, result.ParksVisited));
        Assert.Null(result.FirstVisit);
        Assert.Empty(result.Parks);
    }
}

public class CountryToolTests
{
    [Fact]
    public void The_world_list_has_every_visited_country_and_the_total()
    {
        var result = Fixture.Run<CountryList>("list_countries", Fixture.Data());

        Assert.Equal("World", result.Scope);
        Assert.Equal(3, result.Visited);
        Assert.Equal(4, result.Total);
        Assert.Equal(["United States of America", "Sweden", "Japan"], result.Countries.Select(country => country.Name).ToArray());
    }

    [Fact]
    public void A_continent_lists_only_its_own_countries()
    {
        var result = Fixture.Run<CountryList>("list_countries", Fixture.Data(), """{"continent":"europe"}""");

        Assert.Equal("Europe", result.Scope);
        var sweden = Assert.Single(result.Countries);
        Assert.Equal(("Sweden", 1, 7), (sweden.Name, sweden.Trips, sweden.Days));
        Assert.Equal(1, result.Visited);
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public void An_unknown_continent_lists_the_valid_choices()
    {
        var error = Assert.Throws<ToolInputException>(() => Fixture.Run<CountryList>("list_countries", Fixture.Data(), """{"continent":"Atlantis"}"""));

        Assert.Contains("Europe", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_country_is_found_by_name_or_id_in_any_case()
    {
        var data = Fixture.Data();

        var byName = Fixture.Run<CountrySummary>("country_summary", data, """{"country":"sweden"}""");
        var byCapitalName = Fixture.Run<CountrySummary>("country_summary", data, """{"country":"Sweden"}""");
        var byId = Fixture.Run<CountrySummary>("country_summary", data, """{"country":"752"}""");

        Assert.Equal(byName, byCapitalName);
        Assert.Equal(byName, byId);
        Assert.Equal(("Europe", 1, 7), (byName.Continent, byName.Trips, byName.Days));
        Assert.Equal(new DateOnly(2022, 7, 1), byName.FirstVisit);
        Assert.Equal(new DateOnly(2022, 7, 7), byName.LastVisit);
    }

    [Fact]
    public void A_country_with_no_trip_is_an_error_that_points_to_the_list()
    {
        var error = Assert.Throws<ToolInputException>(() => Fixture.Run<CountrySummary>("country_summary", Fixture.Data(), """{"country":"Denmark"}"""));

        Assert.Contains("list_countries", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_library_lists_no_countries()
    {
        var result = Fixture.Run<CountryList>("list_countries", Fixture.Data([]));

        Assert.Equal((0, 4), (result.Visited, result.Total));
        Assert.Empty(result.Countries);
    }
}

public class TripToolTests
{
    [Fact]
    public void Trips_are_listed_newest_first_with_an_id_and_no_name()
    {
        var result = Fixture.Run<TripList>("list_trips", Fixture.Data());

        Assert.Equal(6, result.Total);
        Assert.False(result.Truncated);
        Assert.Null(result.Note);
        Assert.Equal([5, 3, 4, 2, 6, 1], result.Trips.Select(trip => trip.Id).ToArray());
        Assert.DoesNotContain(typeof(TripEntry).GetProperties(), property => property.Name.Contains("Name", StringComparison.Ordinal));
    }

    [Fact]
    public void A_date_range_matches_a_trip_with_any_day_inside_it_and_keeps_the_whole_trip()
    {
        var result = Fixture.Run<TripList>("list_trips", Fixture.Data(), """{"from":"2023-11-04","to":"2023-12-31"}""");

        var trip = Assert.Single(result.Trips);
        Assert.Equal((3, 5), (trip.Id, trip.Days));
        Assert.Equal(new DateOnly(2023, 11, 1), trip.StartDate);
    }

    [Fact]
    public void A_country_filter_keeps_the_trips_that_have_a_stop_there()
    {
        var result = Fixture.Run<TripList>("list_trips", Fixture.Data(), """{"country":"Sweden"}""");

        Assert.Equal([4], result.Trips.Select(trip => trip.Id).ToArray());
        Assert.Equal(["Sweden"], result.Trips[0].Countries);
    }

    [Fact]
    public void A_collection_filter_keeps_the_trips_that_visit_a_park()
    {
        var result = Fixture.Run<TripList>("list_trips", Fixture.Data(), """{"collection":"disney-parks"}""");

        Assert.Equal([3, 2, 1], result.Trips.Select(trip => trip.Id).ToArray());
        Assert.Equal(["Disneyland Park", "Magic Kingdom"], result.Trips[0].Parks);
        Assert.Equal(["Disney parks"], result.Trips[0].Collections);
    }

    [Fact]
    public void A_limit_cuts_the_list_and_says_so()
    {
        var result = Fixture.Run<TripList>("list_trips", Fixture.Data(), """{"limit":2}""");

        Assert.Equal((6, 2, true), (result.Total, result.Returned, result.Truncated));
        Assert.Contains("cut short", result.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void A_limit_above_the_most_is_cut_to_200()
    {
        var trips = Enumerable.Range(1, 205).Select(id => Fixture.Trip(id, $"Trip {id}", new DateOnly(2000, 1, 1).AddDays(id * 3), 2, (59, 15), 0)).ToList();

        var result = Fixture.Run<TripList>("list_trips", Fixture.Data(trips), """{"limit":500}""");

        Assert.Equal((205, 200, true), (result.Total, result.Returned, result.Truncated));
    }

    [Fact]
    public void A_bad_date_is_an_error()
    {
        Assert.Throws<ToolInputException>(() => Fixture.Run<TripList>("list_trips", Fixture.Data(), """{"from":"last spring"}"""));
    }

    [Fact]
    public void A_trip_names_its_main_city_and_null_when_no_photo_has_one()
    {
        var result = Fixture.Run<TripList>("list_trips", Fixture.Data());

        var cities = result.Trips.ToDictionary(trip => trip.Id, trip => trip.MainCity);
        Assert.Equal("Tokyo", cities[5]);
        Assert.Equal("Orlando", cities[3]);
        Assert.Equal("Orlando", cities[1]);
        Assert.Null(cities[6]);
    }

    [Fact]
    public void A_city_filter_matches_the_main_city_ignoring_case()
    {
        var result = Fixture.Run<TripList>("list_trips", Fixture.Data(), """{"city":"orlando"}""");

        Assert.Equal([3, 2, 1], result.Trips.Select(trip => trip.Id).ToArray());
    }

    [Fact]
    public void A_city_filter_skips_a_city_the_trip_only_passed_through()
    {
        // Trip 3 stops in Kissimmee, and its main city is Orlando.
        var result = Fixture.Run<TripList>("list_trips", Fixture.Data(), """{"city":"Kissimmee"}""");

        Assert.Empty(result.Trips);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public void One_trip_is_read_by_its_id()
    {
        var trip = Fixture.Run<TripEntry>("get_trip", Fixture.Data(), """{"id":3}""");

        Assert.Equal((3, 5), (trip.Id, trip.Days));
        Assert.Equal("Orlando", trip.MainCity);
        Assert.Equal(["United States of America"], trip.Countries);
        Assert.Equal(["Disneyland Park", "Magic Kingdom"], trip.Parks);
    }

    [Fact]
    public void An_unknown_trip_is_an_error_that_points_to_the_list()
    {
        var error = Assert.Throws<ToolInputException>(() => Fixture.Run<TripEntry>("get_trip", Fixture.Data(), """{"id":99}"""));

        Assert.Contains("list_trips", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_library_lists_no_trips()
    {
        var result = Fixture.Run<TripList>("list_trips", Fixture.Data([]));

        Assert.Equal((0, 0, false), (result.Total, result.Returned, result.Truncated));
    }
}

public class DaysAwayToolTests
{
    [Fact]
    public void Days_by_year_add_up_to_the_days_away_of_the_stats_screen()
    {
        var data = Fixture.Data();

        var result = Fixture.Run<DaysAway>("days_away", data, """{"by":"year"}""");

        Assert.Equal(data.Library.Totals().DaysAway, result.TotalDays);
        Assert.Equal(["2019", "2020", "2021", "2022", "2023", "2024"], result.Periods.Select(period => period.Period).ToArray());
        Assert.Equal([4, 3, 5, 7, 5, 6], result.Periods.Select(period => period.Days).ToArray());
    }

    [Fact]
    public void A_trip_across_a_month_end_is_split_between_the_months()
    {
        var trips = new List<StatsTrip> { Fixture.Trip(1, "Ski", new DateOnly(2024, 1, 30), 5, (59, 15), 0) };

        var result = Fixture.Run<DaysAway>("days_away", Fixture.Data(trips), """{"by":"month"}""");

        Assert.Equal([("2024-01", 2), ("2024-02", 3)], result.Periods.Select(period => (period.Period, period.Days)).ToArray());
    }

    [Fact]
    public void An_unknown_grouping_is_an_error()
    {
        Assert.Throws<ToolInputException>(() => Fixture.Run<DaysAway>("days_away", Fixture.Data(), """{"by":"week"}"""));
    }

    [Fact]
    public void An_empty_library_has_no_periods()
    {
        var result = Fixture.Run<DaysAway>("days_away", Fixture.Data([]), """{"by":"year"}""");

        Assert.Equal(0, result.TotalDays);
        Assert.Empty(result.Periods);
    }
}
