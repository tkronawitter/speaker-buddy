using System.Globalization;

namespace SpeakerBuddy.Core;

/// <summary>Where the talk length <c>L</c> came from.</summary>
public enum LengthSource
{
    /// <summary>No source gave a length.</summary>
    Unknown,

    /// <summary>The <c>--length</c> start argument.</summary>
    Argument,

    /// <summary>A <c>## session length</c> line in the deck.</summary>
    SessionLength,

    /// <summary>The highest mark, at least 5:00 (fallback).</summary>
    HighestMark,
}

/// <summary>The talk length that counts and where it came from.</summary>
/// <param name="Seconds">The length, or null if unknown.</param>
/// <param name="Source">Which rule gave it.</param>
/// <param name="SlideIndex">The slide of the session length or the highest mark; null otherwise.</param>
public sealed record ResolvedLength(int? Seconds, LengthSource Source, int? SlideIndex);

/// <summary>Talk length <c>L</c> and tolerance <c>tol</c> (requirement L-07).</summary>
public static class TalkLength
{
    /// <summary>The highest mark counts as <c>L</c> only from this value on.</summary>
    public const int MinimumHighestMarkSeconds = 5 * 60;

    /// <summary>Lower bound of the tolerance, also used when <c>L</c> is unknown.</summary>
    public const int MinimumToleranceSeconds = 45;

    /// <summary>
    /// <c>L</c> from the start argument if given, else from the deck's <c>## session length</c>,
    /// else the highest mark if it is at least 5:00, else null.
    /// </summary>
    public static int? Resolve(int? argumentSeconds, MarkSet marks) => ResolveWithSource(argumentSeconds, marks).Seconds;

    /// <summary><see cref="Resolve"/> with the rule and slide that gave the length.</summary>
    public static ResolvedLength ResolveWithSource(int? argumentSeconds, MarkSet marks)
    {
        ArgumentNullException.ThrowIfNull(marks);

        if (argumentSeconds is int argument)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(argument, nameof(argumentSeconds));
            return new ResolvedLength(argument, LengthSource.Argument, null);
        }

        if (marks.SessionLength is { } session)
        {
            return new ResolvedLength(session.Seconds, LengthSource.SessionLength, session.SlideIndex);
        }

        return marks.HighestSeconds is int highest && highest >= MinimumHighestMarkSeconds
            ? new ResolvedLength(highest, LengthSource.HighestMark, marks.Marks[^1].SlideIndex)
            : new ResolvedLength(null, LengthSource.Unknown, null);
    }

    /// <summary>
    /// Parses the start argument for <c>L</c>: whole minutes (<c>45</c>) or <c>m:ss</c>
    /// (<c>44:30</c>). Returns false for anything else or a length of zero.
    /// </summary>
    public static bool TryParseArgument(string? text, out int seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Trim().Split(':');
        var invariant = CultureInfo.InvariantCulture;
        const NumberStyles digits = NumberStyles.None;
        if (parts.Length == 1 && int.TryParse(parts[0], digits, invariant, out var minutes) && minutes is > 0 and <= 999)
        {
            seconds = minutes * 60;
            return true;
        }

        if (parts.Length == 2
            && int.TryParse(parts[0], digits, invariant, out minutes) && minutes <= 999
            && parts[1].Length == 2
            && int.TryParse(parts[1], digits, invariant, out var secs) && secs < 60
            && minutes * 60 + secs > 0)
        {
            seconds = minutes * 60 + secs;
            return true;
        }

        return false;
    }

    /// <summary><c>tol = max(L / 45, 0:45)</c> in whole seconds; 0:45 when <c>L</c> is unknown.</summary>
    public static int Tolerance(int? lengthSeconds) =>
        lengthSeconds is int length ? Math.Max(length / 45, MinimumToleranceSeconds) : MinimumToleranceSeconds;
}
