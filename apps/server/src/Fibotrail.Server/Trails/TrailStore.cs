using System.Text.Json;
using Fibotrail.Server.Data;
using Fibotrail.Trails;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Trails;

/// <summary>What a GPX file would add, before it's saved.</summary>
/// <param name="OriginalName">The name the file had when you picked it.</param>
/// <param name="StartUtc">The first point's time, in UTC. The web app shows it in the device's time zone.</param>
/// <param name="MovingMinutes">Minutes between points where the speed was above 1 km/h.</param>
/// <param name="OverlapsTrip">False when the trail's dates don't overlap the trip's, so the sheet warns before saving.</param>
public sealed record TrailPreview(string OriginalName, DateTime StartUtc, DateTime EndUtc, double DistanceKm, int MovingMinutes, int PointCount, bool OverlapsTrip);

/// <summary>A trail of a trip, with the simplified line the map draws.</summary>
/// <param name="Segments">The line, split at gaps. The map draws each segment on its own and never joins them.</param>
public sealed record TrailInfo(int Id, string OriginalName, DateTime StartUtc, DateTime EndUtc, double DistanceKm, int MovingMinutes, int PointCount, IReadOnlyList<IReadOnlyList<TrailPosition>> Segments);

/// <summary>Where a trail's offset came from.</summary>
public enum OffsetSource
{
    /// <summary>You chose it with Adjust time. An estimate never replaces it.</summary>
    You,

    /// <summary>Fibotrail estimated it from the photos that have a position of their own.</summary>
    Estimated,

    /// <summary>There was nothing to estimate from, so the offset is 0.</summary>
    None,

    /// <summary>A trial offset from the Adjust time sheet. Nothing is saved.</summary>
    Trial,
}

/// <summary>A trip photo that lands on a trail.</summary>
/// <param name="Estimated">True when the photo has no position of its own, so the trail gives the only one.</param>
public sealed record AlignedPhotoInfo(string Id, DateTime LocalTime, double Latitude, double Longitude, bool Estimated);

/// <summary>How a trip's photos land on one trail.</summary>
/// <param name="OffsetMinutes">The offset used: photo local time = trail UTC time + offset.</param>
/// <param name="EstimatedOffsetMinutes">The estimate, or null when there is none. Shown by Use estimate.</param>
/// <param name="Photos">The photos that land on the trail, in time order.</param>
public sealed record TrailAlignment(int TrailId, int OffsetMinutes, OffsetSource Source, int? EstimatedOffsetMinutes, IReadOnlyList<AlignedPhotoInfo> Photos);

/// <summary>How a trip's photos land on its trails.</summary>
/// <param name="Outside">The photos that land on no trail, in time order.</param>
/// <param name="PhotoCount">All the trip's photos.</param>
public sealed record TripAlignment(IReadOnlyList<TrailAlignment> Trails, IReadOnlyList<string> Outside, int PhotoCount);

/// <summary>An offset you chose for a trail, in minutes, or null to use the estimate.</summary>
public sealed record OffsetChange(int? Minutes);

public enum TrailOutcome
{
    Done,

    /// <summary>No trip, or no trail of that trip, has that ID.</summary>
    NotFound,

    /// <summary>The file can't be used. The problem says why.</summary>
    Refused,
}

public sealed record TrailResult<T>(TrailOutcome Outcome, T? Value = default, string? Problem = null);

/// <summary>
/// Adds, lists, and removes the GPS trails of a trip. The original file is kept byte for byte, and
/// the row holds the numbers and the line for the map. Nothing here reads or writes Immich. See
/// docs/specs/gps-trails.md and ADR-024.
/// </summary>
public sealed class TrailStore(FibotrailDbContext db, ServerSettings settings, TimeProvider time, TrailFileCache files)
{
    /// <summary>The largest file accepted: 50 MB, about a month of points every second.</summary>
    public const long MaxBytes = 50L * 1024 * 1024;

    public const int MaxNameLength = 200;

    public const string TooLarge = "This file is larger than 50 MB. Split it into one file for each day with GPSBabel, then add each one.";

    private static readonly JsonSerializerOptions LineJson = new(JsonSerializerDefaults.Web);

    /// <summary>Checks a file and returns what it would add, without saving anything.</summary>
    public async Task<TrailResult<TrailPreview>> PreviewAsync(int tripId, string? name, byte[] file, CancellationToken cancellationToken)
    {
        var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == tripId, cancellationToken);
        if (trip is null)
        {
            return new(TrailOutcome.NotFound);
        }

