using System.Runtime.Versioning;
using SpeakerBuddy.Core;

namespace SpeakerBuddy.App;

/// <summary>
/// Runs the talk (plan step 36): polls PowerPoint, feeds the timer's <c>t</c> and the slide into
/// <see cref="TalkController"/>, and sends the resulting lines over <see cref="SerialLink"/>.
/// Polls four times a second so a slide change reaches the timer quickly; a line goes out at least
/// once per second (L-05), also without a show and while PowerPoint is busy (keepalive).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class TalkRunner : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan PeriodicInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly Action<string> _log;
    private readonly ITimerLink _link;
    private readonly Func<PollResult> _poll;
    private readonly TalkController _controller;
    private readonly object _controllerLock = new();
    private readonly CancellationTokenSource _stop = new();
    private Thread? _thread;
    private TimerStatusLine? _timer;
    private string? _port;
    private string? _firmware;
    private EvaluationRule? _lastRule;
    private DateTime _lastPeriodic = DateTime.MinValue;
    private ShowState? _lastShow;
    private int _disposed;

    public TalkRunner(int? lengthArgument, string? fixedPort, FileLog log)
        : this(lengthArgument, log is null ? throw new ArgumentNullException(nameof(log)) : log.Write,
               fixedPort, null, null)
    {
    }

    /// <param name="poll">Reads the show; null polls PowerPoint.</param>
    /// <param name="link">The connection to the timer; null opens the serial link.</param>
    internal TalkRunner(int? lengthArgument, Action<string> log, string? fixedPort = null,
                        Func<PollResult>? poll = null, ITimerLink? link = null)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _controller = new TalkController(lengthArgument, ColorScheme.Default);
        _poll = poll ?? new PowerPointSource(_log).Poll;
        _link = link ?? new SerialLink(() => fixedPort ?? WindowsPortDiscovery.FindTimerPort(), _log);
        _link.Connected += OnConnected;
        _link.Disconnected += OnDisconnected;
        _link.StatusReceived += status => _timer = status;
        _link.FirmwareReceived += OnFirmware;
    }

    /// <summary>Port of the connected timer, or null.</summary>
    public string? Port => _port;

    /// <summary>Firmware version the connected timer reported, or null until it did.</summary>
    public string? Firmware => _firmware;

    public void Start()
    {
        _log($"started, speaker-buddy {AppVersion.Current}");
        _link.Start();
        _thread = new Thread(Run) { IsBackground = true, Name = "talk" };
        _thread.Start();
    }

    /// <summary>
    /// Stops polling and sending and releases the serial port (the kill switch). Safe to call more
    /// than once: the tray's Stop and the <c>using</c> in Main both dispose.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stop.Cancel();
        var ended = _thread?.Join(StopTimeout) ?? true;
        _link.Dispose();

        // A thread stuck in a PowerPoint call still waits on the token; it is a background thread
        // and dies with the process, so its token source is left to the finalizer instead
        if (ended)
        {
            _stop.Dispose();
        }

        _log(ended ? "stopped, port released" : "stopped, port released, talk thread did not end in time");
    }

    private void OnConnected(string port)
    {
        _port = port;
        _log($"timer connected on {port}");
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

    private void OnFirmware(FirmwareLine firmware)
    {
        if (firmware.Version != _firmware)
        {
            _firmware = firmware.Version;
            _log($"timer firmware {firmware.Version}");
        }
    }

    private void OnDisconnected()
    {
        _port = null;
        _timer = null;
        _firmware = null;
        lock (_controllerLock)
        {
            _controller.OnDisconnected();
        }

        _log("timer disconnected");
    }

    private void Run()
    {
        while (!_stop.IsCancellationRequested)
        {
            Tick(DateTime.UtcNow);
            _stop.Token.WaitHandle.WaitOne(PollInterval);
        }
    }

    /// <summary>One poll of the talk thread at <paramref name="now"/> (UTC); the tests call it directly.</summary>
    internal void Tick(DateTime now)
    {
        try
        {
            // A busy PowerPoint skips the poll, not the tick: the last good show keeps the
            // state line going once per second, so the timer never sees the link as lost
            var poll = _poll();
            if (poll.Ok)
            {
                _lastShow = poll.Show;
            }

            var periodic = now - _lastPeriodic >= PeriodicInterval;
            if (periodic)
            {
                _lastPeriodic = now;
            }

            IReadOnlyList<ProtocolLine> lines;
            lock (_controllerLock)
            {
                var marksBefore = _controller.Marks;
                lines = _controller.Tick(_lastShow, _timer, periodic);
                LogChanges(marksBefore);
            }

            foreach (var line in lines)
            {
                _link.Send(line);
            }
        }
        catch (Exception e)
        {
            // The timer keeps counting on its own; one failed tick must never end the program mid-talk
            _log($"tick failed and was skipped: {e.GetType().Name} {e.Message}");
        }
    }

    private void LogChanges(MarkSet? marksBefore)
    {
        var marks = _controller.Marks;
        if (marks is not null && !ReferenceEquals(marks, marksBefore))
        {
            _log($"{marks.Marks.Count} marks, highest {marks.HighestSeconds?.ToString() ?? "none"} s");
            foreach (var warning in marks.Warnings)
            {
                _log($"slide {warning.SlideIndex}: mark {warning.Seconds} s below {warning.PreviousSeconds} s, ignored");
            }

            if (_controller.Length is { } length)
            {
                _log($"talk length {length.Seconds?.ToString() ?? "unknown"} s from {length.Source}"
                           + (length.SlideIndex is int slide ? $" on slide {slide}" : string.Empty));
            }

            foreach (var extra in marks.ExtraSessionLengths)
            {
                _log($"slide {extra.SlideIndex}: second session length {extra.Seconds} s, ignored");
            }
        }

        var evaluation = _controller.LastEvaluation;
        if (evaluation?.Rule != _lastRule)
        {
            _lastRule = evaluation?.Rule;
            if (evaluation is not null)
            {
                _log($"t={_timer?.Seconds} {evaluation.Rule} {evaluation.Color} d={evaluation.Deviation}");
            }
        }
    }
}
