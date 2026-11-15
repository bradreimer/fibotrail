namespace Fibotrail.Server.Data;

/// <summary>The one password. The table holds at most one row, with Id 1.</summary>
public sealed class Credential
{
    public const int SingleId = 1;

    public int Id { get; set; } = SingleId;

    /// <summary>An Argon2id hash in PHC string format. Never the password itself.</summary>
    public required string PasswordHash { get; set; }

    public DateTime UpdatedAt { get; set; }
}

/// <summary>A signed-in browser. The cookie holds the token, and the table holds only its hash.</summary>
public sealed class Session
{
    public int Id { get; set; }

    public required string TokenHash { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastUsedAt { get; set; }
}

/// <summary>One failed sign-in attempt, kept for the sign-in delay.</summary>
public sealed class SignInFailure
{
    public int Id { get; set; }

    public required string Address { get; set; }

    public DateTime OccurredAt { get; set; }
}
