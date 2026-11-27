namespace Fibotrail.Games.Fetch;

/// <summary>A box, from its left and top edges to its right and bottom edges.</summary>
public sealed record FetchBox(int Left, int Top, int Right, int Bottom);

/// <summary>
/// The numbers of the Fetch world. Lengths are in hundredths of a pixel of a 400 × 700 px world,
/// and time is in ticks of 1/60 s. Only integers are used, so the C# and TypeScript simulations
/// agree exactly. The numbers are sent with each course. See docs/specs/fetch-game.md.
/// </summary>
/// <param name="Gravity">Added to the speed each tick.</param>
/// <param name="Flap">The speed a flap sets. Negative is up.</param>
/// <param name="Speed">How far the world moves each tick.</param>
/// <param name="DogX">The left edge of the schnauzer's 64 px sprite, on screen.</param>
/// <param name="DogSize">The sprite's width and height.</param>
/// <param name="Body">The schnauzer's body, relative to the sprite's top-left corner.</param>
/// <param name="StartY">The top of the sprite when a run starts.</param>
/// <param name="Ground">Where the ground starts. A body that reaches it crashes.</param>
public sealed record FetchPhysics(
    int Gravity,
    int Flap,
    int Speed,
    int WorldWidth,
    int WorldHeight,
    int Ground,
    int DogX,
    int DogSize,
    FetchBox Body,
    int StartY,
    int GateWidth,
    int GapHeight,
    int TreatRadius)
{
    /// <summary>Hundredths of a pixel in one pixel.</summary>
    public const int Unit = 100;

    public static readonly FetchPhysics Default = new(
        Gravity: 50,
        Flap: -750,
        Speed: 200,
        WorldWidth: 400 * Unit,
        WorldHeight: 700 * Unit,
        Ground: 620 * Unit,
        DogX: 100 * Unit,
        DogSize: 64 * Unit,
        Body: new FetchBox(6 * Unit, 20 * Unit, 58 * Unit, 44 * Unit),
        StartY: 318 * Unit,
        GateWidth: 70 * Unit,
        GapHeight: 250 * Unit,
        TreatRadius: 24 * Unit);
}

/// <summary>A gate: a post with a gap to fly through.</summary>
/// <param name="X">The gate's left edge, in the world.</param>
/// <param name="Day">The trip day of the gate's photo. Day 1 is the start date.</param>
public sealed record FetchGate(int X, int GapTop, int GapBottom, int Day);

/// <summary>A treat: one of the trip's photos, as a circle to catch.</summary>
public sealed record FetchTreat(int X, int Y, string PhotoId);

/// <summary>A day's course: the physics, the gates, the treats, and the finish line.</summary>
public sealed record FetchCourse(FetchPhysics Physics, IReadOnlyList<FetchGate> Gates, IReadOnlyList<FetchTreat> Treats, int FinishX);

/// <summary>How a run ended.</summary>
public enum FetchEnd
{
    /// <summary>The body passed the finish line.</summary>
    Finish,

    /// <summary>The body touched a gate outside its gap.</summary>
    Gate,

    /// <summary>The body went above the top of the world.</summary>
    Ceiling,

    /// <summary>The body reached the ground.</summary>
    Ground,
}

/// <summary>A run's grade.</summary>
/// <param name="Tick">The tick the run ended on.</param>
/// <param name="Caught">The indexes of the caught treats, in order.</param>
public sealed record FetchRunGrade(FetchEnd End, int Tick, int GatesPassed, IReadOnlyList<int> Caught)
{
    public bool Finished => End == FetchEnd.Finish;
}

/// <summary>
/// One run in flight. Each <see cref="Step"/> is one tick. The web app has the same simulation in
/// <c>apps/web/src/games/fetch/flight.ts</c>, and both test suites hold the same cases.
/// </summary>
public sealed class FetchFlight(FetchCourse course)
{
    private readonly FetchCourse _course = course ?? throw new ArgumentNullException(nameof(course));
    private readonly SortedSet<int> _caught = [];

    public int Tick { get; private set; }

    /// <summary>The top of the sprite.</summary>
    public int Y { get; private set; } = course.Physics.StartY;

    public int Velocity { get; private set; }

    /// <summary>How far the world has moved.</summary>
    public int Scroll { get; private set; }

    public int GatesPassed { get; private set; }

    public FetchEnd? End { get; private set; }

    public IReadOnlyCollection<int> Caught => _caught;

    /// <summary>Moves one tick, with a flap or not. Does nothing once the run has ended.</summary>
    public void Step(bool flap)
    {
        if (End is not null)
        {
            return;
        }

        var physics = _course.Physics;
        Tick++;
        if (flap)
        {
            Velocity = physics.Flap;
        }

        Velocity += physics.Gravity;
        Y += Velocity;
        Scroll += physics.Speed;

        var left = physics.DogX + Scroll + physics.Body.Left;
        var right = physics.DogX + Scroll + physics.Body.Right;
        var top = Y + physics.Body.Top;
        var bottom = Y + physics.Body.Bottom;

        if (top < 0)
        {
            End = FetchEnd.Ceiling;
            return;
        }

        if (bottom >= physics.Ground)
        {
            End = FetchEnd.Ground;
            return;
        }

        var passed = 0;
        foreach (var gate in _course.Gates)
        {
            var gateRight = gate.X + physics.GateWidth;
            if (right > gate.X && left < gateRight && (top < gate.GapTop || bottom > gate.GapBottom))
            {
                End = FetchEnd.Gate;
                return;
            }

            if (left > gateRight)
            {
                passed++;
            }
        }

        GatesPassed = passed;

        long radius = physics.TreatRadius;
        for (var index = 0; index < _course.Treats.Count; index++)
        {
            var treat = _course.Treats[index];
            long dx = treat.X - Math.Clamp(treat.X, left, right);
            long dy = treat.Y - Math.Clamp(treat.Y, top, bottom);
            if ((dx * dx) + (dy * dy) <= radius * radius)
            {
                _caught.Add(index);
            }
        }

        if (left >= _course.FinishX)
        {
            End = FetchEnd.Finish;
        }
    }

    /// <summary>The grade, once the run has ended.</summary>
    public FetchRunGrade Grade() =>
        End is { } end
            ? new FetchRunGrade(end, Tick, GatesPassed, [.. _caught])
            : throw new InvalidOperationException("The run hasn't ended.");

    /// <summary>
    /// Flies a whole run. The first flap is on tick 1, when the run starts; <paramref name="taps"/>
    /// are the ticks of the flaps after it. The world always moves, so every run ends.
    /// </summary>
    public static FetchRunGrade Fly(FetchCourse course, IEnumerable<int> taps)
    {
        ArgumentNullException.ThrowIfNull(taps);
        var flight = new FetchFlight(course);
        var flaps = taps.ToHashSet();
        while (flight.End is null)
        {
            flight.Step(flight.Tick == 0 || flaps.Contains(flight.Tick + 1));
        }

        return flight.Grade();
    }
}
