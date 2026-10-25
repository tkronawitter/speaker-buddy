using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]

namespace SpeakerBuddy.App;

/// <summary>
/// The shell around <see cref="CommandLine"/> and <see cref="ConsoleModes"/>: console, serial port,
/// Ctrl+C and the tray message loop, the parts that need a real Windows session.
/// </summary>
internal static class Program
{
    private const int AttachParentProcess = -1;

    [STAThread]
    private static int Main(string[] args)
    {
        var command = CommandLine.Parse(args);

        // The program is a Windows (tray) application; console modes borrow the caller's console.
        if (command.ConsoleRequested && !Console.IsOutputRedirected)
        {
            AttachConsole(AttachParentProcess);
        }

        switch (command.Mode)
        {
            case Mode.Help:
                Console.WriteLine(CommandLine.Usage);
                return 0;
            case Mode.Invalid:
                if (!command.ConsoleRequested)
                {
                    AttachConsole(AttachParentProcess);
                }

                Console.Error.WriteLine(CommandLine.Usage);
                return 2;
            case Mode.Check:
                return ConsoleModes.Check(command.DeckPath!, command.Length, Console.Out, Console.Error);
            case Mode.Demo:
                return Demo(command.Port);
        }

        var log = new FileLog();
        LogUnhandledExceptions(log);
        log.DeleteOldFiles();
        ApplicationConfiguration.Initialize();
        using var runner = new TalkRunner(command.Length, command.Port, log);
        Application.Run(new TrayContext(runner, log));
        return 0;
    }

    // No dialog may ever cover the slides (A-03): an exception on the UI thread is logged and the
    // message loop goes on instead of WinForms showing its ThreadExceptionDialog. An exception on
    // another thread still ends the process, but leaves its reason in the log.
    private static void LogUnhandledExceptions(FileLog log)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            log.Write($"unhandled on the tray thread, ignored: {e.Exception.GetType().Name} {e.Exception.Message}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            log.Write($"unhandled, the program ends: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            log.Write($"unobserved task exception, ignored: {e.Exception.GetType().Name} {e.Exception.Message}");
            e.SetObserved();
        };
    }

    private static int Demo(string? fixedPort)
    {
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Cancel();
        };

        using var link = new SerialLink(() => fixedPort ?? WindowsPortDiscovery.FindTimerPort(), Console.Error.WriteLine);
        return ConsoleModes.Demo(link, Console.Out, stop.Token, () => DateTime.UtcNow,
            () => stop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1)));
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
}