        var read = Read(file);
        return read.Problem is not null
            ? new(TrailOutcome.Refused, Problem: read.Problem)
            : new(TrailOutcome.Done, Preview(CleanName(name), read.Summary!, trip));
    }

    /// <summary>Checks a file, keeps it unchanged in the data folder, and adds it to the trip.</summary>
    public async Task<TrailResult<TrailInfo>> AddAsync(int tripId, string? name, byte[] file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        var trip = await db.Trips.FirstOrDefaultAsync(candidate => candidate.Id == tripId, cancellationToken);
        if (trip is null)
        {
            return new(TrailOutcome.NotFound);
        }

        var read = Read(file);
        if (read.Problem is not null)
        {
            return new(TrailOutcome.Refused, Problem: read.Problem);
        }

        var decision = await DecisionOfAsync(trip, cancellationToken);
        var fileName = $"{Guid.NewGuid():N}.gpx";
        Directory.CreateDirectory(settings.TrailDirectory);
        var path = Path.Combine(settings.TrailDirectory, fileName);
        await File.WriteAllBytesAsync(path, file, cancellationToken);

        var summary = read.Summary!;
        var trail = new Trail
        {
            DecisionId = decision.Id,
            FileName = fileName,
            OriginalName = CleanName(name),
            StartUtc = summary.Start,
            EndUtc = summary.End,
            DistanceKm = summary.DistanceKm,
            MovingSeconds = (int)summary.MovingTime.TotalSeconds,
            PointCount = summary.PointCount,
            LineJson = JsonSerializer.Serialize(TrailMath.Simplify(read.Segments!), LineJson),
            AddedAt = time.GetUtcNow().UtcDateTime,
        };
        db.Trails.Add(trail);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // No row, so no file either.
            File.Delete(path);
            throw;
        }

        return new(TrailOutcome.Done, ToInfo(trail));
    }

    /// <summary>The trails of a trip, oldest first. Null when there's no such trip.</summary>
    public async Task<IReadOnlyList<TrailInfo>?> ListAsync(int tripId, CancellationToken cancellationToken)
    {
        var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == tripId, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        return [.. (await TrailsOfAsync(trip, cancellationToken)).Select(ToInfo)];
    }

    private Task<List<Trail>> TrailsOfAsync(Trip trip, CancellationToken cancellationToken) =>
        db.Trails.AsNoTracking()
            .Where(trail => db.Decisions.Any(decision => decision.Id == trail.DecisionId && decision.Fingerprint == trip.Fingerprint))
            .OrderBy(trail => trail.StartUtc)
            .ThenBy(trail => trail.Id)
            .ToListAsync(cancellationToken);

    /// <summary>Removes a trail of a trip: its row and its file.</summary>
    public async Task<TrailOutcome> RemoveAsync(int tripId, int trailId, CancellationToken cancellationToken)
    {
        var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == tripId, cancellationToken);
        var trail = trip is null ? null : await db.Trails
            .Where(candidate => candidate.Id == trailId && db.Decisions.Any(decision => decision.Id == candidate.DecisionId && decision.Fingerprint == trip.Fingerprint))
            .FirstOrDefaultAsync(cancellationToken);
        if (trail is null)
        {
            return TrailOutcome.NotFound;
        }

        db.Trails.Remove(trail);
        await db.SaveChangesAsync(cancellationToken);
        var path = Path.Combine(settings.TrailDirectory, trail.FileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return TrailOutcome.Done;
    }

    /// <summary>
    /// Places the trip's photos on each of its trails. A trial offset for one trail shows what Adjust
    /// time would do, without saving. Null when there's no such trip. Positions from a trail are
    /// shown only, never stored. See ADR-025.
    /// </summary>
    public async Task<TripAlignment?> AlignAsync(int tripId, int? trialTrailId, int? trialOffsetMinutes, CancellationToken cancellationToken)
    {
        var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == tripId, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        var trails = await TrailsOfAsync(trip, cancellationToken);
        var photos = (await Trips.InboxRoutes.TripPhotosAsync(db, tripId, cancellationToken))
            .Select(photo => new AlignPhoto(photo.Id, photo.LocalTime, photo.Latitude, photo.Longitude))
            .ToList();

        var onAnyTrail = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<TrailAlignment>();
        foreach (var trail in trails)
        {
            var segments = files.Get(Path.Combine(settings.TrailDirectory, trail.FileName));
            if (segments is null)
            {
                // The line is in the row, so the trail still draws. Without its file, no photo can be placed.
                result.Add(new TrailAlignment(trail.Id, trail.OffsetMinutes ?? 0, trail.OffsetMinutes is null ? OffsetSource.None : OffsetSource.You, null, []));
                continue;
            }

            var estimate = PhotoAlignment.EstimateOffset(photos, segments);
            var (offset, source) = trail.Id == trialTrailId && trialOffsetMinutes is { } trial
                ? (trial, OffsetSource.Trial)
                : trail.OffsetMinutes is { } chosen ? (chosen, OffsetSource.You)
                : estimate is { } estimated ? (estimated, OffsetSource.Estimated)
                : (0, OffsetSource.None);
            var aligned = PhotoAlignment.Align(photos, segments, offset)
                .Where(photo => !photo.Outside)
                .Select(photo => new AlignedPhotoInfo(photo.Photo.Id, photo.Photo.LocalTime, photo.Position!.Latitude, photo.Position.Longitude, !photo.Photo.HasPosition))
                .ToList();
            onAnyTrail.UnionWith(aligned.Select(photo => photo.Id));
            result.Add(new TrailAlignment(trail.Id, offset, source, estimate, aligned));
        }

        return new TripAlignment(result, [.. photos.Where(photo => !onAnyTrail.Contains(photo.Id)).Select(photo => photo.Id)], photos.Count);
    }

    /// <summary>Saves the offset you chose for a trail, or removes it to use the estimate.</summary>
    public async Task<TrailResult<int?>> SetOffsetAsync(int tripId, int trailId, int? minutes, CancellationToken cancellationToken)
    {
        if (minutes is < -PhotoAlignment.MaxOffsetMinutes or > PhotoAlignment.MaxOffsetMinutes)
        {
            return new(TrailOutcome.Refused, Problem: "Choose an offset from 14 hours behind to 14 hours ahead.");
        }

        var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == tripId, cancellationToken);
        var trail = trip is null ? null : await db.Trails
            .Where(candidate => candidate.Id == trailId && db.Decisions.Any(decision => decision.Id == candidate.DecisionId && decision.Fingerprint == trip.Fingerprint))
            .FirstOrDefaultAsync(cancellationToken);
        if (trail is null)
        {
            return new(TrailOutcome.NotFound);
        }

        trail.OffsetMinutes = minutes;
        await db.SaveChangesAsync(cancellationToken);
        return new(TrailOutcome.Done, minutes);
    }

    /// <summary>The full path of a trail's stored file. For tests and maintenance.</summary>
    public string PathOf(Trail trail)
    {
        ArgumentNullException.ThrowIfNull(trail);
        return Path.Combine(settings.TrailDirectory, trail.FileName);
    }

    /// <summary>The name a picked file had, without any folder, or "trail.gpx" when it had none.</summary>
    public static string CleanName(string? name)
    {
        var clean = Path.GetFileName((name ?? string.Empty).Replace('\\', '/')).Trim();
        clean = new string([.. clean.Where(character => !char.IsControl(character))]);
        if (clean.Length == 0)
        {
            return "trail.gpx";
        }

        return clean.Length <= MaxNameLength ? clean : clean[..MaxNameLength];
    }

    private static (string? Problem, IReadOnlyList<IReadOnlyList<TrailPoint>>? Segments, TrailSummary? Summary) Read(byte[] file)
    {
        using var stream = new MemoryStream(file, writable: false);
        var parsed = GpxParser.Parse(stream);
        if (parsed.Trail is null)
        {
            return (parsed.Problem, null, null);
        }

        var segments = TrailMath.SplitAtGaps(parsed.Trail.Segments);
        return (null, segments, TrailMath.Summarize(segments));
    }

    private static TrailPreview Preview(string name, TrailSummary summary, Trip trip) =>
        new(name, summary.Start, summary.End, Math.Round(summary.DistanceKm, 2), (int)Math.Round(summary.MovingTime.TotalMinutes), summary.PointCount, TrailMath.Overlaps(summary, trip.StartDate, trip.EndDate));

    private static TrailInfo ToInfo(Trail trail) =>
        new(
            trail.Id,
            trail.OriginalName,
            DateTime.SpecifyKind(trail.StartUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(trail.EndUtc, DateTimeKind.Utc),
            Math.Round(trail.DistanceKm, 2),
            (int)Math.Round(trail.MovingSeconds / 60.0),
            trail.PointCount,
            JsonSerializer.Deserialize<List<List<TrailPosition>>>(trail.LineJson, LineJson) ?? []);

    /// <summary>
    /// The trip's decision, made with no choice in it when the trip has none yet. A trail hangs from
    /// it, because a decision follows its trip across rescans and a trip row may not. See ADR-010.
    /// </summary>
    private async Task<Decision> DecisionOfAsync(Trip trip, CancellationToken cancellationToken)
    {
        var decision = await db.Decisions.FirstOrDefaultAsync(candidate => candidate.Fingerprint == trip.Fingerprint, cancellationToken);
        if (decision is not null)
        {
            return decision;
        }

        decision = new Decision
        {
            Fingerprint = trip.Fingerprint,
            StartDate = trip.StartDate,
            EndDate = trip.EndDate,
            DecidedAt = time.GetUtcNow().UtcDateTime,
        };
        db.Decisions.Add(decision);
        await db.SaveChangesAsync(cancellationToken);
        return decision;
    }
}
