using System.Text.RegularExpressions;

namespace SpeakerTimer.Core;

/// <summary>
/// Reads time marks from speaker notes (requirement L-01). A mark is a line consisting of exactly
/// <c>## m:ss</c>: minutes 0 to 99 with one or two digits, seconds 00 to 59, nothing else on the line
/// except trailing spaces or tabs.
/// </summary>
public static partial class MarkParser
{
    // PowerPoint separates paragraphs with \r and line breaks within a paragraph with \v.
    private static readonly char[] LineSeparators = ['\r', '\n', '\v'];

    // Trailing whitespace is invisible in the notes pane, so it does not invalidate a mark.
    private static readonly char[] TrailingWhitespace = [' ', '\t'];

    [GeneratedRegex("^## ([0-9]{1,2}):([0-5][0-9])$", RegexOptions.CultureInvariant)]
    private static partial Regex MarkLine();

    // Looks like an attempted mark: hashes, then a time-like "digits:digit" somewhere after them.
    [GeneratedRegex(@"^\s*#{1,3}\s*[0-9]{1,3}\s*:\s*[0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex NearMissLine();

    /// <summary>Parses one line. Returns false unless the whole line, without trailing whitespace, is a mark.</summary>
    public static bool TryParseLine(string line, out int seconds)
    {
        line = line.TrimEnd(TrailingWhitespace);
        var match = MarkLine().Match(line);
        if (!match.Success || match.Length != line.Length)
        {
            seconds = 0;
            return false;
        }

        seconds = (int.Parse(match.Groups[1].ValueSpan) * 60) + int.Parse(match.Groups[2].ValueSpan);
        return true;
    }

    /// <summary>
    /// True for a line that looks like a mark but is not one, such as <c>## 10:75</c>,
    /// <c>## 10:00 Demo</c>, <c>## 1:05:00</c> or <c>##10:00</c> (requirement L-09).
    /// </summary>
    public static bool IsNearMiss(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return NearMissLine().IsMatch(line) && !TryParseLine(line, out _);
    }

    /// <summary>All lines of the notes that look like marks but are not (L-09).</summary>
    public static IReadOnlyList<string> NearMisses(string? notes) =>
        string.IsNullOrEmpty(notes) ? [] : notes.Split(LineSeparators).Where(IsNearMiss).ToList();

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
