using System.Globalization;

namespace SpeakerTimer.Core;

/// <summary>Talk length <c>L</c> and tolerance <c>tol</c> (requirement L-07).</summary>
public static class TalkLength
{
    /// <summary>The highest mark counts as <c>L</c> only from this value on.</summary>
    public const int MinimumHighestMarkSeconds = 5 * 60;

    /// <summary>Lower bound of the tolerance, also used when <c>L</c> is unknown.</summary>
    public const int MinimumToleranceSeconds = 30;

    /// <summary>
    /// <c>L</c> from the start argument if given, else the highest mark if it is at least 5:00,
    /// else null.
    /// </summary>
    public static int? Resolve(int? argumentSeconds, MarkSet marks)
    {
        ArgumentNullException.ThrowIfNull(marks);

        if (argumentSeconds is int argument)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(argument, nameof(argumentSeconds));
            return argument;
        }

        return marks.HighestSeconds is int highest && highest >= MinimumHighestMarkSeconds ? highest : null;
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

    /// <summary><c>tol = max(L / 45, 0:30)</c> in whole seconds; 0:30 when <c>L</c> is unknown.</summary>
    public static int Tolerance(int? lengthSeconds) =>
        lengthSeconds is int length ? Math.Max(length / 45, MinimumToleranceSeconds) : MinimumToleranceSeconds;
}
