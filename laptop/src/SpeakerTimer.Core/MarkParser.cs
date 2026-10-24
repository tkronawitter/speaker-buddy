using System.Text.RegularExpressions;

namespace SpeakerTimer.Core;

/// <summary>
/// Reads time marks from speaker notes (requirement L-01). A mark is a line consisting of exactly
/// <c>## m:ss</c>: minutes 0 to 99 with one or two digits, seconds 00 to 59, nothing else on the line.
/// </summary>
public static partial class MarkParser
{
    // PowerPoint separates paragraphs with \r and line breaks within a paragraph with \v.
    private static readonly char[] LineSeparators = ['\r', '\n', '\v'];

    [GeneratedRegex("^## ([0-9]{1,2}):([0-5][0-9])$", RegexOptions.CultureInvariant)]
    private static partial Regex MarkLine();

    /// <summary>Parses one line. Returns false unless the whole line is a mark.</summary>
    public static bool TryParseLine(string line, out int seconds)
    {
        var match = MarkLine().Match(line);
        if (!match.Success || match.Length != line.Length)
        {
            seconds = 0;
            return false;
        }

        seconds = (int.Parse(match.Groups[1].ValueSpan) * 60) + int.Parse(match.Groups[2].ValueSpan);
        return true;
    }

    /// <summary>Returns the first mark in the notes of one slide, or null if there is none.</summary>
    public static int? ParseNotes(string? notes)
    {
        if (string.IsNullOrEmpty(notes))
        {
            return null;
        }

        foreach (var line in notes.Split(LineSeparators))
        {
            if (TryParseLine(line, out var seconds))
            {
                return seconds;
            }
        }

        return null;
    }
}
