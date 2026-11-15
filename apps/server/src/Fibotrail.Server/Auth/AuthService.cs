using System.Security.Cryptography;
using System.Text;
using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Auth;

public enum SetupOutcome
{
    Done,
    AlreadySetUp,
    Rejected,
}

/// <param name="Outcome">What happened.</param>
/// <param name="Problem">Words for the person when the password was rejected.</param>
public sealed record PasswordChange(SetupOutcome Outcome, string? Problem = null);

/// <summary>What a valid session cookie resolves to.</summary>
/// <param name="Refreshed">True when the last-use time moved, so the cookie should be sent again.</param>
public sealed record SessionCheck(bool Refreshed);

/// <summary>The password and the sessions. Everything is stored in the database, so the
/// reset command can change it while the server runs.</summary>
public sealed class AuthService(FibotrailDbContext db, PasswordHasher hasher, TimeProvider time)
{
    /// <summary>A session lasts 30 days from its last use.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    /// <summary>The last-use time is written at most this often, to keep reads from becoming writes.</summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public Task<bool> IsPasswordSetAsync(CancellationToken cancellationToken = default) =>
        db.Credentials.AnyAsync(cancellationToken);

    /// <summary>Stores the first password. It works only while no password is stored.</summary>
    public async Task<PasswordChange> SetUpAsync(
        string? password,
        string? confirmation,
        CancellationToken cancellationToken = default)
    {
        if (await IsPasswordSetAsync(cancellationToken))
        {
            return new PasswordChange(SetupOutcome.AlreadySetUp);
        }

        var problem = PasswordPolicy.Check(password, confirmation);
        if (problem is not null)
        {
            return new PasswordChange(SetupOutcome.Rejected, problem);
        }

        db.Credentials.Add(new Credential { PasswordHash = hasher.Hash(password!), UpdatedAt = Now });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request stored a password first. The row's fixed Id makes the second insert fail.
            db.ChangeTracker.Clear();
            return new PasswordChange(SetupOutcome.AlreadySetUp);
        }

        return new PasswordChange(SetupOutcome.Done);
    }

    /// <summary>
    /// Replaces the password, ends every session, and clears the sign-in delay. It changes nothing
    /// when the password is rejected.
    /// </summary>
    public async Task<PasswordChange> ResetPasswordAsync(
        string? password,
        string? confirmation,
        CancellationToken cancellationToken = default)
    {
        var problem = PasswordPolicy.Check(password, confirmation);
        if (problem is not null)
        {
            return new PasswordChange(SetupOutcome.Rejected, problem);
        }

        var hash = hasher.Hash(password!);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var credential = await db.Credentials.SingleOrDefaultAsync(cancellationToken);
        if (credential is null)
        {
            db.Credentials.Add(new Credential { PasswordHash = hash, UpdatedAt = Now });
        }
        else
        {
            credential.PasswordHash = hash;
            credential.UpdatedAt = Now;
        }

        await db.SaveChangesAsync(cancellationToken);
        await db.Sessions.ExecuteDeleteAsync(cancellationToken);
        await db.SignInFailures.ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PasswordChange(SetupOutcome.Done);
    }

    public async Task<bool> VerifyPasswordAsync(string? password, CancellationToken cancellationToken = default)
    {
        if (password is null || password.Length > PasswordPolicy.MaximumLength)
        {
            return false;
        }

        var credential = await db.Credentials.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return credential is not null && PasswordHasher.Verify(password, credential.PasswordHash);
    }

    /// <summary>Starts a session and returns the token for the cookie. Only its hash is stored.</summary>
    public async Task<string> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.Sessions.Add(new Session { TokenHash = HashToken(token), CreatedAt = Now, LastUsedAt = Now });
        await db.SaveChangesAsync(cancellationToken);
        return token;
    }

    /// <summary>Returns null when the token names no live session.</summary>
    public async Task<SessionCheck?> CheckSessionAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 128)
        {
            return null;
        }

        var hash = HashToken(token);
        var session = await db.Sessions.SingleOrDefaultAsync(row => row.TokenHash == hash, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var now = Now;
        if (now - session.LastUsedAt > SessionLifetime)
        {
            db.Sessions.Remove(session);
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (now - session.LastUsedAt < RefreshInterval)
        {
            return new SessionCheck(Refreshed: false);
        }

        session.LastUsedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return new SessionCheck(Refreshed: true);
    }

    public async Task EndSessionAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        var hash = HashToken(token);
        await db.Sessions.Where(row => row.TokenHash == hash).ExecuteDeleteAsync(cancellationToken);
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
