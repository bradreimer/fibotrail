using Fibotrail.Games.Pin;

namespace Fibotrail.Games.Tests;

/// <summary>The Pin game. See docs/specs/pin-game.md.</summary>
public class PinPuzzleTests
{
    private static readonly DateOnly Day = new(2026, 11, 20);

    private static readonly GeoPoint Paris = new(48.8566, 2.3522);

    private static GameTrip Trip(int id, int photos = 3, bool located = true) => new(
        id,
        $"Trip {id}",
        new DateOnly(2020 + id, 6, 1),
        new DateOnly(2020 + id, 6, 5),
        $"Place {id}",
        [.. Enumerable.Range(0, photos).Select(index => new GamePhoto(
            $"t{id}-p{index}",
            new DateTime(2020 + id, 6, 1 + index, 10, 0, 0),
            located ? 40 + id : null,
            located ? -100 + index : null))]);

    private static GameLibrary Library(int trips = 4) => new([.. Enumerable.Range(1, trips).Select(id => Trip(id))]);

    /// <summary>A puzzle whose answer is Paris.</summary>
    private static PinPuzzle AtParis() => new(Day, 2, new PinPhoto("p", Paris, 1, "Paris trip", new DateOnly(2027, 9, 1), new DateOnly(2027, 9, 5), "Paris"));

    /// <summary>The point <paramref name="km"/> due north of Paris.</summary>
    private static GeoPoint NorthOfParis(double km) => new(Paris.Latitude + (km / PinPuzzle.EarthRadiusKm * 180 / Math.PI), Paris.Longitude);

    [Fact]
    public void A_library_with_no_located_photo_has_no_puzzle()
    {
        var library = new GameLibrary([Trip(1, located: false), Trip(2, photos: 0)]);

        Assert.Null(PinPuzzle.Create(library, Day));
        Assert.Equal(1, PinPuzzle.TripsNeeded(library));
        Assert.Equal(0, PinPuzzle.TripsNeeded(Library()));
    }

    [Fact]
    public void Only_a_photo_with_a_location_is_picked()
    {
        var library = new GameLibrary([Trip(1, located: false), Trip(2, photos: 1)]);

        for (var day = 0; day < 30; day++)
        {
            var puzzle = PinPuzzle.Create(library, Day.AddDays(day))!;
            Assert.Equal("t2-p0", puzzle.Photo.PhotoId);
            Assert.Equal(new GeoPoint(42, -100), puzzle.Photo.Location);
        }
    }

    [Fact]
    public void The_same_date_and_library_give_the_same_puzzle()
    {
        var first = PinPuzzle.Create(Library(), Day)!;
        var second = PinPuzzle.Create(new GameLibrary([.. Library().Trips.Reverse()]), Day)!;

        Assert.Equal(first.Photo, second.Photo);
        Assert.Equal(DailySeed.PuzzleNumber(Day), first.Number);
    }

    [Fact]
    public void The_distance_is_the_great_circle_distance()
    {
        var london = new GeoPoint(51.5074, -0.1278);

        Assert.Equal(344, PinPuzzle.DistanceKm(Paris, london), 0);
        Assert.Equal(0, PinPuzzle.DistanceKm(Paris, Paris), 6);
        Assert.Equal(Math.PI * PinPuzzle.EarthRadiusKm, PinPuzzle.DistanceKm(new GeoPoint(0, 0), new GeoPoint(0, 180)), 3);
    }

    [Theory]
    [InlineData(50, PinBand.Green)]
    [InlineData(51, PinBand.Yellow)]
    [InlineData(500, PinBand.Yellow)]
    [InlineData(501, PinBand.Orange)]
    [InlineData(2000, PinBand.Orange)]
    [InlineData(2001, PinBand.Red)]
    public void The_bands_have_fixed_edges(double km, PinBand band) => Assert.Equal(band, PinPuzzle.BandOf(km));

    [Theory]
    [InlineData(10, PinBand.Green)]
    [InlineData(51, PinBand.Yellow)]
    [InlineData(501, PinBand.Orange)]
    [InlineData(2001, PinBand.Red)]
    public void A_pin_is_graded_by_its_distance(double km, PinBand band)
    {
        var mark = AtParis().Grade(NorthOfParis(km))!;

        Assert.Equal(band, mark.Band);
        Assert.Equal(band == PinBand.Green, mark.Solved);
        Assert.Equal(band == PinBand.Green ? null : Compass.S, mark.Direction);
    }

