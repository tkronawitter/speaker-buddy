using System.Text.RegularExpressions;

namespace SpeakerBuddy.Core;

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

    // The planned talk length: "## session length m:ss", the words in any case, minutes as for --length.
    [GeneratedRegex("^## session length ([0-9]{1,3}):([0-5][0-9])$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SessionLengthLine();

    // Looks like an attempted session length: hashes, a word starting with "ses" or "len", then a
    // number, such as "## session lenght 60:00", "## length 60:00" or "## session length 60".
    [GeneratedRegex(@"^\s*#{1,3}[^0-9]*\b(ses|len)[^0-9]*[0-9]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SessionLengthNearMissLine();

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
    /// Parses one line as the planned talk length, <c>## session length m:ss</c>. It is no mark: it
    /// only sets <c>L</c>. Returns false unless the whole line, without trailing whitespace, is one
    /// with a length above zero.
    /// </summary>
    public static bool TryParseSessionLengthLine(string line, out int seconds)
    {
        line = line.TrimEnd(TrailingWhitespace);
        var match = SessionLengthLine().Match(line);
        seconds = match.Success
            ? (int.Parse(match.Groups[1].ValueSpan) * 60) + int.Parse(match.Groups[2].ValueSpan)
            : 0;
        return seconds > 0;
    }

    /// <summary>
    /// True for a line that looks like a mark but is not one, such as <c>## 10:75</c>,
    /// <c>## 10:00 Demo</c>, <c>## 1:05:00</c> or <c>##10:00</c> (requirement L-09), or like a
    /// session length but is not one, such as <c>## session lenght 60:00</c> or <c>## length 60:00</c>.
    /// </summary>
    public static bool IsNearMiss(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return (NearMissLine().IsMatch(line) && !TryParseLine(line, out _))
            || (SessionLengthNearMissLine().IsMatch(line) && !TryParseSessionLengthLine(line, out _));
    }

    /// <summary>All lines of the notes that look like marks or session lengths but are not (L-09).</summary>
    public static IReadOnlyList<string> NearMisses(string? notes) =>
        string.IsNullOrEmpty(notes) ? [] : notes.Split(LineSeparators).Where(IsNearMiss).ToList();

    /// <summary>The session lengths in the notes of one slide, in line order; usually none or one.</summary>
    public static IReadOnlyList<int> SessionLengths(string? notes)
    {
        if (string.IsNullOrEmpty(notes))
        {
            return [];
        }

        var lengths = new List<int>();
        foreach (var line in notes.Split(LineSeparators))
        {
            if (TryParseSessionLengthLine(line, out var seconds))
            {
                lengths.Add(seconds);
            }
        }

        return lengths;
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
