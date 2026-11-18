using System.Globalization;
using System.Text.RegularExpressions;
using Fibotrail.Detection;
using Fibotrail.Server.Data;
using Fibotrail.Server.Trips;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Cli;

/// <summary>
/// <c>fibotrail trip</c>. It adds a trip by hand, for photos that carry no position, so that detection
/// can't find them. The trip is a decision, so a rescan keeps it. It also renames any trip, and pins a
/// photo to a trip, both as decisions. See docs/specs/trip-detection.md and ADR-017. It reads and writes Fibotrail's own database only.
/// </summary>
public static partial class TripCommand
{
    public const string Name = "trip";

    private const string Usage = """
        Usage:
          fibotrail trip add --start <day> --end <day> [options]   Add a trip. Days look like 2005-07-31.
          fibotrail trip list                                      Show the trips you added by hand.
          fibotrail trip remove <id>                               Remove one of them, and its decision.
          fibotrail trip rename <id> <name>                        Rename any trip. The id is the trip's id in the app.
          fibotrail trip pin <id> <photo>                          Put a photo in a trip. The photo is an Immich asset ID or photo page URL.
          fibotrail trip unpin <photo>                             Let the photo go where its own time and position put it.

        Options for add:
          --name <text>      The trip's name. Without it, the name is the place and the month, such as "Berlin, May 2005".
          --place <text>     The main place, such as "San Francisco".
          --country <text>   The country, such as "United States".
          --lat <degrees>    Where the trip was. Give --lon too. With no photo position in the days, this is the trip's stop.
          --lon <degrees>
        """;

    private static readonly string[] ValueOptions = ["--start", "--end", "--name", "--place", "--country", "--lat", "--lon"];

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

