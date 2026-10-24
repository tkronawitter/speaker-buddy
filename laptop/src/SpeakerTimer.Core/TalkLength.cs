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

    /// <summary><c>tol = max(L / 45, 0:30)</c> in whole seconds; 0:30 when <c>L</c> is unknown.</summary>
    public static int Tolerance(int? lengthSeconds) =>
        lengthSeconds is int length ? Math.Max(length / 45, MinimumToleranceSeconds) : MinimumToleranceSeconds;
}
