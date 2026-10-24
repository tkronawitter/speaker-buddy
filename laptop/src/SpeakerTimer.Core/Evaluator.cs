namespace SpeakerTimer.Core;

/// <summary>Colour shown on the timer.</summary>
public enum EvaluationColor
{
    Neutral,
    Green,
    Red,
    Purple,
}

/// <summary>The rule that decided an evaluation, named after the requirement IDs.</summary>
public enum EvaluationRule
{
    /// <summary>The deck has no marks, nothing is evaluated.</summary>
    NoMarks,

    /// <summary>B-01: no mark reached yet.</summary>
    BeforeFirstMark,

    /// <summary>B-02: mark reached within the tolerance, green.</summary>
    OnPlan,

    /// <summary>B-03: mark reached more than the tolerance late, purple ("talk faster").</summary>
    Behind,

    /// <summary>B-04: mark reached more than the tolerance early and still ahead, red ("talk slower").</summary>
    Ahead,

    /// <summary>B-05: no new mark, the colour of the last evaluation holds.</summary>
    Hold,

    /// <summary>B-06: the next mark is overdue by more than the tolerance, purple.</summary>
    LateForNextMark,
}

/// <summary>Result of one evaluation step.</summary>
/// <param name="Color">Colour to show.</param>
/// <param name="Deviation">
/// <c>t - m</c> in seconds at the arrival on the last reached mark: positive is behind, negative
/// ahead; null before the first mark.
/// </param>
/// <param name="Rule">The rule that decided the colour.</param>
public sealed record Evaluation(EvaluationColor Color, int? Deviation, EvaluationRule Rule);

/// <summary>
/// Evaluates the talk against its marks (rules B-01 to B-06). Fed with the elapsed time and the
/// current slide index once per tick. Precedence: B-06, then B-02 to B-04 on arrival, then B-05,
/// then B-01.
/// </summary>
public sealed class Evaluator
{
    private readonly IReadOnlyList<Mark> _marks;
    private readonly int _tolerance;
    private int _reachedCount;
    private Mark? _lastMark;
    private int _lastDeviation;
    private EvaluationColor _lastColor = EvaluationColor.Neutral;

    public Evaluator(MarkSet marks, int toleranceSeconds)
    {
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentOutOfRangeException.ThrowIfNegative(toleranceSeconds);

        _marks = marks.Marks;
        _tolerance = toleranceSeconds;
    }

    /// <param name="t">Elapsed talk time in seconds, as counted by the timer.</param>
    /// <param name="slideIndex">Current slide index in the deck (not the show position).</param>
    public Evaluation Update(int t, int slideIndex)
    {
        if (_marks.Count == 0)
        {
            return new Evaluation(EvaluationColor.Neutral, null, EvaluationRule.NoMarks);
        }

        // A mark is reached the first time the slide index is at or past its slide; jumping past
        // several marks reaches all of them, and only the last one decides the colour.
        var arrived = false;
        while (_reachedCount < _marks.Count && slideIndex >= _marks[_reachedCount].SlideIndex)
        {
            _lastMark = _marks[_reachedCount++];
            _lastDeviation = t - _lastMark.Seconds;
            _lastColor = ClassifyArrival(_lastDeviation);
            arrived = true;
        }

        int? deviation = _lastMark is null ? null : _lastDeviation;

        if (_reachedCount < _marks.Count && t > _marks[_reachedCount].Seconds + _tolerance)
        {
            return new Evaluation(EvaluationColor.Purple, deviation, EvaluationRule.LateForNextMark);
        }

        if (_lastMark is null)
        {
            return new Evaluation(EvaluationColor.Neutral, null, EvaluationRule.BeforeFirstMark);
        }

        if (arrived)
        {
            return new Evaluation(_lastColor, deviation, RuleForArrival(_lastColor));
        }

        // B-04: red only while still ahead; staying on the slide turns it green.
        if (_lastColor == EvaluationColor.Red)
        {
            if (t < _lastMark.Seconds - _tolerance)
            {
                return new Evaluation(EvaluationColor.Red, deviation, EvaluationRule.Ahead);
            }

            _lastColor = EvaluationColor.Green;
        }

        return new Evaluation(_lastColor, deviation, EvaluationRule.Hold);
    }

    // Behind means "talk faster" (purple), ahead means "talk slower" (red).
    private EvaluationColor ClassifyArrival(int deviation) =>
        deviation > _tolerance ? EvaluationColor.Purple
        : deviation < -_tolerance ? EvaluationColor.Red
        : EvaluationColor.Green;

    private static EvaluationRule RuleForArrival(EvaluationColor color) => color switch
    {
        EvaluationColor.Purple => EvaluationRule.Behind,
        EvaluationColor.Red => EvaluationRule.Ahead,
        _ => EvaluationRule.OnPlan,
    };
}