        var list = arguments is ["list"];
        var remove = arguments is ["remove", var idText] && int.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out _);
        var rename = arguments is ["rename", var renameId, _] && int.TryParse(renameId, NumberStyles.None, CultureInfo.InvariantCulture, out _);
        var pin = arguments is ["pin", var pinId, _] && int.TryParse(pinId, NumberStyles.None, CultureInfo.InvariantCulture, out _);
        var unpin = arguments is ["unpin", _];
        var add = arguments is ["add", ..] ? ParseAdd(arguments.Skip(1).ToList(), console) : null;
        if (!list && !remove && !rename && !pin && !unpin && add is null)
        {
            // ParseAdd explains its own problem. Anything else gets the usage.
            if (arguments is not ["add", ..])
            {
                console.WriteLine(Usage);
            }

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

        if (rename)
        {
            return await RenameAsync(int.Parse(arguments[1], CultureInfo.InvariantCulture), arguments[2], console, db, time, cancellationToken);
        }

        if (pin)
        {
            return await PinAsync(int.Parse(arguments[1], CultureInfo.InvariantCulture), arguments[2], console, db, detection, time, cancellationToken);
        }

        if (unpin)
        {
            return await UnpinAsync(arguments[1], console, db, detection, cancellationToken);
        }

        return await AddAsync(add!, console, db, detection, time, cancellationToken);
    }

    /// <summary>The asset ID in an Immich asset ID or a photo page URL, in lowercase. Null when there isn't one.</summary>
    internal static string? PhotoId(string text)
    {
        var match = AssetId().Match(text);
        return match.Success ? match.Value.ToLowerInvariant() : null;
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex AssetId();

    private static async Task<int> PinAsync(
        int id, string photoText, IConsoleIo console, FibotrailDbContext db, DetectionService detection, TimeProvider time, CancellationToken cancellationToken)
    {
        var photoId = PhotoId(photoText);
        if (photoId is null)
        {
            console.WriteLine("The photo needs to be an Immich asset ID, such as 5b2d32a9-e396-46aa-87fb-1d1ea88cc23e, or the URL of its photo page.");
            return 1;
        }

        var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (trip is null)
        {
            console.WriteLine($"There is no trip {id}. The id is the one the app shows for the trip.");
            return 1;
        }

        var photo = await db.Photos.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == photoId, cancellationToken);
        if (photo is null)
        {
            console.WriteLine($"Fibotrail has no photo {photoId}. Scan your library, then run this command again.");
            return 1;
        }

        var day = PinnedPhotos.DayIn(DateOnly.FromDateTime(photo.LocalTime), trip.StartDate, trip.EndDate);
        var pinned = await db.PinnedPhotos.FindAsync([photoId], cancellationToken);
        if (pinned is null)
        {
            db.PinnedPhotos.Add(new PinnedPhoto { PhotoId = photoId, Day = day, DecidedAt = time.GetUtcNow().UtcDateTime });
        }
        else
        {
            pinned.Day = day;
            pinned.DecidedAt = time.GetUtcNow().UtcDateTime;
        }

        await db.SaveChangesAsync(cancellationToken);
        console.WriteLine($"Pinned photo {photoId} to {day:yyyy-MM-dd}, a day of trip {id} (\"{trip.Name}\"). Its own day is {photo.LocalTime:yyyy-MM-dd}.");
        return await ReportAsync(photoId, console, db, detection, cancellationToken);
    }

    private static async Task<int> UnpinAsync(string photoText, IConsoleIo console, FibotrailDbContext db, DetectionService detection, CancellationToken cancellationToken)
    {
        var photoId = PhotoId(photoText);
        if (photoId is null)
        {
            console.WriteLine("The photo needs to be an Immich asset ID, such as 5b2d32a9-e396-46aa-87fb-1d1ea88cc23e, or the URL of its photo page.");
            return 1;
        }

        var pinned = await db.PinnedPhotos.FindAsync([photoId], cancellationToken);
        if (pinned is null)
        {
            console.WriteLine($"Photo {photoId} isn't pinned.");
            return 1;
        }

        db.PinnedPhotos.Remove(pinned);
        await db.SaveChangesAsync(cancellationToken);
        console.WriteLine($"Unpinned photo {photoId}.");
        return await ReportAsync(photoId, console, db, detection, cancellationToken);
    }

    /// <summary>Runs detection and says which trip holds the photo.</summary>
    private static async Task<int> ReportAsync(string photoId, IConsoleIo console, FibotrailDbContext db, DetectionService detection, CancellationToken cancellationToken)
    {
        if (!(await detection.RunAsync(cancellationToken)).Ran)
        {
            console.WriteLine("Saved. Detection runs once you confirm a home location.");
            return 0;
        }

        var holder = await db.TripPhotos.AsNoTracking()
            .Where(link => link.PhotoId == photoId)
            .Join(db.Trips.AsNoTracking(), link => link.TripId, trip => trip.Id, (_, trip) => trip)
            .FirstOrDefaultAsync(cancellationToken);
        console.WriteLine(holder is null
            ? "The photo is in no trip now."
            : $"The photo is in trip {holder.Id}, \"{holder.Name}\", {holder.StartDate:yyyy-MM-dd} to {holder.EndDate:yyyy-MM-dd}, {holder.PhotoCount.ToString("N0", CultureInfo.InvariantCulture)} photos.");
        return 0;
    }

    /// <summary>Reads the options of <c>add</c>. Prints what is wrong and returns null when something is.</summary>
    private static ManualTrip? ParseAdd(List<string> arguments, IConsoleIo console)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Count; index += 2)
        {
            if (!ValueOptions.Contains(arguments[index], StringComparer.Ordinal) || index + 1 >= arguments.Count || values.ContainsKey(arguments[index]))
            {
                console.WriteLine($"\"{arguments[index]}\" isn't an option here, has no value, or is given twice.\n{Usage}");
                return null;
            }

            values[arguments[index]] = arguments[index + 1];
        }

        if (!TryDay(values, "--start", console, out var start) || !TryDay(values, "--end", console, out var end))
        {
            return null;
        }

        if (end < start)
        {
            console.WriteLine($"The end day {end:yyyy-MM-dd} is before the start day {start:yyyy-MM-dd}.");
            return null;
        }

        double? latitude = null;
        double? longitude = null;
        if (values.ContainsKey("--lat") != values.ContainsKey("--lon"))
        {
            console.WriteLine("Give --lat and --lon together, or neither.");
            return null;
        }

        if (values.TryGetValue("--lat", out var latText) && values.TryGetValue("--lon", out var lonText))
        {
            if (!double.TryParse(latText, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) || lat is < -90 or > 90
                || !double.TryParse(lonText, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) || lon is < -180 or > 180)
            {
                console.WriteLine("--lat must be from -90 to 90 and --lon from -180 to 180, in decimal degrees, such as 37.7749 and -122.4194.");
                return null;
            }

            latitude = lat;
            longitude = lon;
        }

        return new ManualTrip
        {
            StartDate = start,
            EndDate = end,
            Name = Clean(values.GetValueOrDefault("--name")),
            Place = Clean(values.GetValueOrDefault("--place")),
            Country = Clean(values.GetValueOrDefault("--country")),
            Latitude = latitude,
            Longitude = longitude,
        };
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static bool TryDay(Dictionary<string, string> values, string option, IConsoleIo console, out DateOnly day)
    {
        day = default;
        if (values.TryGetValue(option, out var text) && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
        {
            return true;
        }

        console.WriteLine($"{option} needs a day that looks like 2005-07-31.");
        return false;
    }

    private static async Task<int> ListAsync(IConsoleIo console, FibotrailDbContext db, CancellationToken cancellationToken)
    {
        var trips = (await db.ManualTrips.AsNoTracking().ToListAsync(cancellationToken)).OrderBy(trip => trip.StartDate).ToList();
        if (trips.Count == 0)
        {
            console.WriteLine("No trips added by hand yet.");
            return 0;
        }

        foreach (var trip in trips)
        {
            var place = trip.Place is null ? string.Empty : $", {trip.Place}";
            console.WriteLine($"{trip.Id}: {trip.StartDate:yyyy-MM-dd} to {trip.EndDate:yyyy-MM-dd}, \"{trip.Name ?? TripDetector.Name(trip.Place, trip.StartDate)}\"{place}");
        }

        return 0;
    }

    private static async Task<int> RemoveAsync(int id, IConsoleIo console, FibotrailDbContext db, DetectionService detection, CancellationToken cancellationToken)
    {
        var trip = await db.ManualTrips.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (trip is null)
        {
            console.WriteLine($"There is no trip {id}. Run \"fibotrail trip list\" to see them.");
            return 1;
        }

        // The decision made with the trip goes too, so no trace is left. A detected trip on the same days comes back as a suggestion.
        var fingerprint = TripDetector.Fingerprint(trip.StartDate, trip.Place);
        var decision = await db.Decisions.FirstOrDefaultAsync(candidate => candidate.Fingerprint == fingerprint, cancellationToken);
        if (decision is not null)
        {
            db.Decisions.Remove(decision);
        }

        db.ManualTrips.Remove(trip);
        await db.SaveChangesAsync(cancellationToken);
        var result = await detection.RunAsync(cancellationToken);
        console.WriteLine(result.Ran
            ? $"Removed trip {id}. Detection found {result.Trips} trips."
            : $"Removed trip {id}. Detection runs once you confirm a home location.");
        return 0;
    }

    private static async Task<int> RenameAsync(int id, string name, IConsoleIo console, FibotrailDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var before = await db.Trips.AsNoTracking().Where(trip => trip.Id == id).Select(trip => trip.Name).FirstOrDefaultAsync(cancellationToken);
        var result = await new TripDecisions(db, time).SetDetailsAsync(id, name, null, cancellationToken);
        switch (result.Outcome)
        {
            case DecisionOutcome.Done:
                console.WriteLine($"Renamed trip {id} from \"{before}\" to \"{name.Trim()}\".");
                return 0;
            case DecisionOutcome.NotFound:
                console.WriteLine($"There is no trip {id}. The id is the one the app shows for the trip.");
                return 1;
            default:
                console.WriteLine(result.Problem!);
                return 1;
        }
    }

    private static async Task<int> AddAsync(
        ManualTrip trip,
        IConsoleIo console,
        FibotrailDbContext db,
        DetectionService detection,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var start = trip.StartDate;
        var end = trip.EndDate;

        var clash = (await db.ManualTrips.AsNoTracking().ToListAsync(cancellationToken))
            .Select(other => (Name: other.Name ?? TripDetector.Name(other.Place, other.StartDate), other.StartDate, other.EndDate))
            .Concat((await db.Trips.AsNoTracking().Where(other => other.Status == TripStatus.Added).ToListAsync(cancellationToken))
                .Select(other => (other.Name, other.StartDate, other.EndDate)))
            .FirstOrDefault(other => other.StartDate <= end && start <= other.EndDate);
        if (clash.Name is not null)
        {
            console.WriteLine($"\"{clash.Name}\" ({clash.StartDate:yyyy-MM-dd} to {clash.EndDate:yyyy-MM-dd}) is already a trip in your library and shares days with these. Pick days that no trip covers.");
            return 1;
        }

        var from = start.ToDateTime(TimeOnly.MinValue);
        var until = end.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var photos = await db.Photos.CountAsync(photo => photo.LocalTime >= from && photo.LocalTime < until, cancellationToken);
        if (photos == 0)
        {
            console.WriteLine($"No photo was taken from {start:yyyy-MM-dd} to {end:yyyy-MM-dd}, so there is nothing to put in the trip. Scan your library first, or check the days.");
            return 1;
        }

        trip.DecidedAt = time.GetUtcNow().UtcDateTime;
        db.ManualTrips.Add(trip);

        // The trip is added as if you chose Add trip in the inbox. Its fingerprint is the one detection will compute.
        var fingerprint = TripDetector.Fingerprint(start, trip.Place);
        var name = trip.Name ?? TripDetector.Name(trip.Place, start);
        var decision = await db.Decisions.FirstOrDefaultAsync(candidate => candidate.Fingerprint == fingerprint, cancellationToken);
        if (decision is null)
        {
            db.Decisions.Add(new Decision { Fingerprint = fingerprint, Status = TripStatus.Added, Name = name, StartDate = start, EndDate = end, DecidedAt = trip.DecidedAt });
        }
        else
        {
            decision.Status = TripStatus.Added;
            decision.Name = name;
            decision.StartDate = start;
            decision.EndDate = end;
            decision.DecidedAt = trip.DecidedAt;
        }

        await db.SaveChangesAsync(cancellationToken);

        var result = await detection.RunAsync(cancellationToken);
        if (!result.Ran)
        {
            console.WriteLine($"Saved the trip, with {photos.ToString("N0", CultureInfo.InvariantCulture)} photos in its days. Detection runs once you confirm a home location.");
            return 0;
        }

        db.ChangeTracker.Clear();
        var stored = await db.Trips.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Fingerprint == fingerprint, cancellationToken);
        if (stored is null)
        {
            console.WriteLine($"Saved the trip, but detection didn't produce it. Run \"fibotrail trip list\", then \"fibotrail trip remove {trip.Id}\" to undo it.");
            return 1;
        }

        console.WriteLine($"Added \"{stored.Name}\": {stored.StartDate:yyyy-MM-dd} to {stored.EndDate:yyyy-MM-dd}, {stored.Days} days, {stored.PhotoCount.ToString("N0", CultureInfo.InvariantCulture)} photos. Status: {stored.Status}.");
        return 0;
    }
}
