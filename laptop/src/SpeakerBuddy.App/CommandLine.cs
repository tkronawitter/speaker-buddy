using SpeakerBuddy.Core;

namespace SpeakerBuddy.App;

internal enum Mode
{
    /// <summary>The notification area program that follows the slide show.</summary>
    Tray,
    Check,
    Demo,
    Help,

    /// <summary>The arguments are wrong: the usage goes to standard error, exit code 2.</summary>
    Invalid,
}

/// <param name="Mode">What the program does.</param>
/// <param name="ConsoleRequested">True when --check, --demo or --help asked for console output.</param>
/// <param name="Length">Talk length from --length in seconds, or null.</param>
/// <param name="Port">Port from --port, or null to find the timer by its USB ID.</param>
/// <param name="DeckPath">The deck after --check, only in check mode.</param>
/// <param name="Rehearsal">True with --rehearsal in tray mode: write the rehearsal CSV.</param>
internal sealed record Command(Mode Mode, bool ConsoleRequested, int? Length, string? Port, string? DeckPath, bool Rehearsal = false);

/// <summary>Reads the command line of speaker-buddy.exe; no side effects, so it is tested on its own.</summary>
internal static class CommandLine
{
    public const string Usage = """
        Usage:
          speaker-buddy [--length <minutes|m:ss>] [--port COMn] [--rehearsal]
              Starts in the notification area, follows the PowerPoint slide show and drives the timer
          speaker-buddy --check <deck.pptx> [--length <minutes|m:ss>]
              Lists the time marks of a deck and reports near misses, hidden and decreasing marks
          speaker-buddy --demo [--port COMn]
              Sends a colour and deviation test pattern to the timer

        --length   Talk length L; without it a "## session length m:ss" line in the notes counts,
                   else the highest mark if it is at least 5:00
        --port     Use this port instead of finding the timer by its USB ID
        --rehearsal  Writes a CSV file per slide show next to the log: one row per slide change with
                   the mark, the elapsed time, the deviation and the colour
        From PowerShell, append "| Out-Host" to --check and --demo so the prompt waits for the output.
        """;

    /// <summary>
    /// --help wins over everything; --check over --demo. The console modes ignore options they do not
    /// know (--rehearsal among them), the tray mode rejects any other option starting with "--".
    /// </summary>
    public static Command Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var console = args.Contains("--check") || args.Contains("--demo") || args.Contains("--help");
        if (console)
        {
            if (args.Contains("--help"))
            {
                return new Command(Mode.Help, true, null, null, null);
            }

            if (!TryReadOptions(args, out var length, out var port))
            {
                return new Command(Mode.Invalid, true, null, null, null);
            }

            if (!args.Contains("--check"))
            {
                return new Command(Mode.Demo, true, length, port, null);
            }

            var index = Array.IndexOf(args, "--check");
            return index + 1 >= args.Length
                ? new Command(Mode.Invalid, true, null, null, null)
                : new Command(Mode.Check, true, length, port, args[index + 1]);
        }

        if (!TryReadOptions(args, out var talkLength, out var fixedPort)
            || args.Any(a => a.StartsWith("--", StringComparison.Ordinal) && a is not ("--length" or "--port" or "--rehearsal")))
        {
            return new Command(Mode.Invalid, false, null, null, null);
        }

        return new Command(Mode.Tray, false, talkLength, fixedPort, null, args.Contains("--rehearsal"));
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
}
