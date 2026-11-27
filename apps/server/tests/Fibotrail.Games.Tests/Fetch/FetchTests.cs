using Fibotrail.Games.Fetch;

namespace Fibotrail.Games.Tests.Fetch;

/// <summary>The Fetch game. See docs/specs/fetch-game.md.</summary>
public class FetchTests
{
    private static readonly DateOnly Day = new(2026, 11, 20);

    private static GameTrip Trip(int id, int photos = 10, bool located = true, Func<int, double>? latitude = null) => new(
        id,
        $"Trip {id}",
        new DateOnly(2020 + id, 6, 1),
        new DateOnly(2020 + id, 6, 10),
        $"Place {id}",
        [.. Enumerable.Range(0, photos).Select(index => new GamePhoto(
            $"t{id}-p{index}",
            new DateTime(2020 + id, 6, 1 + index, 10, 0, 0),
            located ? (latitude ?? (i => 40 + i))(index) : null,
            located ? -100 : null))]);

    private static GameLibrary Library(int trips = 4) => new([.. Enumerable.Range(1, trips).Select(id => Trip(id))]);

    /// <summary>
    /// A course used by both test suites, so the C# and TypeScript simulations are held to the same
    /// answers. The same course is in apps/web/src/games/fetch/flight.test.ts.
    /// </summary>
    public static FetchCourse SharedCourse()
    {
        int[] centers = [300, 250, 350, 400, 300, 200, 280, 330];
        const int unit = FetchPhysics.Unit;
        var gates = centers.Select((center, index) => new FetchGate((440 + (index * 240)) * unit, (center - 125) * unit, (center + 125) * unit, index + 1)).ToList();
        int[] heights = [260, 230, 300, 380, 320, 230, 240, 300];
        var treats = heights.Select((y, index) => new FetchTreat((440 + (index * 240) + 155) * unit, y * unit, $"p{index}")).ToList();
        return new FetchCourse(FetchPhysics.Default, gates, treats, (440 + (8 * 240)) * unit);
    }

    /// <summary>
    /// A simple pilot: it flaps when it's falling below the middle of the next gap. It shows that a
    /// course can be flown, and it makes the taps of the shared cases.
    /// </summary>
    public static List<int> Autopilot(FetchCourse course, int aim = 0)
    {
        var physics = course.Physics;
        var flight = new FetchFlight(course);
        var taps = new List<int>();
        flight.Step(true);
        while (flight.End is null)
        {
            var nose = physics.DogX + flight.Scroll + physics.Body.Left;
            var next = course.Gates.FirstOrDefault(gate => gate.X + physics.GateWidth >= nose) ?? course.Gates[^1];
            var target = ((next.GapTop + next.GapBottom) / 2) + (aim * FetchPhysics.Unit);
            var middle = flight.Y + ((physics.Body.Top + physics.Body.Bottom) / 2);
            var flap = middle > target && flight.Velocity >= 0;
            if (flap)
            {
                taps.Add(flight.Tick + 1);
            }

            flight.Step(flap);
        }

        return taps;
    }

    [Fact]
    public void A_library_with_no_located_photo_has_no_course()
    {
        var library = new GameLibrary([Trip(1, located: false), Trip(2, photos: 0)]);

        Assert.Null(FetchPuzzle.Create(library, Day));
        Assert.Equal(1, FetchPuzzle.TripsNeeded(library));
        Assert.Equal(0, FetchPuzzle.TripsNeeded(Library()));
    }

    [Fact]
    public void The_same_date_and_library_give_the_same_course()
    {
        var first = FetchPuzzle.Create(Library(), Day)!;
        var again = FetchPuzzle.Create(new GameLibrary([.. Library().Trips.Reverse()]), Day)!;

        Assert.Equal(first.Trip, again.Trip);
        Assert.Equal(first.Course.Gates, again.Course.Gates);
        Assert.Equal(first.Course.Treats, again.Course.Treats);
        Assert.Equal(DailySeed.PuzzleNumber(Day), first.Number);
    }

