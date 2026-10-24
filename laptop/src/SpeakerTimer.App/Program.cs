using System.Runtime.Versioning;
using SpeakerTimer.App;
using SpeakerTimer.Core;

[assembly: SupportedOSPlatform("windows")]

const string Usage = """
    Usage: speaker-timer --demo [--port COMn]

      --demo        Sends a colour and deviation test pattern to the timer (plan step 32)
      --port COMn   Use this port instead of finding the timer by its USB ID

    The PowerPoint mode follows in a later build.
    """;

if (!args.Contains("--demo"))
{
    Console.Error.WriteLine(Usage);
    return 2;
}

string? fixedPort = null;
var portIndex = Array.IndexOf(args, "--port");
if (portIndex >= 0)
{
    if (portIndex + 1 >= args.Length)
    {
        Console.Error.WriteLine(Usage);
        return 2;
    }

    fixedPort = args[portIndex + 1];
}

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

Console.WriteLine("demo running, Ctrl+C to stop");
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
