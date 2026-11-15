namespace Fibotrail.Server.Auth;

/// <summary>The one rule for a password: at least 12 characters. No composition rule applies.</summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 12;

    /// <summary>A ceiling that keeps one request from hashing megabytes of text.</summary>
    public const int MaximumLength = 1024;

    public const string TooShort = "Use at least 12 characters.";
    public const string TooLong = "Use 1,024 characters or fewer.";
    public const string Mismatch = "Those two passwords don't match.";

    /// <summary>Returns what is wrong with the password, in words for the person, or null when it's fine.</summary>
    public static string? Check(string? password, string? confirmation)
    {
        if (password is null || password.Length < MinimumLength)
        {
            return TooShort;
        }

        if (password.Length > MaximumLength)
        {
            return TooLong;
        }

        return string.Equals(password, confirmation, StringComparison.Ordinal) ? null : Mismatch;
    }
}
