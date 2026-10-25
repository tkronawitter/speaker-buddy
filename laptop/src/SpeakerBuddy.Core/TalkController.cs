namespace SpeakerBuddy.Core;

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
    private bool _lengthDue;
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

    private StateLine NeutralState => new(_scheme.Neutral, null);

    /// <summary>Marks of the current show, for the log; null while no show runs.</summary>
    public MarkSet? Marks => _marks;

    /// <summary>The evaluation of the last tick, for the log; null if nothing was evaluated.</summary>
    public Evaluation? LastEvaluation { get; private set; }

    /// <summary>
    /// Lines to send right after the timer connected: <c>L</c>, as null when unknown so the timer
    /// drops an <c>L</c> left from an earlier talk, and the last state, so a reconnected timer is
    /// current at once.
    /// </summary>
    public IReadOnlyList<ProtocolLine> OnConnected()
    {
        var lines = new List<ProtocolLine> { new LengthLine(_length) };
        _sentLength = _length;
        _lengthDue = false;

        if (_lastState is not null)
        {
            lines.Add(_lastState);
        }

        return lines;
    }

    /// <summary>
    /// The timer is gone: forget its last <c>t</c>, so nothing measured before the cable was pulled
    /// counts against the first status after the reconnect.
    /// </summary>
    public void OnDisconnected()
    {
        _lastT = -1;
    }

    /// <summary>
    /// One tick. Returns the lines to send now: <c>L</c> when it changes, or as null at a show start
    /// that leaves it unknown, the end
    /// line when the show ends, and the state line when it changed or <paramref name="periodic"/>
    /// is true (once per second). Every periodic tick sends at least one line, so the timer only
    /// shows its disconnected symbol when the cable or the program is gone (keepalive): without a
    /// show, or before the timer's first status, that line is the neutral state.
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
            else if (periodic)
            {
                lines.Add(NeutralState);
            }

            return lines;
        }

        if (!string.Equals(show.PresentationName, _presentation, StringComparison.OrdinalIgnoreCase))
        {
            StartShow(show);
        }

        if (_lengthDue || _length != _sentLength)
        {
            lines.Add(new LengthLine(_length));
            _sentLength = _length;
            _lengthDue = false;
        }

        if (timer is null)
        {
            if (periodic)
            {
                lines.Add(_lastState ?? NeutralState);
            }

            return lines;
        }

        // A reset on the timer (long press: t == 0 and stopped) starts the evaluation over, once on
        // arrival at 0. A smaller t alone is no reset: a stale or backlog status must not wipe the talk.
        if (timer is { Seconds: 0, Run: false } && _lastT != 0)
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

        // An unknown L is sent as null at every show start, so no L of an earlier talk stays on the timer
        _lengthDue = _length is null;
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
        _length = _lengthArgument;
        _evaluator = null;
        _lastT = -1;
        _lastState = null;
        LastEvaluation = null;
    }
}
