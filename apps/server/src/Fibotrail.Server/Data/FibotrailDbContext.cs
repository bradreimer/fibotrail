using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fibotrail.Server.Data;

public sealed class FibotrailDbContext(DbContextOptions<FibotrailDbContext> options) : DbContext(options)
{
    public DbSet<Credential> Credentials => Set<Credential>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<SignInFailure> SignInFailures => Set<SignInFailure>();

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
