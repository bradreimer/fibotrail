using System.Globalization;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Cli;

/// <summary>
/// <c>fibotrail join</c>. It makes two trips one trip, for the case where you went more than the gap
/// limit without taking a picture. The join is a decision, so a rescan keeps it. See
/// docs/specs/trip-detection.md and ADR-016. It reads and writes Fibotrail's own database only.
/// </summary>
public static class JoinCommand
{
    public const string Name = "join";

    private const string Usage = """
        Usage:
          fibotrail join <day> <day>     Join the trips that cover these two days. Days look like 2027-03-15.
          fibotrail join --list          Show the joins you made.
          fibotrail join --remove <id>   Undo a join. The trips split again.
        """;

    public static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        IConsoleIo console,
        FibotrailDbContext db,
        DetectionService detection,
        ServerSettings settings,
        TimeProvider time,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(detection);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(time);

        var list = arguments is ["--list"];
        var remove = arguments is ["--remove", var idText] && int.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out _);
        var days = arguments.Count == 2 ? ParseDays(arguments) : null;
        if (!list && !remove && days is null)
        {
            console.WriteLine(Usage);
            return 2;
        }

        if (!File.Exists(settings.DatabasePath))
        {
            console.WriteLine($"No database at {settings.DatabasePath}. Start Fibotrail and scan your library, then run this command again.");
            return 1;
        }

        if (list)
        {
            return await ListAsync(console, db, cancellationToken);
        }

        if (remove)
        {
            return await RemoveAsync(int.Parse(arguments[1], CultureInfo.InvariantCulture), console, db, detection, cancellationToken);
        }

        return await JoinAsync(days!.Value.First, days.Value.Second, console, db, detection, time, cancellationToken);
    }

    private static (DateOnly First, DateOnly Second)? ParseDays(IReadOnlyList<string> arguments) =>
        DateOnly.TryParseExact(arguments[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first)
            && DateOnly.TryParseExact(arguments[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var second)
            ? (first, second)
            : null;

    private static async Task<int> ListAsync(IConsoleIo console, FibotrailDbContext db, CancellationToken cancellationToken)
    {
        var joins = (await db.TripJoins.AsNoTracking().ToListAsync(cancellationToken)).OrderBy(join => join.StartDate).ToList();
        if (joins.Count == 0)
        {
            console.WriteLine("No joins yet.");
            return 0;
        }

        foreach (var join in joins)
        {
            console.WriteLine($"{join.Id}: {join.StartDate:yyyy-MM-dd} to {join.EndDate:yyyy-MM-dd}");
        }

        return 0;
    }

    private static async Task<int> RemoveAsync(int id, IConsoleIo console, FibotrailDbContext db, DetectionService detection, CancellationToken cancellationToken)
    {
        var join = await db.TripJoins.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (join is null)
        {
            console.WriteLine($"There is no join {id}. Run \"fibotrail join --list\" to see them.");
            return 1;
        }

        db.TripJoins.Remove(join);
        await db.SaveChangesAsync(cancellationToken);
        var result = await detection.RunAsync(cancellationToken);
        console.WriteLine(result.Ran
            ? $"Removed join {id}. Detection found {result.Trips} trips."
            : $"Removed join {id}. Detection runs once you confirm a home location.");
        return 0;
    }

    private static async Task<int> JoinAsync(
        DateOnly first,
        DateOnly second,
        IConsoleIo console,
        FibotrailDbContext db,
        DetectionService detection,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var (earlier, later) = first <= second ? (first, second) : (second, first);
        var trips = await db.Trips.ToListAsync(cancellationToken);
        var tripA = trips.FirstOrDefault(trip => trip.StartDate <= earlier && earlier <= trip.EndDate);
        var tripB = trips.FirstOrDefault(trip => trip.StartDate <= later && later <= trip.EndDate);
        if (tripA is null || tripB is null)
        {
            var missing = tripA is null ? earlier : later;
            console.WriteLine($"No trip covers {missing:yyyy-MM-dd}. Pick a day inside each of the two trips. Trips found: {trips.Count}.");
            return 1;
        }

        if (tripA.Id == tripB.Id)
        {
            console.WriteLine($"Both days are in the same trip, \"{tripA.Name}\". Pick one day in each trip.");
            return 1;
        }

        var start = tripA.StartDate < tripB.StartDate ? tripA.StartDate : tripB.StartDate;
        var end = tripA.EndDate > tripB.EndDate ? tripA.EndDate : tripB.EndDate;
        var decidedAt = time.GetUtcNow().UtcDateTime;
        db.TripJoins.Add(new TripJoin { StartDate = start, EndDate = end, DecidedAt = decidedAt });

        var kept = await CarryDecisionAsync(db, tripA, tripB, start, end, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var result = await detection.RunAsync(cancellationToken);
        if (!result.Ran)
        {
            console.WriteLine("Saved the join. Detection runs once you confirm a home location.");
            return 0;
        }

        db.ChangeTracker.Clear();
        var joined = await db.Trips.AsNoTracking().Where(trip => trip.StartDate <= earlier && earlier <= trip.EndDate).FirstOrDefaultAsync(cancellationToken);
        var stillSplit = joined is null || joined.StartDate > later || joined.EndDate < later;
        if (stillSplit)
        {
            console.WriteLine($"Saved the join, but {earlier:yyyy-MM-dd} and {later:yyyy-MM-dd} still show as separate trips. Run \"fibotrail join --list\", then \"fibotrail join --remove <id>\" to undo it.");
            return 1;
        }

        console.WriteLine($"Joined into \"{joined!.Name}\": {joined.StartDate:yyyy-MM-dd} to {joined.EndDate:yyyy-MM-dd}, {joined.Days} days, {joined.PhotoCount.ToString("N0", CultureInfo.InvariantCulture)} photos. Status: {joined.Status}.");
        if (kept is not null)
        {
            console.WriteLine(kept);
        }

        return 0;
    }

    /// <summary>
    /// The joined trip is a new detected trip, so it needs one decision. An added trip wins over the
    /// other, then the earlier trip's decision wins. The decision takes the joined days, so the
    /// reconciler finds it. The other decision isn't deleted: its fingerprint is freed, and it applies
    /// again if the join is removed.
    /// </summary>
    private static async Task<string?> CarryDecisionAsync(FibotrailDbContext db, Trip tripA, Trip tripB, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var decisionA = await db.Decisions.FirstOrDefaultAsync(decision => decision.Fingerprint == tripA.Fingerprint, cancellationToken);
        var decisionB = await db.Decisions.FirstOrDefaultAsync(decision => decision.Fingerprint == tripB.Fingerprint, cancellationToken);
        var adopted = decisionB?.Status == TripStatus.Added && decisionA?.Status != TripStatus.Added ? decisionB : decisionA ?? decisionB;
        if (adopted is null)
        {
            return null;
        }

        var dropped = ReferenceEquals(adopted, decisionA) ? decisionB : decisionA;
        if (dropped is not null)
        {
            // Saved first, so the freed fingerprint never collides with a trip detected a moment later.
            dropped.Fingerprint = $"joined-{dropped.Id}";
        }

        adopted.StartDate = start;
        adopted.EndDate = end;
        return dropped is null
            ? null
            : $"Kept the decision you made on \"{(ReferenceEquals(adopted, decisionA) ? tripA.Name : tripB.Name)}\". The one on \"{(ReferenceEquals(adopted, decisionA) ? tripB.Name : tripA.Name)}\" is set aside, and applies again if you remove the join.";
    }
}
