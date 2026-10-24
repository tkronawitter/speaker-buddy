using System.IO.Ports;
using System.Runtime.Versioning;
using SpeakerBuddy.Core;

namespace SpeakerBuddy.App;

/// <summary>
/// Serial connection to the timer (requirements L-06, A-03): finds the port, opens it with DTR and
/// RTS off so the board does not reset, reads status lines, writes laptop lines, and reconnects
/// after the cable was pulled. Events are raised on the link's background thread.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SerialLink : IDisposable
{
    private const int BaudRate = 115200;
    private const int IoTimeoutMs = 500;
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    // While no program reads the port the timer keeps up to 1 KB of old status lines queued; they
    // arrive within milliseconds of the open and are dropped, so the first t the laptop sees is live.
    private static readonly TimeSpan BacklogDrainTime = TimeSpan.FromMilliseconds(300);

    private readonly Func<string?> _findPort;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _portLock = new();
    private SerialPort? _port;
    private Task? _loop;

    /// <param name="findPort">Returns the port to use, or null while no timer is attached.</param>
    public SerialLink(Func<string?> findPort)
    {
        _findPort = findPort ?? throw new ArgumentNullException(nameof(findPort));
    }

    /// <summary>Raised with the port name after the port was opened.</summary>
    public event Action<string>? Connected;

    /// <summary>Raised after an open port was lost or closed.</summary>
    public event Action? Disconnected;

    /// <summary>Raised for every valid status line from the timer.</summary>
    public event Action<TimerStatusLine>? StatusReceived;

    /// <summary>Raised for every valid firmware version line from the timer.</summary>
    public event Action<FirmwareLine>? FirmwareReceived;

    public void Start()
    {
        if (_loop is not null)
        {
            throw new InvalidOperationException("The link is already started.");
        }

        _loop = Task.Run(() => Run(_stop.Token));
    }

    /// <summary>Sends one line; returns false while no timer is connected or the write failed.</summary>
    public bool Send(ProtocolLine line)
    {
        var text = Protocol.Serialize(line) + "\n";
        lock (_portLock)
        {
            if (_port is not { IsOpen: true })
            {
                return false;
            }

            try
            {
                _port.Write(text);
                return true;
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        ClosePort();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException e) when (e.InnerExceptions.All(i => i is OperationCanceledException))
        {
            // Stopping is the expected end of the loop.
        }

        _stop.Dispose();
    }

    private void Run(CancellationToken stop)
    {
        var framer = new LineFramer();
        var buffer = new byte[256];

        while (!stop.IsCancellationRequested)
        {
            var portName = _findPort();
            if (portName is null || !TryOpen(portName))
            {
                stop.WaitHandle.WaitOne(RetryInterval);
                continue;
            }

            framer.Reset();
            Connected?.Invoke(portName);
            try
            {
                ReadUntilLost(framer, buffer, stop);
            }
            finally
            {
                ClosePort();
                Disconnected?.Invoke();
            }

            stop.WaitHandle.WaitOne(RetryInterval);
        }
    }

    private bool TryOpen(string portName)
    {
        try
        {
            SafeLineState.TurnOff(portName);
        }
        catch (IOException)
        {
            return false;
        }

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
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            port.Dispose();
            return false;
        }

        lock (_portLock)
        {
            _port = port;
        }

        return true;
    }

    private void ReadUntilLost(LineFramer framer, byte[] buffer, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            SerialPort? port;
            lock (_portLock)
            {
                port = _port;
            }

            if (port is not { IsOpen: true })
            {
                return;
            }

            int count;
            try
            {
                count = port.Read(buffer, 0, buffer.Length);
            }
            catch (TimeoutException)
            {
                continue;
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException or OperationCanceledException)
            {
                return;
            }

            foreach (var line in framer.Feed(buffer.AsSpan(0, count)))
            {
                if (!Protocol.TryParse(line, out var parsed))
                {
                    continue;
                }

                if (parsed is TimerStatusLine status)
                {
                    StatusReceived?.Invoke(status);
                }
                else if (parsed is FirmwareLine firmware)
                {
                    FirmwareReceived?.Invoke(firmware);
                }
            }
        }
    }

    private void ClosePort()
    {
        lock (_portLock)
        {
            if (_port is null)
            {
                return;
            }

            try
            {
                _port.Close();
            }
            catch (IOException)
            {
                // The device is already gone; nothing left to close.
            }

            _port.Dispose();
            _port = null;
        }
    }
}
