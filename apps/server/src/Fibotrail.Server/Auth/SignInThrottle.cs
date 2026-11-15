using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Auth;

/// <summary>Waits before an answer. A test replaces it, so no test sleeps for 30 seconds.</summary>
public interface ISignInDelay
{
    Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken);
}

public sealed class SignInDelay(TimeProvider time) : ISignInDelay
{
    public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        Task.Delay(duration, time, cancellationToken);
}

/// <summary>
/// The sign-in delay: after 5 failed attempts from one address in 15 minutes, each further attempt
/// waits 30 seconds before it's answered.
/// </summary>
public sealed class SignInThrottle(FibotrailDbContext db, TimeProvider time, ISignInDelay delay)
{
    public const int FreeAttempts = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(30);

    private DateTime WindowStart => time.GetUtcNow().UtcDateTime - Window;

    /// <summary>Waits when the address has used up its free attempts. Returns true when it waited.</summary>
    public async Task<bool> WaitIfNeededAsync(string address, CancellationToken cancellationToken = default)
    {
        var windowStart = WindowStart;
        var failures = await db.SignInFailures.CountAsync(
            failure => failure.Address == address && failure.OccurredAt > windowStart,
            cancellationToken);
        if (failures < FreeAttempts)
        {
            return false;
        }

        await delay.WaitAsync(Delay, cancellationToken);
        return true;
    }

    public async Task RecordFailureAsync(string address, CancellationToken cancellationToken = default)
    {
        var windowStart = WindowStart;
        await db.SignInFailures.Where(failure => failure.OccurredAt <= windowStart).ExecuteDeleteAsync(cancellationToken);
        db.SignInFailures.Add(new SignInFailure { Address = address, OccurredAt = time.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task ClearAsync(string address, CancellationToken cancellationToken = default) =>
        db.SignInFailures.Where(failure => failure.Address == address).ExecuteDeleteAsync(cancellationToken);
}
