using System.Runtime.Versioning;
using SpeakerBuddy.Core;

namespace SpeakerBuddy.App;

/// <summary>
/// Runs the talk (plan step 36): polls PowerPoint, feeds the timer's <c>t</c> and the slide into
/// <see cref="TalkController"/>, and sends the resulting lines over <see cref="SerialLink"/>.
/// Polls four times a second so a slide change reaches the timer quickly; the state line also
/// goes out once per second (L-05).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class TalkRunner : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan PeriodicInterval = TimeSpan.FromSeconds(1);

    private readonly FileLog _log;
    private readonly SerialLink _link;
    private readonly PowerPointSource _source;
    private readonly TalkController _controller;
    private readonly object _controllerLock = new();
    private readonly CancellationTokenSource _stop = new();
    private Thread? _thread;
    private TimerStatusLine? _timer;
    private string? _port;
    private EvaluationRule? _lastRule;

    public TalkRunner(int? lengthArgument, string? fixedPort, FileLog log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _controller = new TalkController(lengthArgument, ColorScheme.Default);
        _source = new PowerPointSource(_log.Write);
        _link = new SerialLink(() => fixedPort ?? WindowsPortDiscovery.FindTimerPort());
        _link.Connected += OnConnected;
        _link.Disconnected += OnDisconnected;
        _link.StatusReceived += status => _timer = status;
    }

    /// <summary>Port of the connected timer, or null.</summary>
    public string? Port => _port;

    public void Start()
    {
        _log.Write("started");
        _link.Start();
        _thread = new Thread(Run) { IsBackground = true, Name = "talk" };
        _thread.Start();
    }

    /// <summary>Stops polling and sending and releases the serial port (the kill switch).</summary>
    public void Dispose()
    {
        _stop.Cancel();
        _thread?.Join(TimeSpan.FromSeconds(2));
        _link.Dispose();
        _stop.Dispose();
        _log.Write("stopped, port released");
    }

    private void OnConnected(string port)
    {
        _port = port;
        _log.Write($"timer connected on {port}");
        IReadOnlyList<ProtocolLine> lines;
        lock (_controllerLock)
        {
            lines = _controller.OnConnected();
        }

        foreach (var line in lines)
        {
            _link.Send(line);
        }
    }

    private void OnDisconnected()
    {
        _port = null;
        _timer = null;
        _log.Write("timer disconnected");
    }

    private void Run()
    {
        var lastPeriodic = DateTime.MinValue;
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                var poll = _source.Poll();
                if (poll.Ok)
                {
                    var now = DateTime.UtcNow;
                    var periodic = now - lastPeriodic >= PeriodicInterval;
                    if (periodic)
                    {
                        lastPeriodic = now;
                    }

                    IReadOnlyList<ProtocolLine> lines;
                    lock (_controllerLock)
                    {
                        var marksBefore = _controller.Marks;
                        lines = _controller.Tick(poll.Show, _timer, periodic);
                        LogChanges(marksBefore);
                    }

                    foreach (var line in lines)
                    {
                        _link.Send(line);
                    }
                }
            }
            catch (Exception e)
            {
                // The timer keeps counting on its own; one failed tick must never end the program mid-talk
                _log.Write($"tick failed and was skipped: {e.GetType().Name} {e.Message}");
            }

            _stop.Token.WaitHandle.WaitOne(PollInterval);
        }
    }

    private void LogChanges(MarkSet? marksBefore)
    {
        var marks = _controller.Marks;
        if (marks is not null && !ReferenceEquals(marks, marksBefore))
        {
            _log.Write($"{marks.Marks.Count} marks, highest {marks.HighestSeconds?.ToString() ?? "none"} s");
            foreach (var warning in marks.Warnings)
            {
                _log.Write($"slide {warning.SlideIndex}: mark {warning.Seconds} s below {warning.PreviousSeconds} s, ignored");
            }
        }

        var evaluation = _controller.LastEvaluation;
        if (evaluation?.Rule != _lastRule)
        {
            _lastRule = evaluation?.Rule;
            if (evaluation is not null)
            {
                _log.Write($"t={_timer?.Seconds} {evaluation.Rule} {evaluation.Color} d={evaluation.Deviation}");
            }
        }
    }
}
