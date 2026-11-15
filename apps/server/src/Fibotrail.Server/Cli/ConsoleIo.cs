using System.Text;

namespace Fibotrail.Server.Cli;

/// <summary>The terminal, as the commands see it. A test supplies its own.</summary>
public interface IConsoleIo
{
    void WriteLine(string text);

    /// <summary>Asks for a line and doesn't show what is typed. Returns null at the end of input.</summary>
    string? ReadSecret(string prompt);
}

public sealed class SystemConsoleIo : IConsoleIo
{
    public void WriteLine(string text) => Console.Out.WriteLine(text);

    public string? ReadSecret(string prompt)
    {
        Console.Out.Write(prompt);
        if (Console.IsInputRedirected)
        {
            return Console.In.ReadLine();
        }

        var typed = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Out.WriteLine();
                return typed.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Length > 0)
                {
                    typed.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                typed.Append(key.KeyChar);
            }
        }
    }
}
