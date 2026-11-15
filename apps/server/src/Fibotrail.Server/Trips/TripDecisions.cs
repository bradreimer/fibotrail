using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Trips;

public enum DecisionOutcome
{
    Done,
    NotFound,

    /// <summary>The name or the cover can't be used. The message says why.</summary>
    Rejected,
}

public sealed record DecisionResult(DecisionOutcome Outcome, string? Problem = null);

/// <summary>
/// Records what you decide about a trip. Every choice is written to the decision table by
/// fingerprint, and copied to the trip row. Detection reads the decision table on each run, so a
/// rescan never undoes a choice. See ADR-002.
/// </summary>
public sealed class TripDecisions(FibotrailDbContext db, TimeProvider time)
{
    public const int MaxNameLength = 100;

    /// <summary>Sets the status: Added, Ignored, Later, or back to Suggested.</summary>
    public Task<DecisionResult> SetStatusAsync(int tripId, TripStatus status, CancellationToken cancellationToken) =>
        ChangeAsync(tripId, (_, decision) => decision.Status = status, cancellationToken);

    /// <summary>
    /// Adds the trip to your library, with the name and the cover you chose. A name or cover left
    /// out keeps what the trip has.
    /// </summary>
    public async Task<DecisionResult> AddAsync(int tripId, string? name, string? coverPhotoId, CancellationToken cancellationToken)
    {
        var details = await SetDetailsAsync(tripId, name, coverPhotoId, cancellationToken);
        return details.Outcome == DecisionOutcome.Done
            ? await SetStatusAsync(tripId, TripStatus.Added, cancellationToken)
            : details;
    }

    /// <summary>Renames the trip, changes its cover, or both.</summary>
    public async Task<DecisionResult> SetDetailsAsync(int tripId, string? name, string? coverPhotoId, CancellationToken cancellationToken)
    {
        var trimmed = name?.Trim();
        if (name is not null && (trimmed!.Length == 0 || trimmed.Length > MaxNameLength))
        {
            return new DecisionResult(DecisionOutcome.Rejected, $"Give the trip a name of 1 to {MaxNameLength} characters.");
        }

        if (coverPhotoId is not null
            && !await db.TripPhotos.AnyAsync(photo => photo.TripId == tripId && photo.PhotoId == coverPhotoId, cancellationToken)
            && await db.Trips.AnyAsync(trip => trip.Id == tripId, cancellationToken))
        {
            return new DecisionResult(DecisionOutcome.Rejected, "Choose a cover from the photos of this trip.");
        }

        return await ChangeAsync(
            tripId,
            (trip, decision) =>
            {
                if (trimmed is not null)
                {
                    // The suggested name isn't a decision. It may improve when the trip gains photos.
                    decision.Name = trimmed == trip.SuggestedName ? null : trimmed;
                }

                if (coverPhotoId is not null)
                {
                    decision.CoverPhotoId = coverPhotoId == trip.SuggestedCoverPhotoId ? null : coverPhotoId;
                }
            },
            cancellationToken);
    }

    private async Task<DecisionResult> ChangeAsync(int tripId, Action<Trip, Decision> change, CancellationToken cancellationToken)
    {
        var trip = await db.Trips.FirstOrDefaultAsync(candidate => candidate.Id == tripId, cancellationToken);
        if (trip is null)
        {
            return new DecisionResult(DecisionOutcome.NotFound);
        }

        var decision = await db.Decisions.FirstOrDefaultAsync(candidate => candidate.Fingerprint == trip.Fingerprint, cancellationToken);
        if (decision is null)
        {
            decision = new Decision { Fingerprint = trip.Fingerprint };
            db.Decisions.Add(decision);
        }

        change(trip, decision);
        decision.StartDate = trip.StartDate;
        decision.EndDate = trip.EndDate;
        decision.DecidedAt = time.GetUtcNow().UtcDateTime;

        // The trip row carries a copy, so lists read one table.
        trip.Status = decision.Status ?? TripStatus.Suggested;
        trip.Name = decision.Name ?? trip.SuggestedName;
        trip.CoverPhotoId = decision.CoverPhotoId ?? trip.SuggestedCoverPhotoId;
        await db.SaveChangesAsync(cancellationToken);
        return new DecisionResult(DecisionOutcome.Done);
    }
}
