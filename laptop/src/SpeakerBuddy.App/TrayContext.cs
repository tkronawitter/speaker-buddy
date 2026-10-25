using System.Diagnostics;
using System.Runtime.Versioning;

namespace SpeakerBuddy.App;

/// <summary>
/// Tray-only shell (plan step 37, A-03): a notification-area icon with "Open log" and "Stop and
/// release port". No windows, balloons or toasts. The tooltip and the icon (logo in colour or grey)
/// show whether the timer is connected.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class TrayContext : ApplicationContext
{
    private readonly TalkRunner _runner;
    private readonly Action<string> _log;
    private readonly Func<string> _logFile;
    private readonly Action<string> _openFile;
    private readonly NotifyIcon _icon;
    private readonly Icon _connectedIcon = LoadIcon("speaker-buddy.ico");
    private readonly Icon _waitingIcon = LoadIcon("speaker-buddy-waiting.ico");
    private readonly System.Windows.Forms.Timer _status;

    public TrayContext(TalkRunner runner, FileLog log)
        : this(runner,
               log is null ? throw new ArgumentNullException(nameof(log)) : log.Write,
               () => log.CurrentFile,
               path => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }))
    {
    }

    /// <param name="logFile">Returns the path of today's log file.</param>
    /// <param name="openFile">Opens a file in its associated program; the tests record the path.</param>
    internal TrayContext(TalkRunner runner, Action<string> log, Func<string> logFile, Action<string> openFile)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _logFile = logFile ?? throw new ArgumentNullException(nameof(logFile));
        _openFile = openFile ?? throw new ArgumentNullException(nameof(openFile));

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open log", null, (_, _) => OpenLog());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Stop and release port", null, (_, _) => Stop());

        _icon = new NotifyIcon
        {
            Icon = _waitingIcon,
            Text = "Speaker Buddy",
            ContextMenuStrip = menu,
            Visible = true,
        };

        _status = new System.Windows.Forms.Timer { Interval = 1000 };
        _status.Tick += (_, _) => ShowStatus();
        _status.Start();

        _runner.Start();
    }

    // The icon in the size the notification area uses at the current DPI
    internal static Icon LoadIcon(string name)
    {
        using var stream = typeof(TrayContext).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"embedded icon {name} is missing");
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    /// <summary>The notification-area icon with its menu, tooltip and visibility; for the tests.</summary>
    internal NotifyIcon NotifyIcon => _icon;

    /// <summary>True while the icon shows the logo in colour (timer connected).</summary>
    internal bool ShowsConnected => ReferenceEquals(_icon.Icon, _connectedIcon);

    // Runs once per second on the status timer; the tests call it directly
    internal void ShowStatus()
    {
        var icon = _runner.Port is null ? _waitingIcon : _connectedIcon;
        if (!ReferenceEquals(_icon.Icon, icon))
        {
            _icon.Icon = icon;
        }

        _icon.Text = Tooltip(_runner.Port, _runner.Firmware);
    }

    // NotifyIcon refuses texts over 127 characters; the versions are at most 40 each
    internal static string Tooltip(string? port, string? firmware)
    {
        var text = port is null
            ? $"Speaker Buddy {AppVersion.Current}: waiting for the timer"
            : $"Speaker Buddy {AppVersion.Current}: connected on {port}"
              + (firmware is null ? string.Empty : $", timer {firmware}");
        return text.Length <= 127 ? text : text[..127];
    }

    private void OpenLog()
    {
        _log("log opened from the tray");
        try
        {
            _openFile(_logFile());
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            _log($"opening the log failed: {e.Message}");
        }
    }

    private void Stop()
    {
        _status.Stop();
        _runner.Dispose();
        _icon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _status.Dispose();
            _icon.Dispose();
            _connectedIcon.Dispose();
            _waitingIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
