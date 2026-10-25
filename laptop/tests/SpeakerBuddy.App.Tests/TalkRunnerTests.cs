using SpeakerBuddy.Core;

namespace SpeakerBuddy.App.Tests;

public class TalkRunnerTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly StateLine Neutral = new(ColorScheme.Default.Neutral, null);

    private static readonly ShowState Show = new(@"C:\talks\talk.pptx", 1,
        [new SlideNotes(1, false, null), new SlideNotes(2, false, null)]);

    private readonly MemoryLog _log = new();
    private readonly FakeTimerLink _link = new();
    private readonly Queue<Func<PollResult>> _polls = new();

    // The fake PowerPoint source: each tick takes the next queued poll, the last one repeats
    private PollResult Poll()
    {
        var next = _polls.Count > 1 ? _polls.Dequeue() : _polls.Peek();
        return next();
    }

    private TalkRunner Runner(int? length = 600, params Func<PollResult>[] polls)
    {
        foreach (var poll in polls.Length == 0 ? [NoShow] : polls)
        {
            _polls.Enqueue(poll);
        }

        return new TalkRunner(length, _log.Write, poll: Poll, link: _link);
    }

    private static PollResult NoShow() => new(true, null);

    private static PollResult Running() => new(true, Show);

    private static PollResult Busy() => new(false, null);

    [Fact]
    public void Connected_sends_the_length_and_shows_the_port()
    {
        using var runner = Runner(600);

        _link.RaiseConnected("COM7");

        Assert.Equal("COM7", runner.Port);
        Assert.Equal([new LengthLine(600)], _link.Sent);
        Assert.Contains("timer connected on COM7", _log.Lines);
    }

    [Fact]
    public void Without_a_show_a_neutral_state_goes_out_once_per_second()
    {
        using var runner = Runner();

        runner.Tick(T0);
        runner.Tick(T0.AddMilliseconds(250));
        runner.Tick(T0.AddMilliseconds(999));
        runner.Tick(T0.AddSeconds(1));

        Assert.Equal([Neutral, Neutral], _link.Sent);
    }

    [Fact]
    public void A_busy_powerpoint_keeps_the_last_show_instead_of_ending_it()
    {
        using var runner = Runner(600, Running, Busy);

        runner.Tick(T0);
        runner.Tick(T0.AddSeconds(1));
        runner.Tick(T0.AddSeconds(2));

        Assert.DoesNotContain(_link.Sent, line => line is EndedLine);
        Assert.Equal(3, _link.Sent.Count(line => line is StateLine));
    }

    [Fact]
    public void The_end_of_the_show_sends_the_ended_line()
    {
        using var runner = Runner(600, Running, NoShow);

        runner.Tick(T0);
        runner.Tick(T0.AddMilliseconds(250));

        Assert.Single(_link.Sent, line => line is EndedLine);
    }

    [Fact]
    public void A_status_from_the_timer_reaches_the_evaluation()
    {
        using var runner = Runner(600, Running);

        _link.RaiseStatus(new TimerStatusLine(30, true));
        runner.Tick(T0);

        Assert.Contains(_log.Lines, line => line.StartsWith("t=30 ", StringComparison.Ordinal));
        Assert.IsType<StateLine>(_link.Sent[^1]);
    }

    [Fact]
    public void A_throwing_poll_is_logged_and_the_next_tick_runs()
    {
        using var runner = Runner(600, () => throw new InvalidOperationException("COM gone"), NoShow);

        runner.Tick(T0);
        runner.Tick(T0.AddSeconds(1));

        Assert.Contains("tick failed and was skipped: InvalidOperationException COM gone", _log.Lines);
        Assert.Equal([Neutral], _link.Sent);
    }

    [Fact]
    public void Firmware_and_disconnect_update_what_the_tray_shows()
    {
        using var runner = Runner();
        _link.RaiseConnected("COM7");

        _link.RaiseFirmware(new FirmwareLine("v0.1.0"));
        _link.RaiseFirmware(new FirmwareLine("v0.1.0"));

        Assert.Equal("v0.1.0", runner.Firmware);
        Assert.Single(_log.Lines, line => line == "timer firmware v0.1.0");

        _link.RaiseDisconnected();

        Assert.Null(runner.Port);
        Assert.Null(runner.Firmware);
        Assert.Contains("timer disconnected", _log.Lines);
    }

    [Fact]
    public void A_reconnected_timer_gets_the_length_and_the_last_state_at_once()
    {
        using var runner = Runner(600, Running);
        _link.RaiseConnected("COM7");
        _link.RaiseStatus(new TimerStatusLine(30, true));
        runner.Tick(T0);
        var lastState = _link.Sent.OfType<StateLine>().Last();
        _link.RaiseDisconnected();
        _link.Clear();

        _link.RaiseConnected("COM7");

        Assert.Equal([new LengthLine(600), lastState], _link.Sent);
    }

    [Fact]
    public void Dispose_stops_the_talk_thread_and_releases_the_link_once()
    {
        var runner = Runner();
        runner.Start();
        Wait.Until(() => _link.Sent.Count > 0, "the first tick of the talk thread");

        runner.Dispose();
        runner.Dispose();

        Assert.True(_link.Started);
        Assert.Equal(1, _link.DisposeCount);
        Assert.Equal("stopped, port released", _log.Lines[^1]);
    }
}
