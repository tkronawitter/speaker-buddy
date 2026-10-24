using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SpeakerBuddy.Core;

[assembly: SupportedOSPlatform("windows")]

namespace SpeakerBuddy.App;

internal static class Program
{
    private const string Usage = """
        Usage:
          speaker-buddy [--length <minutes|m:ss>] [--port COMn]
              Starts in the notification area, follows the PowerPoint slide show and drives the timer
          speaker-buddy --check <deck.pptx> [--length <minutes|m:ss>]
              Lists the time marks of a deck and reports near misses, hidden and decreasing marks
          speaker-buddy --demo [--port COMn]
              Sends a colour and deviation test pattern to the timer

        --length   Talk length L; without it the highest mark counts if it is at least 5:00
        --port     Use this port instead of finding the timer by its USB ID
        From PowerShell, append "| Out-Host" to --check and --demo so the prompt waits for the output.
        """;

    private const int AttachParentProcess = -1;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--check") || args.Contains("--demo") || args.Contains("--help"))
        {
            // The program is a Windows (tray) application; console modes borrow the caller's console.
            if (!Console.IsOutputRedirected)
            {
                AttachConsole(AttachParentProcess);
            }

            if (args.Contains("--help"))
            {
                Console.WriteLine(Usage);
                return 0;
            }

            if (!TryReadOptions(args, out var length, out var port))
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }

            return args.Contains("--check") ? Check(args, length) : Demo(port);
        }

        if (!TryReadOptions(args, out var talkLength, out var fixedPort) || args.Any(a => a.StartsWith("--", StringComparison.Ordinal) && a is not ("--length" or "--port")))
        {
            AttachConsole(AttachParentProcess);
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var log = new FileLog();
        ApplicationConfiguration.Initialize();
        using var runner = new TalkRunner(talkLength, fixedPort, log);
        Application.Run(new TrayContext(runner, log));
        return 0;
    }

    private static bool TryReadOptions(string[] args, out int? length, out string? port)
    {
        length = null;
        port = null;

        var lengthIndex = Array.IndexOf(args, "--length");
        if (lengthIndex >= 0)
        {
            if (lengthIndex + 1 >= args.Length || !TalkLength.TryParseArgument(args[lengthIndex + 1], out var seconds))
            {
                return false;
            }

            length = seconds;
        }

        var portIndex = Array.IndexOf(args, "--port");
        if (portIndex >= 0)
        {
            if (portIndex + 1 >= args.Length)
            {
                return false;
            }

            port = args[portIndex + 1];
        }

        return true;
    }

    private static int Check(string[] args, int? length)
    {
        var index = Array.IndexOf(args, "--check");
        if (index + 1 >= args.Length)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var path = args[index + 1];
        IReadOnlyList<SlideNotes> slides;
        try
        {
            slides = DeckReader.Read(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException
                                      or DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            Console.Error.WriteLine($"Cannot read {path}: {e.Message}");
            return 2;
        }

        var result = DeckCheck.Run(slides, length);
        Console.WriteLine(DeckCheck.Format(result));
        return result.ProblemCount == 0 ? 0 : 1;
    }

    private static int Demo(string? fixedPort)
    {
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Cancel();
        };

        using var link = new SerialLink(() => fixedPort ?? WindowsPortDiscovery.FindTimerPort());
        TimerStatusLine? lastStatus = null;

        link.Connected += port =>
        {
            Console.WriteLine($"connected on {port}");
            link.Send(new LengthLine(DemoSequence.LengthSeconds));
        };
        link.Disconnected += () => Console.WriteLine("disconnected, waiting for the timer");
        link.StatusReceived += status =>
        {
            if (status != lastStatus)
            {
                Console.WriteLine($"timer t={status.Seconds} run={status.Run}");
                lastStatus = status;
            }
        };

        string? lastFirmware = null;
        link.FirmwareReceived += firmware =>
        {
            if (firmware.Version != lastFirmware)
            {
                Console.WriteLine($"timer firmware {firmware.Version}");
                lastFirmware = firmware.Version;
            }
        };

        Console.WriteLine($"demo running (speaker-buddy {AppVersion.Current}), Ctrl+C to stop");
        link.Start();

        var started = DateTime.UtcNow;
        StateLine? lastSent = null;
        while (!stop.IsCancellationRequested)
        {
            var elapsed = (int)(DateTime.UtcNow - started).TotalSeconds;
            var state = DemoSequence.At(elapsed, ColorScheme.Default);
            if (link.Send(state) && state != lastSent)
            {
                Console.WriteLine($"sent colour {state.Color} deviation {state.Deviation?.ToString() ?? "none"}");
                lastSent = state;
            }

            stop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
        }

        link.Send(new EndedLine());
        return 0;
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
}