    [Fact]
    public void A_course_has_8_gates_and_8_treats_from_located_photos_in_time_order()
    {
        var trip = Trip(1, photos: 15);
        var puzzle = FetchPuzzle.Create(new GameLibrary([trip]), Day)!;

        Assert.Equal(8, puzzle.Course.Gates.Count);
        Assert.Equal(["t1-p0", "t1-p2", "t1-p4", "t1-p6", "t1-p8", "t1-p10", "t1-p12", "t1-p14"], puzzle.Course.Treats.Select(treat => treat.PhotoId));
        Assert.Equal([1, 3, 5, 7, 9, 11, 13, 15], puzzle.Course.Gates.Select(gate => gate.Day));
        Assert.All(puzzle.Course.Gates, gate => Assert.Equal(25000, gate.GapBottom - gate.GapTop));
        Assert.Equal(new FetchTrip(1, "Trip 1", trip.StartDate, trip.EndDate, "Place 1"), puzzle.Trip);
    }

    [Fact]
    public void A_trip_with_one_located_photo_uses_it_for_every_gate()
    {
        var puzzle = FetchPuzzle.Create(new GameLibrary([Trip(1, photos: 1)]), Day)!;

        Assert.All(puzzle.Course.Treats, treat => Assert.Equal("t1-p0", treat.PhotoId));
        Assert.All(puzzle.Course.Gates, gate => Assert.Equal(31000 - 12500, gate.GapTop));
    }

    [Fact]
    public void North_is_up_and_the_band_is_filled()
    {
        var centers = FetchPuzzle.Centers([40, 41, 42]);

        Assert.Equal([435, 310, 185], centers);
    }

    [Fact]
    public void A_small_span_stays_nearly_flat()
    {
        // 0.05° is about 5 km: a city trip. It's scaled as if it spanned 0.5°.
        var centers = FetchPuzzle.Centers([48.85, 48.9]);

        Assert.Equal([322, 298], centers);
    }

    [Fact]
    public void No_gap_moves_more_than_150_px_from_the_one_before()
    {
        var centers = FetchPuzzle.Centers([0, 10, 0, 10]);

        Assert.Equal([435, 285, 435, 285], centers);
    }

    [Fact]
    public void Treats_sit_between_gates_70_px_off_the_middle()
    {
        var course = FetchPuzzle.Create(Library(), Day)!.Course;

        for (var index = 0; index < 8; index++)
        {
            var gate = course.Gates[index];
            var treat = course.Treats[index];
            Assert.Equal(gate.X + 15500, treat.X);
            var center = (gate.GapTop + gate.GapBottom) / 2;
            var nextCenter = index < 7 ? (course.Gates[index + 1].GapTop + course.Gates[index + 1].GapBottom) / 2 : center;
            Assert.Equal(7000, Math.Abs(treat.Y - ((center + nextCenter) / 2 / 100 * 100)));
        }

        Assert.Equal((440 + (8 * 240)) * 100, course.FinishX);
    }

    [Fact]
    public void With_no_taps_the_schnauzer_falls_to_the_ground()
    {
        var grade = FetchFlight.Fly(SharedCourse(), []);

        Assert.Equal(FetchEnd.Ground, grade.End);
        Assert.Equal(0, grade.GatesPassed);
        Assert.Empty(grade.Caught);
    }

    [Fact]
    public void Flapping_every_tick_hits_the_ceiling()
    {
        var grade = FetchFlight.Fly(SharedCourse(), Enumerable.Range(2, 100));

        Assert.Equal(FetchEnd.Ceiling, grade.End);
    }

    /// <summary>
    /// Taps flown on the shared course, with the answers. The same cases are in
    /// apps/web/src/games/fetch/flight.test.ts. A change to the physics changes both.
    /// </summary>
    public static readonly TheoryData<int[], FetchEnd, int, int, int[]> SharedCases = new()
    {
        { [], FetchEnd.Ground, 50, 0, [] },
        { [.. Enumerable.Range(2, 100)], FetchEnd.Ceiling, 49, 0, [] },
        { [16, 31, 46, 70, 99, 128], FetchEnd.Gate, 142, 0, [] },
        {
            [19, 48, 77, 106, 135, 164, 193, 211, 240, 269, 298, 338, 366, 395, 424, 459, 488, 517, 546, 564, 579, 608, 637, 666, 684, 699, 728, 757, 786, 824, 852, 881, 910, 945, 974, 1003, 1032, 1061, 1090, 1119],
            FetchEnd.Finish, 1127, 8, [0, 5, 6, 7]
        },
    };