    [Theory]
    [InlineData(51, 60)]
    [InlineData(310, 310)]
    [InlineData(999, 1000)]
    [InlineData(1001, 1100)]
    [InlineData(2350, 2400)]
    public void The_shown_distance_rounds_up(double km, int shown) => Assert.Equal(shown, PinPuzzle.ShownKm(km));

    [Theory]
    [InlineData(0, Compass.N)]
    [InlineData(22, Compass.N)]
    [InlineData(23, Compass.NE)]
    [InlineData(90, Compass.E)]
    [InlineData(200, Compass.S)]
    [InlineData(250, Compass.W)]
    [InlineData(337.5, Compass.N)]
    [InlineData(315, Compass.NW)]
    [InlineData(359, Compass.N)]
    public void A_bearing_rounds_to_the_nearest_compass_point(double degrees, Compass compass) =>
        Assert.Equal(compass, PinPuzzle.CompassOf(degrees));

    [Fact]
    public void The_arrow_takes_the_short_way_across_the_date_line()
    {
        // From Fiji, Samoa is east across the date line, not west across the Pacific.
        var puzzle = AtParis() with { Photo = AtParis().Photo with { Location = new GeoPoint(-13.8, -171.8) } };

        var mark = puzzle.Grade(new GeoPoint(-17.7, 178.1))!;

        Assert.Equal(Compass.E, mark.Direction);
        Assert.True(mark.DistanceKm < 1300);
    }

    [Fact]
    public void Near_a_pole_the_arrow_still_points_the_short_way()
    {
        var puzzle = AtParis() with { Photo = AtParis().Photo with { Location = new GeoPoint(89, 0) } };

        Assert.Equal(Compass.N, puzzle.Grade(new GeoPoint(89, 180))!.Direction);
        Assert.Equal(Compass.S, puzzle.Grade(new GeoPoint(90, 0))!.Direction);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    [InlineData(double.NaN, 0)]
    public void A_pin_off_the_map_is_not_graded(double latitude, double longitude) =>
        Assert.Null(AtParis().Grade(new GeoPoint(latitude, longitude)));

    [Fact]
    public void A_game_is_scored_pin_by_pin()
    {
        var outcome = AtParis().Score([NorthOfParis(3000), NorthOfParis(300), NorthOfParis(5)])!;

        Assert.True(outcome.Solved);
        Assert.Equal([PinBand.Red, PinBand.Yellow, PinBand.Green], outcome.Marks.Select(mark => mark.Band));
    }

    [Fact]
    public void Too_many_pins_none_or_a_pin_after_a_solved_one_is_refused()
    {
        var puzzle = AtParis();
        var far = NorthOfParis(3000);

        Assert.Null(puzzle.Score([]));
        Assert.Null(puzzle.Score([far, far, far, far, far]));
        Assert.Null(puzzle.Score([Paris, far]));
        Assert.Null(puzzle.Score([far, new GeoPoint(100, 0)]));
        Assert.False(puzzle.Score([far, far, far, far])!.Solved);
    }

    [Fact]
    public void Pins_round_trip_through_the_stored_text()
    {
        GeoPoint[] pins = [new(48.856_61, 2.352_22), new(-33.8688, 151.2093), new(0, -180)];

        var text = PinPuzzle.Encode(pins);

        Assert.Equal("48.8566 2.3522;-33.8688 151.2093;0 -180", text);
        Assert.Equal(pins.Select(pin => pin.Rounded()), PinPuzzle.Decode(text));
    }

    [Fact]
    public void The_share_text_has_squares_and_arrows_only()
    {
        var puzzle = AtParis();
        var solved = puzzle.Score([NorthOfParis(3000), NorthOfParis(300), NorthOfParis(5)])!;
        var lost = puzzle.Score([NorthOfParis(3000), NorthOfParis(3000), NorthOfParis(800), NorthOfParis(100)])!;

        Assert.Equal("Fibotrail Pin #2 3/4\n🟥⬇️\n🟨⬇️\n🟩", PinPuzzle.ShareText(puzzle.Number, solved));
        Assert.Equal("Fibotrail Pin #2 X/4\n🟥⬇️\n🟥⬇️\n🟧⬇️\n🟨⬇️", PinPuzzle.ShareText(puzzle.Number, lost));
        Assert.DoesNotContain("km", PinPuzzle.ShareText(puzzle.Number, lost), StringComparison.Ordinal);
        Assert.DoesNotContain("Paris", PinPuzzle.ShareText(puzzle.Number, lost), StringComparison.Ordinal);
    }
}
