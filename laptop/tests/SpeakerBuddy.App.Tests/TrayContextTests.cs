using SpeakerBuddy.Core;

namespace SpeakerBuddy.App.Tests;

public sealed class TrayContextTests : IDisposable
{
    [Fact]
    public void The_tooltip_says_waiting_without_a_timer()
    {
        Assert.Equal($"Speaker Buddy {AppVersion.Current}: waiting for the timer", TrayContext.Tooltip(null, null));
    }

    [Fact]
    public void The_tooltip_names_the_port_and_the_firmware_once_known()
    {
        Assert.Equal($"Speaker Buddy {AppVersion.Current}: connected on COM7", TrayContext.Tooltip("COM7", null));
        Assert.Equal($"Speaker Buddy {AppVersion.Current}: connected on COM7, timer v0.1.0",
            TrayContext.Tooltip("COM7", "v0.1.0"));
    }

    [Fact]
    public void The_tooltip_stays_within_the_127_characters_windows_accepts()
    {
        var text = TrayContext.Tooltip(new string('P', 100), new string('f', 40));

        Assert.Equal(127, text.Length);
    }

    [Theory]
    [InlineData("speaker-buddy.ico")]
    [InlineData("speaker-buddy-waiting.ico")]
    public void Both_tray_icons_are_embedded_in_the_notification_area_size(string name)
    {
        using var icon = TrayContext.LoadIcon(name);

        Assert.Equal(SystemInformation.SmallIconSize, icon.Size);
    }

    [Fact]
    public void A_missing_icon_fails_loudly()
    {
        Assert.Throws<InvalidOperationException>(() => TrayContext.LoadIcon("missing.ico"));
    }

    private const string LogFile = @"C:\logs\speaker-buddy-2026-01-01.log";

    private readonly MemoryLog _log = new();
    private readonly FakeTimerLink _link = new();
    private readonly List<string> _opened = [];
    private TalkRunner? _runner;

    // Program disposes the runner after the tray ends; here the test does, so no talk thread outlives it
    public void Dispose() => _runner?.Dispose();

    private TrayContext Tray(Action<string>? openFile = null)
    {
        _runner = new TalkRunner(600, _log.Write, poll: () => new PollResult(true, null), link: _link);
        return new TrayContext(_runner, _log.Write, () => LogFile, openFile ?? _opened.Add);
    }

    private static ToolStripItem Item(TrayContext tray, string text) =>
        tray.NotifyIcon.ContextMenuStrip!.Items.Cast<ToolStripItem>().Single(item => item.Text == text);

    [Fact]
    public void The_tray_starts_the_runner_and_shows_the_waiting_icon_with_the_menu()
    {
        Sta.Run(() =>
        {
            using var tray = Tray();

            Assert.True(_link.Started);
            Assert.True(tray.NotifyIcon.Visible);
            Assert.False(tray.ShowsConnected);
            Assert.Equal(
                ["Open log", string.Empty, "Stop and release port"],
                tray.NotifyIcon.ContextMenuStrip!.Items.Cast<ToolStripItem>().Select(item => item.Text ?? string.Empty));
            Assert.IsType<ToolStripSeparator>(tray.NotifyIcon.ContextMenuStrip.Items[1]);
        });
    }

    [Fact]
    public void The_status_follows_the_connection_of_the_timer()
    {
        Sta.Run(() =>
        {
            using var tray = Tray();

            _link.RaiseConnected("COM7");
            _link.RaiseFirmware(new FirmwareLine("v0.1.0"));
            tray.ShowStatus();

            Assert.True(tray.ShowsConnected);
            Assert.Equal(TrayContext.Tooltip("COM7", "v0.1.0"), tray.NotifyIcon.Text);

            _link.RaiseDisconnected();
            tray.ShowStatus();

            Assert.False(tray.ShowsConnected);
            Assert.Equal(TrayContext.Tooltip(null, null), tray.NotifyIcon.Text);
        });
    }

    [Fact]
    public void Open_log_opens_todays_log_file_and_notes_it_in_the_log()
    {
        Sta.Run(() =>
        {
            using var tray = Tray();

            Item(tray, "Open log").PerformClick();

            Assert.Equal([LogFile], _opened);
            Assert.Contains("log opened from the tray", _log.Lines);
        });
    }

    public static TheoryData<Exception> OpenFailures => new()
    {
        new System.ComponentModel.Win32Exception(1155, "No application is associated with the specified file"),
        new FileNotFoundException("The log file is gone"),
    };

    [Theory]
    [MemberData(nameof(OpenFailures))]
    public void A_log_that_cannot_be_opened_is_logged_instead(Exception failure)
    {
        Sta.Run(() =>
        {
            using var tray = Tray(_ => throw failure);

            Item(tray, "Open log").PerformClick();

            Assert.Equal($"opening the log failed: {failure.Message}", _log.Lines[^1]);
        });
    }

    [Fact]
    public void Stop_releases_the_port_hides_the_icon_and_ends_the_tray()
    {
        Sta.Run(() =>
        {
            using var tray = Tray();
            var ended = false;
            tray.ThreadExit += (_, _) => ended = true;

            Item(tray, "Stop and release port").PerformClick();

            Assert.Equal(1, _link.DisposeCount);
            Assert.Contains("stopped, port released", _log.Lines);
            Assert.False(tray.NotifyIcon.Visible);
            Assert.True(ended);
        });
    }
}