    [Theory]
    [MemberData(nameof(SharedCases))]
    public void The_shared_cases_match_the_web_simulation(int[] taps, FetchEnd end, int tick, int gatesPassed, int[] caught)
    {
        var grade = FetchFlight.Fly(SharedCourse(), taps);

        Assert.Equal((end, tick, gatesPassed), (grade.End, grade.Tick, grade.GatesPassed));
        Assert.Equal(caught, grade.Caught);
    }

    [Fact]
    public void The_autopilot_finishes_every_course_of_a_month()
    {
        var library = new GameLibrary([
            Trip(1, photos: 30, latitude: index => 30 + (index * 0.9)),
            Trip(2, photos: 4, latitude: index => index % 2 == 0 ? 10 : 60),
            Trip(3, photos: 12, latitude: index => 48.85 + (index * 0.003)),
            Trip(4, photos: 9, latitude: index => -33 - (index * 2.5)),
        ]);

        for (var date = Day; date < Day.AddDays(31); date = date.AddDays(1))
        {
            var course = FetchPuzzle.Create(library, date)!.Course;
            Assert.Equal(FetchEnd.Finish, FetchFlight.Fly(course, Autopilot(course)).End);
        }
    }

    [Fact]
    public void Taps_must_be_ticks_of_2_or_more_in_increasing_order()
    {
        Assert.True(FetchPuzzle.IsValidRun([]));
        Assert.True(FetchPuzzle.IsValidRun([2, 5, 9]));
        Assert.False(FetchPuzzle.IsValidRun([1, 5]));
        Assert.False(FetchPuzzle.IsValidRun([5, 5]));
        Assert.False(FetchPuzzle.IsValidRun([9, 5]));
        Assert.False(FetchPuzzle.IsValidRun(null));
        Assert.False(FetchPuzzle.IsValidRun([.. Enumerable.Range(2, 301)]));
    }

    [Fact]
    public void Scoring_refuses_bad_days_and_drops_taps_after_a_run_ended()
    {
        var puzzle = new FetchPuzzle(Day, 2, SharedCourse(), new FetchTrip(1, "Trip", Day, Day, null));
        var flown = Autopilot(puzzle.Course);

        Assert.Null(puzzle.Score([]));
        Assert.Null(puzzle.Score([[], [], [], []]));
        Assert.Null(puzzle.Score([flown, []]));
        Assert.Null(puzzle.Score([[3, 2]]));

        var outcome = puzzle.Score([[16, 31, 46, 70, 99, 128, 2000], flown])!;
        Assert.True(outcome.Solved);
        Assert.Equal([16, 31, 46, 70, 99, 128], outcome.Taps[0]);
        Assert.Equal(flown, outcome.Taps[1]);
        Assert.False(puzzle.Score([[]])!.Solved);
    }

    [Fact]
    public void Taps_are_stored_as_text_and_read_back()
    {
        IReadOnlyList<int>[] runs = [[14, 40, 77], [], [9, 31]];

        var text = FetchPuzzle.Encode(runs);

        Assert.Equal("14,40,77;;9,31", text);
        Assert.Equal(runs, FetchPuzzle.Decode(text));
        Assert.Equal([Array.Empty<int>()], FetchPuzzle.Decode(string.Empty));
    }

    [Fact]
    public void The_share_text_has_bones_squares_and_an_end_only()
    {
        var outcome = new FetchOutcome(
            true,
            [new FetchRunGrade(FetchEnd.Gate, 400, 3, [0, 2]), new FetchRunGrade(FetchEnd.Finish, 1084, 8, [0, 1, 3, 4, 6, 7])],
            [[], []]);

        Assert.Equal("Fibotrail Fetch #9 2/3 🦴 6/8\n🦴⬛🦴💥\n🦴🦴⬛🦴🦴⬛🦴🦴🏁", FetchPuzzle.ShareText(9, outcome));

        var unsolved = new FetchOutcome(false, [new FetchRunGrade(FetchEnd.Ground, 30, 0, [])], [[]]);
        Assert.Equal("Fibotrail Fetch #9 X/3 🦴 0/8\n💥", FetchPuzzle.ShareText(9, unsolved));
    }
}
