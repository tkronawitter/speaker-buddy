namespace SpeakerTimer.Core;

/// <summary>
/// Maps evaluation colours to the <c>#RRGGBB</c> values sent to the timer. The defaults live here
/// only, so a later settings file (O-05) replaces this one instance.
/// </summary>
public sealed record ColorScheme(string Neutral, string Green, string Red, string Purple)
{
    public static ColorScheme Default { get; } = new(
        Neutral: "#000000",
        Green: "#00A000",
        Red: "#C00000",
        Purple: "#8000A0");

    public string For(EvaluationColor color) => color switch
    {
        EvaluationColor.Neutral => Neutral,
        EvaluationColor.Green => Green,
        EvaluationColor.Red => Red,
        EvaluationColor.Purple => Purple,
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, null),
    };
}
