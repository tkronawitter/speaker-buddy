using System.IO.Ports;
using System.Runtime.Versioning;

namespace SpeakerBuddy.App;

/// <summary>
/// The open serial port as <see cref="SerialLink"/> uses it. Lets the tests replace the board with
/// a fake port; the program always uses <see cref="WindowsSerialDevice"/>.
/// </summary>
internal interface ISerialDevice : IDisposable
{
    bool IsOpen { get; }

    /// <summary>Reads what arrived; throws <see cref="TimeoutException"/> when nothing did in time.</summary>
    int Read(byte[] buffer, int offset, int count);

    void Write(string text);

    void Close();
}

/// <summary>A real COM port, opened the way the board needs it (requirement L-06).</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsSerialDevice : ISerialDevice
{
    private const int BaudRate = 115200;
    private const int IoTimeoutMs = 500;

    // While no program reads the port the timer keeps up to 1 KB of old status lines queued; they
    // arrive within milliseconds of the open and are dropped, so the first t the laptop sees is live.
    private static readonly TimeSpan BacklogDrainTime = TimeSpan.FromMilliseconds(300);

    private readonly SerialPort _port;

    private WindowsSerialDevice(SerialPort port)
    {
        _port = port;
    }

    public bool IsOpen => _port.IsOpen;

    /// <summary>
    /// Opens the port with DTR and RTS off, so the board does not reset, and drops the backlog.
    /// Throws <see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>,
    /// <see cref="ArgumentException"/> or <see cref="InvalidOperationException"/> when it cannot.
    /// </summary>
    public static ISerialDevice Open(string portName)
    {
        SafeLineState.TurnOff(portName);

        var port = new SerialPort(portName, BaudRate)
        {
            DtrEnable = false,
            RtsEnable = false,
            Handshake = Handshake.None,
            ReadTimeout = IoTimeoutMs,
            WriteTimeout = IoTimeoutMs,
        };
        try
        {
            port.Open();
            Thread.Sleep(BacklogDrainTime);
            port.DiscardInBuffer();
        }
        catch
        {
            port.Dispose();
            throw;
        }

        return new WindowsSerialDevice(port);
    }

    public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);

    public void Write(string text) => _port.Write(text);

    public void Close() => _port.Close();

    public void Dispose() => _port.Dispose();
}
