using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Fibotrail.Server.Auth;

/// <summary>The cost of one Argon2id hash.</summary>
/// <param name="MemoryKib">Memory in kibibytes.</param>
/// <param name="Iterations">Passes over the memory.</param>
/// <param name="Parallelism">Lanes.</param>
public sealed record Argon2Cost(int MemoryKib, int Iterations, int Parallelism)
{
    /// <summary>The OWASP minimum for Argon2id: 19 MiB, 2 iterations, 1 lane.</summary>
    public static Argon2Cost Default { get; } = new(19456, 2, 1);
}

/// <summary>
/// Hashes and verifies the password with Argon2id. A hash is a PHC string such as
/// <c>$argon2id$v=19$m=19456,t=2,p=1$salt$hash</c>, so it carries its own cost and salt.
/// </summary>
public sealed class PasswordHasher(Argon2Cost cost)
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int MaxMemoryKib = 1024 * 1024;
    private const int MaxIterations = 16;
    private const int MaxParallelism = 16;

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Compute(password, salt, cost, HashBytes);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"$argon2id$v=19$m={cost.MemoryKib},t={cost.Iterations},p={cost.Parallelism}${Encode(salt)}${Encode(hash)}");
    }

    /// <summary>Returns true when the password matches the hash. A malformed hash never matches.</summary>
    public static bool Verify(string password, string encodedHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length == 0 || !TryParse(encodedHash, out var storedCost, out var salt, out var expected))
        {
            return false;
        }

        var actual = Compute(password, salt, storedCost, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Compute(string password, byte[] salt, Argon2Cost cost, int length)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = cost.MemoryKib,
            Iterations = cost.Iterations,
            DegreeOfParallelism = cost.Parallelism,
        };
        return argon.GetBytes(length);
    }

    private static bool TryParse(string? encoded, out Argon2Cost cost, out byte[] salt, out byte[] hash)
    {
        cost = Argon2Cost.Default;
        salt = [];
        hash = [];

        // "$argon2id$v=19$m=..,t=..,p=..$salt$hash" splits into an empty part and five more.
        var parts = encoded?.Split('$');
        if (parts is not { Length: 6 } || parts[1] != "argon2id" || parts[2] != "v=19")
        {
            return false;
        }

        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in parts[3].Split(','))
        {
            var sides = pair.Split('=');
            if (sides.Length != 2
                || !int.TryParse(sides[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            numbers[sides[0]] = number;
        }

        if (!numbers.TryGetValue("m", out var memory) || memory is < 8 or > MaxMemoryKib
            || !numbers.TryGetValue("t", out var iterations) || iterations is < 1 or > MaxIterations
            || !numbers.TryGetValue("p", out var parallelism) || parallelism is < 1 or > MaxParallelism)
        {
            return false;
        }

        try
        {
            salt = Decode(parts[4]);
            hash = Decode(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length < 8 || hash.Length < 16)
        {
            return false;
        }

        cost = new Argon2Cost(memory, iterations, parallelism);
        return true;
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static byte[] Decode(string text) =>
        Convert.FromBase64String(text.PadRight(text.Length + ((4 - (text.Length % 4)) % 4), '='));
}
