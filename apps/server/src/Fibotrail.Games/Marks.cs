using System.Globalization;
using System.Text;

namespace Fibotrail.Games;

/// <summary>
/// The marks of a game's tries, as stored text and as a share text. Stored, each try is a row of
/// letters (R, C, W, E for Earlier, L for Later) and rows are joined by commas, such as "CRWCR,RRRRR".
/// </summary>
public static class Marks
{
    public static string Encode(IEnumerable<IReadOnlyList<Mark>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return string.Join(',', rows.Select(row => new string([.. row.Select(Letter)])));
    }

    public static IReadOnlyList<IReadOnlyList<Mark>> Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(row => (IReadOnlyList<Mark>)[.. row.Select(FromLetter)])];
    }

    /// <summary>
    /// The text you paste to brag: the game, the puzzle number, the day's variant when there is one,
    /// the tries (X when not solved), and one line of squares per try. It holds no trip name, place,
    /// date, or photo.
    /// </summary>
    public static string ShareText(string gameName, int puzzleNumber, bool solved, IReadOnlyList<IReadOnlyList<Mark>> rows, int maxTries, string? variant = null)
    {
        ArgumentNullException.ThrowIfNull(gameName);
        ArgumentNullException.ThrowIfNull(rows);
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"Fibotrail {gameName} #{puzzleNumber} {(variant is null ? string.Empty : variant + " ")}{(solved ? rows.Count.ToString(CultureInfo.InvariantCulture) : "X")}/{maxTries}");
        foreach (var row in rows)
        {
            text.Append('\n').Append(string.Concat(row.Select(Square)));
        }

        return text.ToString();
    }

    private static char Letter(Mark mark) => mark switch
    {
        Mark.Right => 'R',
        Mark.Close => 'C',
        Mark.Earlier => 'E',
        Mark.Later => 'L',
        _ => 'W',
    };

    private static Mark FromLetter(char letter) => letter switch
    {
        'R' => Mark.Right,
        'C' => Mark.Close,
        'E' => Mark.Earlier,
        'L' => Mark.Later,
        _ => Mark.Wrong,
    };

    private static string Square(Mark mark) => mark switch
    {
        Mark.Right => "🟩",
        Mark.Close => "🟨",
        Mark.Earlier => "⬆️",
        Mark.Later => "⬇️",
        _ => "⬜",
    };
}
