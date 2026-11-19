using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fibotrail.Server.Data;

public sealed class FibotrailDbContext(DbContextOptions<FibotrailDbContext> options) : DbContext(options)
{
    public DbSet<Credential> Credentials => Set<Credential>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<SignInFailure> SignInFailures => Set<SignInFailure>();

    public DbSet<Photo> Photos => Set<Photo>();

    public DbSet<ScanRun> ScanRuns => Set<ScanRun>();

    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<TripPhoto> TripPhotos => Set<TripPhoto>();

    public DbSet<Decision> Decisions => Set<Decision>();

    public DbSet<OnTheWayPhoto> OnTheWayPhotos => Set<OnTheWayPhoto>();

    public DbSet<PinnedPhoto> PinnedPhotos => Set<PinnedPhoto>();

    public DbSet<TripJoin> TripJoins => Set<TripJoin>();

    public DbSet<ManualTrip> ManualTrips => Set<ManualTrip>();

    public DbSet<Setting> Settings => Set<Setting>();

    public DbSet<GameResult> GameResults => Set<GameResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Credential>(entity =>
        {
            entity.ToTable("credential");
            entity.Property(credential => credential.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.ToTable("session");
            entity.HasIndex(session => session.TokenHash).IsUnique();
        });

        modelBuilder.Entity<SignInFailure>(entity =>
        {
            entity.ToTable("sign_in_failure");
            entity.HasIndex(failure => new { failure.Address, failure.OccurredAt });
        });

        modelBuilder.Entity<Photo>(entity =>
        {
            entity.ToTable("photo");
            entity.HasIndex(photo => photo.LocalTime);
            entity.HasIndex(photo => photo.ImmichUpdatedAt);
        });

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.ToTable("trip");
            entity.HasIndex(trip => trip.Fingerprint).IsUnique();
            entity.HasIndex(trip => new { trip.Status, trip.StartDate });
            entity.Property(trip => trip.Status).HasConversion<string>();
            entity.Property(trip => trip.Confidence).HasConversion<string>();
            entity.HasMany(trip => trip.Photos).WithOne().HasForeignKey(photo => photo.TripId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TripPhoto>(entity =>
        {
            entity.ToTable("trip_photo");
            entity.HasKey(photo => new { photo.TripId, photo.PhotoId });
        });

        modelBuilder.Entity<Decision>(entity =>
        {
            entity.ToTable("decision");
            entity.HasIndex(decision => decision.Fingerprint).IsUnique();
            entity.Property(decision => decision.Status).HasConversion<string>();
        });

        modelBuilder.Entity<TripJoin>(entity => entity.ToTable("trip_join"));

        modelBuilder.Entity<ManualTrip>(entity => entity.ToTable("manual_trip"));

        modelBuilder.Entity<OnTheWayPhoto>(entity =>
        {
            entity.ToTable("on_the_way_photo");
            entity.HasKey(photo => photo.PhotoId);
        });

        modelBuilder.Entity<PinnedPhoto>(entity =>
        {
            entity.ToTable("pinned_photo");
            entity.HasKey(photo => photo.PhotoId);
        });

        modelBuilder.Entity<Setting>(entity =>
        {
            entity.ToTable("setting");
            entity.HasKey(setting => setting.Key);
        });

        modelBuilder.Entity<GameResult>(entity =>
        {
            entity.ToTable("game_result");
            entity.HasIndex(result => new { result.Game, result.PuzzleDate }).IsUnique();
        });

        modelBuilder.Entity<ScanRun>(entity =>
        {
            entity.ToTable("scan_run");
            entity.Property(run => run.State).HasConversion<string>();
        });
    }
}

/// <summary>Lets the dotnet-ef tool create the context when it writes a migration.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FibotrailDbContext>
{
    public FibotrailDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FibotrailDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new FibotrailDbContext(options);
    }
}
