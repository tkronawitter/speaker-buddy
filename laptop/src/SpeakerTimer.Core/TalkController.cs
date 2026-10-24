namespace SpeakerTimer.Core;

/// <summary>
/// A running slide show as read from PowerPoint in one tick.
/// </summary>
/// <param name="PresentationName">Full name of the presentation the program locked onto.</param>
/// <param name="SlideIndex">Current slide index in the deck (not the show position).</param>
/// <param name="Slides">Notes and hidden flags of all slides, read when the show started.</param>
public sealed record ShowState(string PresentationName, int SlideIndex, IReadOnlyList<SlideNotes> Slides);

/// <summary>
/// Turns slide show state and timer status into the lines for the timer (requirements L-03 to
/// L-05, L-07). The timer owns <c>t</c>; without a status line from it nothing is evaluated.
/// Pure logic, so it is tested without PowerPoint or a serial port.
/// </summary>
public sealed class TalkController
{
    private readonly int? _lengthArgument;
    private readonly ColorScheme _scheme;

    private string? _presentation;
    private MarkSet? _marks;
    private int _tolerance = TalkLength.MinimumToleranceSeconds;
    private Evaluator? _evaluator;
    private int? _length;
    private int? _sentLength;
    private int _lastT = -1;
    private StateLine? _lastState;

    /// <param name="lengthArgument">Talk length from the start argument, or null to take it from the marks.</param>
    public TalkController(int? lengthArgument, ColorScheme scheme)
    {
        if (lengthArgument is int argument)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(argument, nameof(lengthArgument));
        }

        _lengthArgument = lengthArgument;
        _length = lengthArgument;
        _scheme = scheme ?? throw new ArgumentNullException(nameof(scheme));
    }

    /// <summary>Marks of the current show, for the log; null while no show runs.</summary>
    public MarkSet? Marks => _marks;

    /// <summary>The evaluation of the last tick, for the log; null if nothing was evaluated.</summary>
    public Evaluation? LastEvaluation { get; private set; }

    /// <summary>
    /// Lines to send right after the timer connected: <c>L</c> if known, and the last state, so a
    /// reconnected timer is current at once.
    /// </summary>
    public IReadOnlyList<ProtocolLine> OnConnected()
    {
        var lines = new List<ProtocolLine>();
        _sentLength = null;
        if (_length is int length)
        {
            lines.Add(new LengthLine(length));
            _sentLength = length;
        }

        if (_lastState is not null)
        {
            lines.Add(_lastState);
        }

        return lines;
    }

    /// <summary>
    /// One tick. Returns the lines to send now: <c>L</c> when it becomes known or changes, the end
    /// line when the show ends, and the state line when it changed or <paramref name="periodic"/>
    /// is true (once per second).
    /// </summary>
    /// <param name="show">The running show, or null when no show of the locked presentation runs.</param>
    /// <param name="timer">Last status line from the timer, or null if none arrived yet.</param>
    public IReadOnlyList<ProtocolLine> Tick(ShowState? show, TimerStatusLine? timer, bool periodic)
    {
        var lines = new List<ProtocolLine>();

        if (show is null)
        {
            if (_presentation is not null)
            {
                EndShow();
                lines.Add(new EndedLine());
            }

            return lines;
        }

        if (!string.Equals(show.PresentationName, _presentation, StringComparison.OrdinalIgnoreCase))
        {
            StartShow(show);
        }

        if (_length != _sentLength && _length is int length)
        {
            lines.Add(new LengthLine(length));
            _sentLength = length;
        }

        if (timer is null)
        {
            return lines;
        }

        // A reset on the timer (t went back) starts the evaluation over.
        if (timer.Seconds < _lastT)
        {
            _evaluator = new Evaluator(_marks!, _tolerance);
        }

        _lastT = timer.Seconds;
        LastEvaluation = _evaluator!.Update(timer.Seconds, show.SlideIndex);
        var state = new StateLine(_scheme.For(LastEvaluation.Color), LastEvaluation.Deviation);
        if (periodic || state != _lastState)
        {
            lines.Add(state);
            _lastState = state;
        }

        return lines;
    }

    private void StartShow(ShowState show)
    {
        _presentation = show.PresentationName;
        _marks = MarkSet.Build(show.Slides);
        _length = TalkLength.Resolve(_lengthArgument, _marks);
        _tolerance = TalkLength.Tolerance(_length);
        _evaluator = new Evaluator(_marks, _tolerance);
        _lastT = -1;
        _lastState = null;
        LastEvaluation = null;
    }

    private void EndShow()
    {
        _presentation = null;
        _marks = null;
        _evaluator = null;
        _lastT = -1;
        _lastState = null;
        LastEvaluation = null;
    }
}
