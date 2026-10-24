using System.Diagnostics;
using System.Runtime.Versioning;

namespace SpeakerBuddy.App;

/// <summary>
/// Tray-only shell (plan step 37, A-03): a notification-area icon with "Open log" and "Stop and
/// release port". No windows, balloons or toasts. The tooltip shows whether the timer is connected.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class TrayContext : ApplicationContext
{
    private readonly TalkRunner _runner;
    private readonly FileLog _log;
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _status;

    public TrayContext(TalkRunner runner, FileLog log)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open log", null, (_, _) => OpenLog());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Stop and release port", null, (_, _) => Stop());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Speaker Buddy",
            ContextMenuStrip = menu,
            Visible = true,
        };

        _status = new System.Windows.Forms.Timer { Interval = 1000 };
        _status.Tick += (_, _) => _icon.Text = _runner.Port is string port
            ? $"Speaker Buddy: connected on {port}"
            : "Speaker Buddy: waiting for the timer";
        _status.Start();

        _runner.Start();
    }

    private void OpenLog()
    {
        _log.Write("log opened from the tray");
        try
        {
            Process.Start(new ProcessStartInfo(_log.CurrentFile) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            _log.Write($"opening the log failed: {e.Message}");
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
        }

        base.Dispose(disposing);
    }
}
