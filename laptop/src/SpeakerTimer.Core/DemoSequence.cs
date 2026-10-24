namespace SpeakerTimer.Core;

/// <summary>
/// Test pattern for <c>--demo</c> (plan step 32): cycles neutral, green, purple and red with a
/// changing deviation, one step every <see cref="StepSeconds"/> seconds, so a board test can watch
/// every colour and the deviation line without PowerPoint.
/// </summary>
public static class DemoSequence
{
    public const int StepSeconds = 3;

    /// <summary>Talk length sent once after connecting, 45 minutes.</summary>
    public const int LengthSeconds = 45 * 60;

    private static readonly (EvaluationColor Color, int? Deviation)[] Steps =
    [
        (EvaluationColor.Neutral, null),
        (EvaluationColor.Green, 0),
        (EvaluationColor.Green, 45),
        (EvaluationColor.Purple, 90),
        (EvaluationColor.Purple, 125),
        (EvaluationColor.Red, -90),
        (EvaluationColor.Red, -615),
    ];

    /// <summary>Number of distinct steps before the pattern repeats.</summary>
    public static int Length => Steps.Length;

    /// <summary>The state line to show <paramref name="elapsedSeconds"/> after the demo started.</summary>
    public static StateLine At(int elapsedSeconds, ColorScheme scheme)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedSeconds);
        ArgumentNullException.ThrowIfNull(scheme);

        var (color, deviation) = Steps[elapsedSeconds / StepSeconds % Steps.Length];
        return new StateLine(scheme.For(color), deviation);
    }
}
