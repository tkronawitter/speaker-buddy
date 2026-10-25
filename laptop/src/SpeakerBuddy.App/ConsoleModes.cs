using SpeakerBuddy.Core;

namespace SpeakerBuddy.App;

/// <summary>
/// The console modes --check and --demo, writing to the given writers instead of the console so
/// the tests run them without a console, a board or a real clock.
/// </summary>
internal static class ConsoleModes
{
    /// <summary>Exit code 0 without problems, 1 with problems, 2 when the deck cannot be read.</summary>
    public static int Check(string path, int? length, TextWriter output, TextWriter error)
    {
        IReadOnlyList<SlideNotes> slides;
        try
        {
            slides = DeckReader.Read(path);
        }
        catch (Exception e) when (DeckReader.IsUnreadable(e))
        {
            error.WriteLine($"Cannot read {path}: {e.Message}");
            return 2;
        }

        var result = DeckCheck.Run(slides, length);
        output.WriteLine(DeckCheck.Format(result));
        return result.ProblemCount == 0 ? 0 : 1;
    }

    /// <summary>
    /// Sends the demo pattern once per <paramref name="wait"/> until <paramref name="stop"/> is
    /// cancelled, then the end line. Returns 0.
    /// </summary>
    /// <param name="utcNow">The clock the pattern follows.</param>
    /// <param name="wait">Waits for the next step; returns early once <paramref name="stop"/> is cancelled.</param>
    public static int Demo(ITimerLink link, TextWriter output, CancellationToken stop, Func<DateTime> utcNow, Action wait)
    {
        TimerStatusLine? lastStatus = null;

        link.Connected += port =>
        {
            output.WriteLine($"connected on {port}");
            link.Send(new LengthLine(DemoSequence.LengthSeconds));
        };
        link.Disconnected += () => output.WriteLine("disconnected, waiting for the timer");
        link.StatusReceived += status =>
        {
            if (status != lastStatus)
            {
                output.WriteLine($"timer t={status.Seconds} run={status.Run}");
                lastStatus = status;
            }
        };

        string? lastFirmware = null;
        link.FirmwareReceived += firmware =>
        {
            if (firmware.Version != lastFirmware)
            {
                output.WriteLine($"timer firmware {firmware.Version}");
                lastFirmware = firmware.Version;
            }
        };

        output.WriteLine($"demo running (speaker-buddy {AppVersion.Current}), Ctrl+C to stop");
        link.Start();

        var started = utcNow();
        StateLine? lastSent = null;
        while (!stop.IsCancellationRequested)
        {
            var elapsed = (int)(utcNow() - started).TotalSeconds;
            var state = DemoSequence.At(elapsed, ColorScheme.Default);
            if (link.Send(state) && state != lastSent)
            {
                output.WriteLine($"sent colour {state.Color} deviation {state.Deviation?.ToString() ?? "none"}");
                lastSent = state;
            }

            wait();
        }

        link.Send(new EndedLine());
        return 0;
    }
}
