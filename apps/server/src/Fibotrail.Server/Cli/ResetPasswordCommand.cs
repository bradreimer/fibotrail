using Fibotrail.Server.Auth;

namespace Fibotrail.Server.Cli;

/// <summary>
/// <c>fibotrail reset-password</c>. It asks for the new password twice and never takes it as an
/// argument, so the password doesn't land in the shell history.
/// </summary>
public static class ResetPasswordCommand
{
    public const string Name = "reset-password";

    public static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        IConsoleIo console,
        AuthService auth,
        ServerSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (arguments.Count > 0)
        {
            console.WriteLine("This command takes no arguments. It asks for the new password, so the password stays out of your shell history.");
            return 2;
        }

        if (!File.Exists(settings.DatabasePath))
        {
            console.WriteLine($"No database at {settings.DatabasePath}. Start Fibotrail once, then run this command again.");
            return 1;
        }

        var password = console.ReadSecret("New password (at least 12 characters): ");
        var confirmation = console.ReadSecret("Type it again: ");
        var change = await auth.ResetPasswordAsync(password, confirmation, cancellationToken);
        if (change.Outcome != SetupOutcome.Done)
        {
            console.WriteLine($"Nothing changed. {change.Problem}");
            return 1;
        }

        console.WriteLine("Your password is changed. Every browser has to sign in again.");
        return 0;
    }
}
