using System.Runtime.Versioning;
using SpeakerBuddy.Core;

namespace SpeakerBuddy.App;

/// <summary>The connection to the timer as <see cref="TalkRunner"/> uses it; the tests fake it.</summary>
internal interface ITimerLink : IDisposable
{
    event Action<string>? Connected;

    event Action? Disconnected;

    event Action<TimerStatusLine>? StatusReceived;

    event Action<FirmwareLine>? FirmwareReceived;

    void Start();

    bool Send(ProtocolLine line);
}

/// <summary>
/// Serial connection to the timer (requirements L-06, A-03): finds the port, opens it with DTR and
/// RTS off so the board does not reset, reads status lines, writes laptop lines, and reconnects
/// after the cable was pulled. Events are raised on the link's background thread.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SerialLink : ITimerLink
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly Func<string?> _findPort;
    private readonly Func<string, ISerialDevice> _open;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _portLock = new();
    private ISerialDevice? _port;
    private Task? _loop;
    private int _disposed;

    /// <param name="findPort">Returns the port to use, or null while no timer is attached.</param>
    /// <param name="log">Receives failures the link recovered from; the link never throws them on.</param>
    public SerialLink(Func<string?> findPort, Action<string> log)
        : this(findPort, WindowsSerialDevice.Open, log)
    {
    }

    /// <param name="open">Opens the named port, or throws as <see cref="WindowsSerialDevice.Open"/> does.</param>
    internal SerialLink(Func<string?> findPort, Func<string, ISerialDevice> open, Action<string> log)
    {
        _findPort = findPort ?? throw new ArgumentNullException(nameof(findPort));
        _open = open ?? throw new ArgumentNullException(nameof(open));
        _log = log ?? throw new ArgumentNullException(nameof(log));
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

    /// <summary>Stops the loop and closes the port. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stop.Cancel();
        ClosePort();
        var ended = true;
        try
        {
            ended = _loop?.Wait(StopTimeout) ?? true;
        }
        catch (AggregateException e)
        {
            // Stopping is the expected end of the loop; anything else is logged, never thrown at
            // the kill switch
            foreach (var inner in e.InnerExceptions.Where(i => i is not OperationCanceledException))
            {
                _log($"serial loop ended with {inner.GetType().Name} {inner.Message}");
            }
        }

        // A loop still inside an open or a read waits on the token; leave the token source to the
        // finalizer rather than dispose it under the running loop
        if (ended)
        {
            _stop.Dispose();
        }
    }

    private void Run(CancellationToken stop)
    {
        var framer = new LineFramer();
        var buffer = new byte[256];

        while (!stop.IsCancellationRequested)
        {
            try
            {
                ConnectAndRead(framer, buffer, stop);
            }
            catch (Exception e)
            {
                // Only the stop ends the loop: a failed search, open or close is logged and retried
                _log($"serial loop failed, retrying: {e.GetType().Name} {e.Message}");
            }

            stop.WaitHandle.WaitOne(RetryInterval);
        }
    }

    private void ConnectAndRead(LineFramer framer, byte[] buffer, CancellationToken stop)
    {
        var portName = _findPort();
        if (portName is null || !TryOpen(portName))
        {
            return;
        }

        framer.Reset();
        Raise(nameof(Connected), () => Connected?.Invoke(portName));
        try
        {
            ReadUntilLost(framer, buffer, stop);
        }
        finally
        {
            ClosePort();
            Raise(nameof(Disconnected), () => Disconnected?.Invoke());
        }
    }

    // A failing subscriber must not take the connection down with it
    private void Raise(string name, Action raise)
    {
        try
        {
            raise();
        }
        catch (Exception e)
        {
            _log($"{name} handler failed: {e.GetType().Name} {e.Message}");
        }
    }

    private bool TryOpen(string portName)
    {
        ISerialDevice port;
        try
        {
            port = _open(portName);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
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
            ISerialDevice? port;
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
                    Raise(nameof(StatusReceived), () => StatusReceived?.Invoke(status));
                }
                else if (parsed is FirmwareLine firmware)
                {
                    Raise(nameof(FirmwareReceived), () => FirmwareReceived?.Invoke(firmware));
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
            catch (Exception e) when (IsPortGone(e))
            {
                // The device is already gone; nothing left to close.
            }
            finally
            {
                try
                {
                    _port.Dispose();
                }
                catch (Exception e) when (IsPortGone(e))
                {
                    // Same: a pulled cable can fail the dispose as well.
                }

                _port = null;
            }
        }
    }

    private static bool IsPortGone(Exception e) =>
        e is IOException or UnauthorizedAccessException or InvalidOperationException;
}
